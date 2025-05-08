using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoiceContoller : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<QuickBooksController> _logger;
        private readonly HttpClient _httpClient;

        public InvoiceContoller(ApplicationDbContext dbContext, ILogger<QuickBooksController> logger, IHttpClientFactory httpClientFactory)
        {
            _dbContext = dbContext;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpGet("sync-invoices")]
        public async Task<IActionResult> SyncInvoices()
        {
            try
            {
                var qboInvoices = _dbContext.Invoices.Where(i => i.Platform == "QBO").ToList();
                _dbContext.Invoices.RemoveRange(qboInvoices);
                _dbContext.SaveChanges();

                var qboLineItems = _dbContext.InvoiceLineItems
                .Where(x => x.PlatformLineItem == "QBO")
                .ToList();
                _dbContext.InvoiceLineItems.RemoveRange(qboLineItems);
                _dbContext.SaveChanges();

                _dbContext.InvoiceLineItems.RemoveRange(_dbContext.InvoiceLineItems);

                var tokenRecord = await _dbContext.QuickBooksTokens
                .Where(x => x.Company == "QBO")  // Filter by Xero company
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync();

                if (tokenRecord == null)
                    return NotFound("No QuickBooks token found.");

                var accessToken = tokenRecord.AccessToken;
                var realmId = tokenRecord.RealmId;

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(realmId))
                    return BadRequest("Missing access token or realm ID.");

                _logger.LogInformation("Fetching invoice data from QuickBooks API.");

                var url = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/query?query=SELECT * FROM Invoice";

                var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(httpRequest);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, json);

                var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (!root.TryGetProperty("QueryResponse", out var queryResponse) ||
                    !queryResponse.TryGetProperty("Invoice", out var invoicesJson))
                {
                    return Ok("No invoices found.");
                }

                _logger.LogInformation("Clearing existing invoice data.");

                await _dbContext.SaveChangesAsync();


                foreach (var item in invoicesJson.EnumerateArray())
                {
                    var quickBooksId = item.GetProperty("Id").GetString() ?? string.Empty;

                    var invoice = new Invoice
                    {
                        QuickBooksId = quickBooksId,
                        DocNumber = item.TryGetProperty("DocNumber", out var doc) ? doc.GetString() ?? " " : " ",
                        CustomerName = item.TryGetProperty("CustomerRef", out var custRef) && custRef.TryGetProperty("name", out var custName) ? custName.GetString() ?? " " : " ",
                        CustomerId = custRef.TryGetProperty("value", out var custId) ? custId.GetString() ?? " " : " ",
                        TxnDate = DateTime.TryParse(item.GetProperty("TxnDate").GetString(), out var txnDate) ? txnDate : DateTime.UtcNow,
                        DueDate = item.TryGetProperty("DueDate", out var dueProp) && DateTime.TryParse(dueProp.GetString(), out var dueDate) ? dueDate : DateTime.UtcNow,

                        Subtotal = item.TryGetProperty("SubTotal", out var sub) ? sub.GetDecimal() : 0,
                        TotalAmt = item.TryGetProperty("TotalAmt", out var total) ? total.GetDecimal() : 0,
                        Balance = item.TryGetProperty("Balance", out var bal) ? bal.GetDecimal() : 0,

                        CustomerEmail = item.TryGetProperty("BillEmail", out var email) && email.TryGetProperty("Address", out var addr) ? addr.GetString() ?? " " : " ",
                        BillingAddressJson = item.TryGetProperty("BillAddr", out var billAddr) ? billAddr.ToString() ?? " " : " ",
                        ShippingAddressJson = item.TryGetProperty("ShipAddr", out var shipAddr) ? shipAddr.ToString() ?? " " : " ",

                        Store = item.TryGetProperty("Store", out var store) ? store.GetString() ?? " " : " ",
                        CustomerMemo = item.TryGetProperty("CustomerMemo", out var memo) && memo.TryGetProperty("value", out var memoVal) ? memoVal.GetString() ?? " " : " ",
                        EmailStatus = item.TryGetProperty("EmailStatus", out var emailStatus) ? emailStatus.GetString() ?? " " : " ",
                        SyncToken = item.TryGetProperty("SyncToken", out var syncToken) ? syncToken.GetString() ?? " " : " ",
                        Platform = "QBO",

                        LineItems = new List<InvoiceLineItem>(),
                    };

                    if (item.TryGetProperty("Line", out var lineItems))
                    {
                        foreach (var line in lineItems.EnumerateArray())
                        {
                            if (!line.TryGetProperty("SalesItemLineDetail", out var detail)) continue;

                            var invoiceLine = new InvoiceLineItem
                            {
                                Description = line.TryGetProperty("Description", out var desc) ? desc.GetString() ?? " " : " ",
                                Amount = line.TryGetProperty("Amount", out var amt) ? amt.GetDecimal() : 0,
                                ItemRef = detail.TryGetProperty("ItemRef", out var itemRef) && itemRef.TryGetProperty("value", out var itemVal) ? itemVal.GetString() ?? " " : " ",
                                ItemName =  itemRef.TryGetProperty("name", out var itemName) ? itemName.GetString() ?? " " : " ",
                                PlatformLineItem = "QBO"
                            };

                            invoice.LineItems.Add(invoiceLine);
                        }
                    }

                    _dbContext.Invoices.Add(invoice);
                }


                await _dbContext.SaveChangesAsync();

                return Ok("Invoices synced and saved successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing invoices from QuickBooks.");
                return StatusCode(500, "An error occurred while syncing invoices.");
            }
        }



        [HttpGet("get-invoices")]
        public async Task<IActionResult> GetInvoices(
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string? searchTerm = null,
    [FromQuery] string? platform = null,
    [FromQuery] string? invoiceType = null) 
        {
            try
            {
                var query = _dbContext.Invoices.AsQueryable();

                if (!string.IsNullOrEmpty(platform))
                {
                    query = query.Where(i => i.Platform.ToLower() == platform.ToLower());
                }

                if (!string.IsNullOrEmpty(invoiceType))
                {
                    query = query.Where(i => i.XeroInvoiceType == invoiceType);
                }

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(i => i.CustomerName.Contains(searchTerm));
                }

                var totalRecords = await query.CountAsync();

                var invoices = await query
                    .OrderByDescending(i => i.TxnDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Ok(new
                {
                    TotalRecords = totalRecords,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    Invoices = invoices
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoices.");
                return StatusCode(500, "An error occurred while fetching invoices.");
            }
        }





        [HttpPost("soft-delete")]
        public async Task<IActionResult> SoftDeleteInvoice([FromBody] SoftDeleteInvoiceRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Id) || string.IsNullOrEmpty(request.SyncToken))
                    return BadRequest("Id and SyncToken are required.");

                // Fetch latest QuickBooks token
                var tokenRecord = await _dbContext.QuickBooksTokens
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (tokenRecord == null)
                    return NotFound("QuickBooks token not found.");

                var accessToken = tokenRecord.AccessToken;
                var realmId = tokenRecord.RealmId;

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(realmId))
                    return BadRequest("Access token or RealmId missing.");

                // Fetch invoice from local DB
                var invoice = await _dbContext.Invoices
                    .FirstOrDefaultAsync(i => i.QuickBooksId == request.Id);

                if (invoice == null)
                    return NotFound("Invoice not found in local database.");

                if (invoice.SyncToken != request.SyncToken)
                    return BadRequest("SyncToken mismatch.");

                // Soft delete in local DB
                invoice.IsActive = false;
                _dbContext.Invoices.Update(invoice);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Invoice marked inactive locally. Now updating in QuickBooks.");

                // Prepare request body for QuickBooks API
                var payload = new
                {
                    Id = request.Id,
                    SyncToken = request.SyncToken,

                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(payload));

                jsonContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var url = $"https://sandbox-quickbooks.api.intuit.com//v3/company/{realmId}/invoice?operation=delete&minorversion=75";

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpRequest.Content = jsonContent;

                var response = await _httpClient.SendAsync(httpRequest);
                var apiResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("QuickBooks API failed: " + apiResponse);
                    return StatusCode((int)response.StatusCode, "QuickBooks API error: " + apiResponse);
                }

                return Ok("Invoice successfully soft-deleted in local DB and QuickBooks.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during soft delete operation.");
                return StatusCode(500, "An internal error occurred.");
            }
        }


        // API to fetch customers
        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers()
        {
            var customers = await _dbContext.Customers
                .Where(c => c.Active)
                .Select(c => new
                {
                    c.QuickBooksCustomerId,
                    c.DisplayName,
                    c.Email,       // Assuming the customer model has Email
                    c.BillingLine1,
                    c.BillingCity,
                    c.BillingState,
                    c.BillingCountry,
                    c.BillingPostalCode
                })
                .ToListAsync();

            return Ok(customers);
        }


        [HttpGet("products")]
        public IActionResult GetProducts()
        {
            var products = _dbContext.Products
                .Where(p => (bool)p.IsActive)
                .Select(p => new Product
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price=p.Price,
                    Description=p.Description
                })
                .ToList();

            return Ok(products);
        }


        [HttpDelete("void-invoice/{id}")]
        public async Task<IActionResult> VoidInvoice(string id)
        {
            try
            {
                // Fetch the QuickBooks token from the database
                var tokenRecord = await _dbContext.QuickBooksTokens
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (tokenRecord == null)
                    return NotFound("No QuickBooks token found.");

                var accessToken = tokenRecord.AccessToken;
                var realmId = tokenRecord.RealmId;

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(realmId))
                    return BadRequest("Missing access token or realm ID.");

                // Get the existing invoice to get the SyncToken
                var existingInvoice = await _dbContext.Invoices.FirstOrDefaultAsync(i => i.QuickBooksId == id);
                if (existingInvoice == null)
                    return NotFound("Invoice not found.");

                // QuickBooks API URL to void the invoice
                var url = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/invoice?operation=void";
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Headers =
            {
                Authorization = new AuthenticationHeaderValue("Bearer", accessToken),
                Accept = { MediaTypeWithQualityHeaderValue.Parse("application/json") }
            }
                };

                // Request body to void the invoice
                var requestBody = new
                {
                    Id = id,
                    SyncToken = existingInvoice.SyncToken
                };

                httpRequest.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                // Send the request to QuickBooks API
                var response = await _httpClient.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                // Handle response
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Error response: {content}");
                    return StatusCode((int)response.StatusCode, content);
                }

                // Update the invoice status to 'Voided' in the database
                //existingInvoice.Status = "Voided";
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Invoice #{id} voided successfully in QuickBooks and updated in the database.");

                return Ok("Invoice voided successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception: {ex.Message}");
                return StatusCode(500, "An error occurred while voiding the invoice.");
            }
        }



        [HttpPost("add-invoice")]
        public async Task<IActionResult> AddInvoice([FromBody] InvoiceRequest invoiceRequest)
        {
            try
            {
                var tokenRecord = await _dbContext.QuickBooksTokens
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (tokenRecord == null)
                    return NotFound("No QuickBooks token found.");

                var accessToken = tokenRecord.AccessToken;
                var realmId = tokenRecord.RealmId;

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(realmId))
                    return BadRequest("Missing access token or realm ID.");

                var jsonPayload = JsonSerializer.Serialize(invoiceRequest);

                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/invoice?minorversion=75")
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, content);

                return Ok(JsonDocument.Parse(content).RootElement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while adding invoice to QuickBooks");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }


        [HttpGet("{quickBooksId}")]
        public async Task<IActionResult> GetInvoiceFromQuickBooks(string quickBooksId)
        {
            if (string.IsNullOrEmpty(quickBooksId))
            {
                return BadRequest("quickBooksId is required.");
            }

            try
            {
                // Get latest QuickBooks auth info (adjust table name if needed)
                var authInfo = await _dbContext.QuickBooksTokens
                    .OrderByDescending(q => q.Id)
                    .FirstOrDefaultAsync();

                if (authInfo == null || string.IsNullOrEmpty(authInfo.AccessToken) || string.IsNullOrEmpty(authInfo.RealmId))
                {
                    return BadRequest("QuickBooks authentication info not found in the database.");
                }

                string accessToken = authInfo.AccessToken;
                string realmId = authInfo.RealmId;
                string minorVersion = "75";

                string url = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/invoice/{quickBooksId}?minorversion={minorVersion}";

                // Set authorization headers
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Make request
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetails = await response.Content.ReadAsStringAsync();
                    _logger.LogError("QuickBooks API error: {ErrorDetails}", errorDetails);
                    return StatusCode((int)response.StatusCode, $"QuickBooks API error: {errorDetails}");
                }

                // Success – return JSON response from QuickBooks
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching invoice from QuickBooks");
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }



       //[HttpPut("update-invoice/{id}")]
       // public async Task<IActionResult> UpdateInvoice(string id, [FromBody] UpdateInvoiceRequest request)
       // {
       //     if (string.IsNullOrEmpty(id))
       //         return BadRequest("Invoice ID is required.");

       //     try
       //     {
       //         // 1. Get QuickBooks auth info from DB
       //         var tokenRecord = await _dbContext.QuickBooksTokens
       //             .OrderByDescending(t => t.CreatedAt)
       //             .FirstOrDefaultAsync();

       //         if (tokenRecord == null)
       //             return NotFound("No QuickBooks token found.");

       //         var accessToken = tokenRecord.AccessToken;
       //         var realmId = tokenRecord.RealmId;

       //         if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(realmId))
       //             return BadRequest("Missing access token or realm ID.");

       //         _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
       //         _httpClient.DefaultRequestHeaders.Accept.Clear();
       //         _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

       //         // 2. Fetch existing invoice to get SyncToken
       //         var getUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/invoice/{id}?minorversion=75";
       //         var getResponse = await _httpClient.GetAsync(getUrl);
       //         var getContent = await getResponse.Content.ReadAsStringAsync();

       //         if (!getResponse.IsSuccessStatusCode)
       //         {
       //             _logger.LogError("Failed to fetch invoice: {Content}", getContent);
       //             return StatusCode((int)getResponse.StatusCode, getContent);
       //         }

       //         using var doc = JsonDocument.Parse(getContent);
       //         var syncToken = doc.RootElement.GetProperty("Invoice").GetProperty("SyncToken").GetString();

       //         // 3. Construct payload for update
       //         var lineItems = request.LineItems.Select(item => new
       //         {
       //             Id = item.Id,
       //             DetailType = "SalesItemLineDetail",
       //             Amount = item.Amount,
       //             Description = item.Description,
       //             SalesItemLineDetail = new
       //             {
       //                 ItemRef = new { value = item.ItemId },
       //                 Qty = item.Quantity,
       //                 UnitPrice = item.UnitPrice
       //             }
       //         });

       //         var updatePayload = new
       //         {
       //             Id = id,
       //             SyncToken = syncToken,
       //             sparse = true,
       //             CustomerRef = new { value = request.CustomerId },
       //             Line = lineItems,
       //             DocNumber = request.InvoiceNumber,
       //             TxnDate = request.Date?.ToString("yyyy-MM-dd"),
       //             DueDate = request.DueDate?.ToString("yyyy-MM-dd"),
       //             PrivateNote = request.Notes
       //         };

       //         var jsonBody = JsonSerializer.Serialize(updatePayload);
       //         var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

       //         // 4. Send update request
       //         var updateUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/invoice?minorversion=75";
       //         var updateResponse = await _httpClient.PostAsync(updateUrl, content);
       //         var updateContent = await updateResponse.Content.ReadAsStringAsync();

       //         if (!updateResponse.IsSuccessStatusCode)
       //         {
       //             _logger.LogError("Invoice update failed: {Content}", updateContent);
       //             return StatusCode((int)updateResponse.StatusCode, updateContent);
       //         }

       //         var updatedInvoiceJson = JsonDocument.Parse(updateContent).RootElement;
       //         return Ok(updatedInvoiceJson);
       //     }
       //     catch (Exception ex)
       //     {
       //         _logger.LogError(ex, "Error while updating invoice.");
       //         return StatusCode(500, $"Server error: {ex.Message}");
       //     }
       // }  //[HttpPut("update-invoice/{id}")]
       // public async Task<IActionResult> UpdateInvoice(string id, [FromBody] UpdateInvoiceRequest request)
       // {
       //     if (string.IsNullOrEmpty(id))
       //         return BadRequest("Invoice ID is required.");

       //     try
       //     {
       //         // 1. Get QuickBooks auth info from DB
       //         var tokenRecord = await _dbContext.QuickBooksTokens
       //             .OrderByDescending(t => t.CreatedAt)
       //             .FirstOrDefaultAsync();

       //         if (tokenRecord == null)
       //             return NotFound("No QuickBooks token found.");

       //         var accessToken = tokenRecord.AccessToken;
       //         var realmId = tokenRecord.RealmId;

       //         if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(realmId))
       //             return BadRequest("Missing access token or realm ID.");

       //         _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
       //         _httpClient.DefaultRequestHeaders.Accept.Clear();
       //         _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

       //         // 2. Fetch existing invoice to get SyncToken
       //         var getUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/invoice/{id}?minorversion=75";
       //         var getResponse = await _httpClient.GetAsync(getUrl);
       //         var getContent = await getResponse.Content.ReadAsStringAsync();

       //         if (!getResponse.IsSuccessStatusCode)
       //         {
       //             _logger.LogError("Failed to fetch invoice: {Content}", getContent);
       //             return StatusCode((int)getResponse.StatusCode, getContent);
       //         }

       //         using var doc = JsonDocument.Parse(getContent);
       //         var syncToken = doc.RootElement.GetProperty("Invoice").GetProperty("SyncToken").GetString();

       //         // 3. Construct payload for update
       //         var lineItems = request.LineItems.Select(item => new
       //         {
       //             Id = item.Id,
       //             DetailType = "SalesItemLineDetail",
       //             Amount = item.Amount,
       //             Description = item.Description,
       //             SalesItemLineDetail = new
       //             {
       //                 ItemRef = new { value = item.ItemId },
       //                 Qty = item.Quantity,
       //                 UnitPrice = item.UnitPrice
       //             }
       //         });

       //         var updatePayload = new
       //         {
       //             Id = id,
       //             SyncToken = syncToken,
       //             sparse = true,
       //             CustomerRef = new { value = request.CustomerId },
       //             Line = lineItems,
       //             DocNumber = request.InvoiceNumber,
       //             TxnDate = request.Date?.ToString("yyyy-MM-dd"),
       //             DueDate = request.DueDate?.ToString("yyyy-MM-dd"),
       //             PrivateNote = request.Notes
       //         };

       //         var jsonBody = JsonSerializer.Serialize(updatePayload);
       //         var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

       //         // 4. Send update request
       //         var updateUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/invoice?minorversion=75";
       //         var updateResponse = await _httpClient.PostAsync(updateUrl, content);
       //         var updateContent = await updateResponse.Content.ReadAsStringAsync();

       //         if (!updateResponse.IsSuccessStatusCode)
       //         {
       //             _logger.LogError("Invoice update failed: {Content}", updateContent);
       //             return StatusCode((int)updateResponse.StatusCode, updateContent);
       //         }

       //         var updatedInvoiceJson = JsonDocument.Parse(updateContent).RootElement;
       //         return Ok(updatedInvoiceJson);
       //     }
       //     catch (Exception ex)
       //     {
       //         _logger.LogError(ex, "Error while updating invoice.");
       //         return StatusCode(500, $"Server error: {ex.Message}");
       //     }
       // }









    }
}
