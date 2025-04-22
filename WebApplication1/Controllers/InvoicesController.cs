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
                var tokenRecord = await _dbContext.QuickBooksTokens
                    .OrderByDescending(t => t.CreatedAt)
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
                        DueDate = item.TryGetProperty("DueDate", out var due) ? DateTime.Parse(due.GetString()) : null,
                        Subtotal = item.TryGetProperty("SubTotal", out var sub) ? sub.GetDecimal() : null,
                        TotalAmt = item.TryGetProperty("TotalAmt", out var total) ? total.GetDecimal() : null,
                        Balance = item.TryGetProperty("Balance", out var bal) ? bal.GetDecimal() : null,
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
                                ItemName = detail.GetProperty("ItemRef").GetProperty("name").GetString()
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


        [HttpGet("invoices")]
        public async Task<IActionResult> GetInvoices()
        {
            try
            {
                var invoices = await _dbContext.Invoices
                    .Select(i => new
                    {
                        i.QuickBooksId,
                        i.DocNumber,
                        i.CustomerName,
                        i.TxnDate,
                        i.DueDate,
                        i.Subtotal,
                        i.TotalAmt,
                        i.Balance,
                        i.CustomerEmail,
                        i.Store,
                        i.CustomerMemo,
                        i.EmailStatus,
                        i.SyncToken
                    })
                    .ToListAsync();

                return Ok(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoices from the database.");
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
                .Where(p => p.IsActive)
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



        [HttpPost("update")]
        public async Task<IActionResult> UpdateInvoice([FromBody] JsonElement invoicePayload)
        {
            try
            {
        
                var company = await _dbContext.QuickBooksTokens.FirstOrDefaultAsync();
                if (company == null)
                {
                    _logger.LogError("Company authentication data not found.");
                    return BadRequest("Company authentication data not found.");
                }

                string accessToken = company.AccessToken;
                string realmId = company.RealmId;


                _httpClient.BaseAddress = new Uri("https://sandbox-quickbooks.api.intuit.com/");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));


                string apiUrl = $"/v3/company/{realmId}/invoice?minorversion=75";
                var jsonString = JsonSerializer.Serialize(invoicePayload);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(apiUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = JsonDocument.Parse(responseString).RootElement;
                    return Ok(responseJson);
                }
                else
                {
                    _logger.LogError($"QuickBooks API error: {responseString}");
                    return StatusCode((int)response.StatusCode, $"QuickBooks API error: {responseString}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Server error: {ex.Message}");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }








    }
}
