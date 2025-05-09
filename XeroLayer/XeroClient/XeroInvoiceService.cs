using Microsoft.Extensions.DependencyInjection; // Required for CreateClient() extension
using System.Net.Http.Headers;


using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.Json;

using WebApplication1.Data;
using Microsoft.EntityFrameworkCore;
using DataLayer.Models.Xero;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;



    public class XeroInvoiceService
     {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly HttpClient _httpClient;


        public XeroInvoiceService(ApplicationDbContext db, IHttpClientFactory httpClientFactory, HttpClient httpClient)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _httpClient = httpClient;

        }

        public async Task<int> FetchAndStoreInvoicesAsync(string? type = null)
    {
        if (type != null && type != "ACCPAY" && type != "ACCREC")
            throw new ArgumentException("Invalid invoice type. Allowed values are: ACCPAY, ACCREC, or null.");

        var auth = await _db.QuickBooksTokens
            .Where(x => x.Company == "Xero")
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (auth == null)
            throw new Exception("Xero auth details not found.");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        client.DefaultRequestHeaders.Add("xero-tenant-id", auth.TenantId);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await client.GetAsync("https://api.xero.com/api.xro/2.0/Invoices?page=1");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JObject.Parse(json);
        var invoicesJson = doc["Invoices"];
        int count = 0;

        var existingXeroInvoices = await _db.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.Platform == "Xero")
            .ToListAsync();

        _db.InvoiceLineItems.RemoveRange(existingXeroInvoices.SelectMany(i => i.LineItems));
        _db.Invoices.RemoveRange(existingXeroInvoices);
        await _db.SaveChangesAsync();

        var filteredInvoices = type == null
            ? invoicesJson
            : invoicesJson.Where(i => i["Type"]?.ToString() == type);

        foreach (var invoiceJson in filteredInvoices)
        {
            var rawDocNumber = invoiceJson["InvoiceNumber"]?.ToString();
            var docNumber = string.IsNullOrWhiteSpace(rawDocNumber) ? $"XERO-{Guid.NewGuid():N}" : rawDocNumber;

            if (await _db.Invoices.AnyAsync(i => i.DocNumber == docNumber && i.Platform != "Xero"))
                continue; // Avoid conflict if the same DocNumber exists from a different platform

            var invoiceId = invoiceJson["InvoiceID"]?.ToString();

            var invoice = new Invoice
            {
                QuickBooksId = invoiceId,
                CustomerName = invoiceJson["Contact"]?["Name"]?.ToString() ?? string.Empty,
                CustomerEmail = invoiceJson["Contact"]?["EmailAddress"]?.ToString() ?? string.Empty,
                DocNumber = docNumber,
                CustomerMemo = invoiceJson["Reference"]?.ToString() ?? string.Empty,
                TxnDate = TryGetDateTime(invoiceJson, "Date"),
                DueDate = TryGetDateTime(invoiceJson, "DueDate"),
                Subtotal = invoiceJson["SubTotal"]?.ToObject<decimal>() ?? 0,
                TotalAmt = invoiceJson["Total"]?.ToObject<decimal>() ?? 0,
                Balance = invoiceJson["AmountDue"]?.ToObject<decimal>() ?? 0,
                XeroInvoiceType = invoiceJson["Type"]?.ToString() ?? string.Empty,
                XeroStatus = invoiceJson["Status"]?.ToString() ?? string.Empty,
                XeroBrandingThemeID = invoiceJson["BrandingThemeID"]?.ToString() ?? string.Empty,
                XeroCurrencyCode = invoiceJson["CurrencyCode"]?.ToString() ?? string.Empty,
                XeroCurrencyRate = invoiceJson["CurrencyRate"]?.ToObject<decimal>() ?? 1,
                XeroIsDiscounted = invoiceJson["IsDiscounted"]?.ToObject<bool>() ?? false,
                XeroLineAmountTypes = invoiceJson["LineAmountTypes"]?.ToString() ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Platform = "Xero",
            };

            foreach (var lineJson in invoiceJson["LineItems"])
            {
                invoice.LineItems.Add(new InvoiceLineItem
                {
                    Description = lineJson["Description"]?.ToString() ?? string.Empty,
                    Quantity = lineJson["Quantity"]?.ToObject<decimal>() ?? 0,
                    Rate = lineJson["UnitAmount"]?.ToObject<decimal>() ?? 0,
                    Amount = lineJson["LineAmount"]?.ToObject<decimal>() ?? 0,
                    XeroLineItemId = lineJson["LineItemID"]?.ToString() ?? string.Empty,
                    XeroAccountCode = lineJson["AccountCode"]?.ToString() ?? string.Empty,
                    XeroTaxType = lineJson["TaxType"]?.ToString() ?? string.Empty,
                    XeroTaxAmount = lineJson["TaxAmount"]?.ToObject<decimal>() ?? 0,
                    XeroDiscountRate = lineJson["DiscountRate"]?.ToObject<decimal>() ?? 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            _db.Invoices.Add(invoice);
            count++;
        }

        await _db.SaveChangesAsync();

        return count;
    }

        private DateTime TryGetDateTime(JToken jsonElement, string propertyName)
        {
            var dateElement = jsonElement[propertyName];
            if (dateElement != null && DateTime.TryParse(dateElement.ToString(), out var result))
            {
                return result;
            }
            return DateTime.MinValue; // Return a default DateTime value if parsing fails
        }

        public async Task<string> AddInvoiceToXeroAndDbAsync(XeroInvoiceCreateDto dto, string accessToken, string tenantId)
        {
            string invoiceId = string.Empty;

            // Step 1: Construct payload
            var payload = new
            {
                Invoices = new[]
                {
            new
            {
                Type = dto.Type,
                Contact = new { ContactID = dto.ContactId },
                LineItems = dto.LineItems.Select(item => new
                {
                    item.Description,
                    item.Quantity,
                    item.UnitAmount,
                    item.AccountCode,
                    item.TaxType,
                    item.LineAmount
                }).ToList(),
                Date = dto.Date.ToString("yyyy-MM-dd"),
                DueDate = dto.DueDate.ToString("yyyy-MM-dd"),
                Reference = dto.Reference,
                Status = "DRAFT"
            }
        }
            };


            var request = new HttpRequestMessage(HttpMethod.Put, "https://api.xero.com/api.xro/2.0/Invoices");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("xero-tenant-id", tenantId);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Xero Sync Failed: {error}");
            }

            // Step 3: Read response from Xero
            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            var xeroInvoice = doc.RootElement.GetProperty("Invoices")[0];

            invoiceId = xeroInvoice.GetProperty("InvoiceID").GetString() ?? "";

            /* 

            // Step 4: Prepare local invoice entity
            var invoice = new Invoice
            {
                QuickBooksId = invoiceId,
                DocNumber = xeroInvoice.GetProperty("InvoiceNumber").GetString() ?? "",
                CustomerId = xeroInvoice.GetProperty("Contact").GetProperty("ContactID").GetString() ?? "",
                CustomerName = xeroInvoice.GetProperty("Contact").GetProperty("Name").GetString() ?? "",
                TxnDate = xeroInvoice.GetProperty("Date").GetDateTime(),
                DueDate = xeroInvoice.GetProperty("DueDate").GetDateTime(),
                Subtotal = xeroInvoice.TryGetProperty("SubTotal", out var subtotalEl) ? subtotalEl.GetDecimal() : 0,
                TotalAmt = xeroInvoice.GetProperty("Total").GetDecimal(),
                Balance = xeroInvoice.GetProperty("AmountDue").GetDecimal(),
                XeroStatus = xeroInvoice.GetProperty("Status").GetString() ?? "",
                XeroInvoiceType = xeroInvoice.GetProperty("Type").GetString() ?? ""
            };

            foreach (var lineItem in xeroInvoice.GetProperty("LineItems").EnumerateArray())
            {
                invoice.LineItems.Add(new InvoiceLineItem
                {
                    ProductId = lineItem.GetProperty("ProductId").GetString() ?? "UnknownProduct",
                    ProductName = lineItem.GetProperty("ProductName").GetString() ?? "UnknownProductName",
                    Description = lineItem.GetProperty("Description").GetString() ?? "",
                    Quantity = lineItem.GetProperty("Quantity").GetDecimal(),
                    Rate = lineItem.GetProperty("UnitAmount").GetDecimal(),
                    Amount = lineItem.GetProperty("LineAmount").GetDecimal(),
                    LineNum = 1,
                    DetailType = "SalesItemLineDetail",
                    ItemRef = lineItem.GetProperty("ItemRef").GetString() ?? "UnknownItemRef",
                    ItemName = lineItem.GetProperty("ItemName").GetString() ?? "UnknownItemName",
                    XeroLineItemId = lineItem.GetProperty("LineItemID").GetString() ?? "",
                    XeroAccountCode = lineItem.GetProperty("AccountCode").GetString() ?? "",
                    XeroAccountId = "",
                    XeroTaxType = lineItem.GetProperty("TaxType").GetString() ?? "",
                    XeroTaxAmount = lineItem.GetProperty("TaxAmount").GetDecimal(),
                    XeroDiscountRate = 0,
                    PlatformLineItem = "Xero"
                });
            }

            // Step 5: Save to local DB

            try
            {
                Console.WriteLine("Saving invoice to local database...");

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                _context.InvoiceLineItems.AddRange(invoice.LineItems);
                await _context.SaveChangesAsync();
            }
            catch (Exception dbEx)
            {
                // Rollback strategy or error handling
                throw new Exception($"Invoice was saved to Xero but failed to save in local DB. InvoiceID: {invoiceId}, Error: {dbEx.Message}");
            }
            */
                        return "Data Added to Xero";
                    }


        public async Task<IActionResult> DeleteInvoice(string invoiceId)
    {
        var tokenDetails = await _db.QuickBooksTokens
            .Where(x => x.Company == "Xero")
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (tokenDetails == null)
            return StatusCode(401, "Token not found.");

        string accessToken = tokenDetails.AccessToken;
        string tenantId = tokenDetails.TenantId;

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.xero.com/api.xro/2.0/Invoices/{invoiceId}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        getRequest.Headers.Add("Xero-tenant-id", tenantId);

        var getResponse = await _httpClient.SendAsync(getRequest);
        if (!getResponse.IsSuccessStatusCode)
            return StatusCode(404, "Invoice not found in Xero.");

        var responseContent = await getResponse.Content.ReadAsStringAsync();
        var xeroInvoiceResponse = JsonSerializer.Deserialize<XeroInvoiceResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var xeroInvoice = xeroInvoiceResponse?.Invoices?.FirstOrDefault();
        if (xeroInvoice == null)
            return StatusCode(404, "Invoice not found in Xero response.");

        if (!xeroInvoice.Status.Equals("DRAFT", StringComparison.OrdinalIgnoreCase))
            return StatusCode(400, "Invoice status must be 'DRAFT' to delete.");

        var localInvoice = await _db.Invoices.FirstOrDefaultAsync(i => i.QuickBooksId == invoiceId);
        if (localInvoice == null)
            return StatusCode(404, "Invoice not found in local database.");

        var payload = new
        {
            Invoices = new[]
            {
            new
            {
                InvoiceID = invoiceId,
                Type = localInvoice.XeroInvoiceType,
                Status = "DELETED"
            }
        }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var deleteRequest = new HttpRequestMessage(HttpMethod.Post, $"https://api.xero.com/api.xro/2.0/Invoices")
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
        };
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        deleteRequest.Headers.Add("Xero-tenant-id", tenantId);

        var deleteResponse = await _httpClient.SendAsync(deleteRequest);
        if (!deleteResponse.IsSuccessStatusCode)
            return StatusCode(500, "Failed to delete invoice in Xero.");

        localInvoice.XeroStatus = "DELETED";
        localInvoice.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return StatusCode(200, "Invoice deleted successfully.");
    }



        public async Task<string> UpdateInvoiceInXeroAsync(string invoiceId, XeroInvoiceUpdateDto dto, string accessToken, string tenantId)
        {
        // Step 1: Fetch the existing invoice by QuickBooksId (which corresponds to InvoiceID in the request body)
        var existingInvoice = await _db.Invoices.FirstOrDefaultAsync(inv => inv.QuickBooksId == invoiceId);

        if (existingInvoice == null)
            throw new Exception($"No local invoice found with InvoiceID (QuickBooksId): {invoiceId}");

        // Step 2: Ensure the status is "DRAFT" before proceeding
        if (!string.Equals(existingInvoice.XeroStatus, "DRAFT", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Invoice cannot be updated because its status is not 'DRAFT'.");

        // Step 3: Build the payload for Xero invoice update
        var payload = new
        {
            Invoices = new[]
            {
        new
        {
        InvoiceID = invoiceId, // InvoiceID from the request body
        Type = existingInvoice.XeroInvoiceType, // Existing invoice type from the database
        //Contact = new { ContactID = existingInvoice.XeroContactId }, // ContactID (already stored in DB)
        LineItems = dto.LineItems.Select(item => new
        {
                item.Description,
                item.Quantity,
                item.UnitAmount,
                item.AccountCode,
                item.TaxType,
                item.LineAmount
            }).ToList(),
            Date = dto.Date.ToString("yyyy-MM-dd"),
            DueDate = dto.DueDate.ToString("yyyy-MM-dd"),
            Reference = dto.Reference,
            Status = dto.Status ?? existingInvoice.XeroStatus // Preserve existing status unless specified
        }
        }
        };

        // Step 4: Send the update request to Xero API
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.xero.com/api.xro/2.0/Invoices/{invoiceId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("xero-tenant-id", tenantId);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);

        // Step 5: Check if the response is successful
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Xero invoice update failed: {error}");
        }

        // Step 6: Optionally, parse the updated invoice data (if needed)
        var responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        var updatedInvoice = doc.RootElement.GetProperty("Invoices")[0];
        var updatedInvoiceId = updatedInvoice.GetProperty("InvoiceID").GetString();


return updatedInvoiceId ?? invoiceId;
}


        public async Task<string> GetInvoiceFromXeroByIdAsync(string invoiceId)
        {
            // Step 1: Get the latest Xero token
            var token = await _db.QuickBooksTokens
                .Where(t => t.Company == "Xero")
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (token == null)
                throw new Exception("No Xero access token found.");

            var accessToken = token.AccessToken;
            var tenantId = token.TenantId;

            // Step 2: Build request
            var url = $"https://api.xero.com/api.xro/2.0/Invoices/{invoiceId}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("xero-tenant-id", tenantId);

            // Step 3: Send request
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Xero API Error: {error}");
            }

            // Step 4: Return raw JSON
            return await response.Content.ReadAsStringAsync();
        }


    /*
    public async Task<int> FetchAndStoreInvoicesAsync()
    {
        var auth = await _db.QuickBooksTokens
            .Where(x => x.Company == "Xero")
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (auth == null)
            throw new Exception("Xero auth details not found.");

        // Prepare HTTP client
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        client.DefaultRequestHeaders.Add("xero-tenant-id", auth.TenantId);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Always fetch page 1 invoices
        var response = await client.GetAsync("https://api.xero.com/api.xro/2.0/Invoices?page=1");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JObject.Parse(json);
        var invoicesJson = doc["Invoices"];

        int count = 0;

        foreach (var invoiceJson in invoicesJson)
        {
            var invoiceId = invoiceJson["InvoiceID"]?.ToString();
            var existing = await _db.Invoices.FirstOrDefaultAsync(i => i.QuickBooksId == invoiceId);
            if (existing != null) continue;

            var invoice = new Invoice
            {
                QuickBooksId = invoiceId,
                CustomerName = invoiceJson["Contact"]?["Name"]?.ToString() ?? string.Empty,
                CustomerEmail = invoiceJson["Contact"]?["EmailAddress"]?.ToString() ?? string.Empty,
                DocNumber = invoiceJson["InvoiceNumber"]?.ToString() ?? string.Empty,
                CustomerMemo = invoiceJson["Reference"]?.ToString() ?? string.Empty,
                TxnDate = TryGetDateTime(invoiceJson, "Date"),
                DueDate = TryGetDateTime(invoiceJson, "DueDate"),
                Subtotal = invoiceJson["SubTotal"]?.ToObject<decimal>() ?? 0,
                TotalAmt = invoiceJson["Total"]?.ToObject<decimal>() ?? 0,
                Balance = invoiceJson["AmountDue"]?.ToObject<decimal>() ?? 0,
                XeroInvoiceType = invoiceJson["Type"]?.ToString() ?? string.Empty,
                XeroStatus = invoiceJson["Status"]?.ToString() ?? string.Empty,
                XeroBrandingThemeID = invoiceJson["BrandingThemeID"]?.ToString() ?? string.Empty,
                XeroCurrencyCode = invoiceJson["CurrencyCode"]?.ToString() ?? string.Empty,
                XeroCurrencyRate = invoiceJson["CurrencyRate"]?.ToObject<decimal>() ?? 1,
                XeroIsDiscounted = invoiceJson["IsDiscounted"]?.ToObject<bool>() ?? false,
                XeroLineAmountTypes = invoiceJson["LineAmountTypes"]?.ToString() ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Platform = "Xero",
            };

            foreach (var lineJson in invoiceJson["LineItems"])
            {
                invoice.LineItems.Add(new InvoiceLineItem
                {
                    Description = lineJson["Description"]?.ToString() ?? string.Empty,
                    Quantity = lineJson["Quantity"]?.ToObject<decimal>() ?? 0,
                    Rate = lineJson["UnitAmount"]?.ToObject<decimal>() ?? 0,
                    Amount = lineJson["LineAmount"]?.ToObject<decimal>() ?? 0,
                    XeroLineItemId = lineJson["LineItemID"]?.ToString() ?? string.Empty,
                    XeroAccountCode = lineJson["AccountCode"]?.ToString() ?? string.Empty,
                    XeroTaxType = lineJson["TaxType"]?.ToString() ?? string.Empty,
                    XeroTaxAmount = lineJson["TaxAmount"]?.ToObject<decimal>() ?? 0,
                    XeroDiscountRate = lineJson["DiscountRate"]?.ToObject<decimal>() ?? 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            _db.Invoices.Add(invoice);
            count++;
        }

        await _db.SaveChangesAsync();

        return count;
    }

    */

    private IActionResult StatusCode(int v1, string v2)
    {
        throw new NotImplementedException();
    }
}

