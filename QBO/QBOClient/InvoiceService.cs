using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class InvoiceService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<InvoiceService> _logger;
        private readonly HttpClient _httpClient;

        public InvoiceService(ApplicationDbContext dbContext, ILogger<InvoiceService> logger, IHttpClientFactory httpClientFactory)
        {
            _dbContext = dbContext;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<string> SyncInvoicesAsync(string accessToken, string realmId)
        {
            try
            {
                _logger.LogInformation("Fetching invoice data from QuickBooks API.");

                var url = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/query?query=SELECT * FROM Invoice";

                var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(httpRequest);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return json;

                var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (!root.TryGetProperty("QueryResponse", out var queryResponse) ||
                    !queryResponse.TryGetProperty("Invoice", out var invoicesJson))
                {
                    return "No invoices found.";
                }

                _logger.LogInformation("Clearing existing invoice data.");
                _dbContext.InvoiceLineItems.RemoveRange(_dbContext.InvoiceLineItems);
                _dbContext.Invoices.RemoveRange(_dbContext.Invoices);
                await _dbContext.SaveChangesAsync();

                foreach (var item in invoicesJson.EnumerateArray())
                {
                    var quickBooksId = item.GetProperty("Id").GetString();

                    var invoice = new Invoice
                    {
                        QuickBooksId = quickBooksId,
                        DocNumber = item.GetProperty("DocNumber").GetString(),
                        CustomerName = item.GetProperty("CustomerRef").GetProperty("name").GetString(),
                        CustomerId = item.GetProperty("CustomerRef").GetProperty("value").GetString(),
                        TxnDate = DateTime.Parse(item.GetProperty("TxnDate").GetString()),
                        DueDate = item.TryGetProperty("DueDate", out var due) ? DateTime.Parse(due.GetString()) : DateTime.UtcNow,
                        Subtotal = item.TryGetProperty("SubTotal", out var sub) ? sub.GetDecimal() : 0,
                        TotalAmt = item.TryGetProperty("TotalAmt", out var total) ? total.GetDecimal() : 0,
                        Balance = item.TryGetProperty("Balance", out var bal) ? bal.GetDecimal() : 0,
                        CustomerEmail = item.TryGetProperty("BillEmail", out var email) ? email.GetProperty("Address").GetString() : null,
                        BillingAddressJson = item.TryGetProperty("BillAddr", out var billAddr) ? billAddr.ToString() : null,
                        ShippingAddressJson = item.TryGetProperty("ShipAddr", out var shipAddr) ? shipAddr.ToString() : null,
                        Store = item.TryGetProperty("Store", out var store) ? store.GetString() : null,
                        CustomerMemo = item.TryGetProperty("CustomerMemo", out var memo) ? memo.GetProperty("value").GetString() : null,
                        EmailStatus = item.TryGetProperty("EmailStatus", out var emailStatus) ? emailStatus.GetString() : null,
                        SyncToken = item.TryGetProperty("SyncToken", out var syncToken) ? syncToken.GetString() : null,
                        LineItems = new List<InvoiceLineItem>(),
                    };

                    if (item.TryGetProperty("Line", out var lineItems))
                    {
                        foreach (var line in lineItems.EnumerateArray())
                        {
                            if (!line.TryGetProperty("SalesItemLineDetail", out var detail)) continue;

                            var invoiceLine = new InvoiceLineItem
                            {
                                Description = line.TryGetProperty("Description", out var desc) ? desc.GetString() : null,
                                Amount = line.TryGetProperty("Amount", out var amt) ? amt.GetDecimal() : 0,
                                ItemRef = detail.GetProperty("ItemRef").GetProperty("value").GetString(),
                                ItemName = detail.GetProperty("ItemRef").GetProperty("name").GetString(),
                                PlatformLineItem = "Xero"
                            };

                            invoice.LineItems.Add(invoiceLine);
                        }
                    }

                    _dbContext.Invoices.Add(invoice);
                }

                await _dbContext.SaveChangesAsync();

                return "Invoices synced and saved successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing invoices from QuickBooks.");
                return "An error occurred while syncing invoices.";
            }
        }

        public async Task<string> SoftDeleteInvoiceAsync(string accessToken, string realmId, string invoiceId, string syncToken)
        {
            try
            {
                // Fetch invoice from local DB
                var invoice = await _dbContext.Invoices
                    .FirstOrDefaultAsync(i => i.QuickBooksId == invoiceId);

                if (invoice == null)
                    return "Invoice not found in local database.";

                if (invoice.SyncToken != syncToken)
                    return "SyncToken mismatch.";

                // Soft delete in local DB
                invoice.IsActive = false;
                _dbContext.Invoices.Update(invoice);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Invoice marked inactive locally. Now updating in QuickBooks.");

                // Prepare request body for QuickBooks API
                var payload = new
                {
                    Id = invoiceId,
                    SyncToken = syncToken
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(payload));

                jsonContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var url = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/invoice?operation=delete&minorversion=75";

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpRequest.Content = jsonContent;

                var response = await _httpClient.SendAsync(httpRequest);
                var apiResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("QuickBooks API failed: " + apiResponse);
                    return "QuickBooks API error: " + apiResponse;
                }

                return "Invoice successfully soft-deleted in local DB and QuickBooks.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during soft delete operation.");
                return "An error occurred during soft delete.";
            }
        }

        public async Task<string> AddInvoiceAsync(string accessToken, string realmId, InvoiceRequest invoiceRequest)
        {
            try
            {
                var jsonPayload = JsonSerializer.Serialize(invoiceRequest);

                var request = new HttpRequestMessage(HttpMethod.Post, $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/invoice?minorversion=75")
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return "Error adding invoice: " + content;

                return "Invoice added successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding invoice to QuickBooks.");
                return "An error occurred while adding the invoice.";
            }
        }
    }
}
