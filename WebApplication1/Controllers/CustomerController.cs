using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using WebApplication1.Data;
using WebApplication1.Models;
using System.Text.Json;
using System.Text;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(ApplicationDbContext dbContext, HttpClient httpClient, ILogger<CustomerController> logger)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _logger = logger;
        }

        [HttpGet("fetch-customer-from-db-paginated")]
        public async Task<IActionResult> FetchCustomersFromDbPaginated(
        int page = 1,
        int pageSize = 10,
        string? searchTerm = null)
        {
            try
            {
                // Get latest token to fetch QuickBooksUserId
                var tokenRecord = await _dbContext.QuickBooksTokens
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (tokenRecord == null)
                    return NotFound("No QuickBooks token found.");

                // Base query filtered by QuickBooksUserId
                var query = _dbContext.Customers
                    .Where(c => c.QuickBooksUserId == tokenRecord.QuickBooksUserId);

                // Apply search if needed
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    string likeTerm = $"%{searchTerm}%";
                    query = query.Where(c =>
                        EF.Functions.Like(c.DisplayName, likeTerm) ||
                        EF.Functions.Like(c.CompanyName, likeTerm) ||
                        EF.Functions.Like(c.Phone, likeTerm));
                }

                // Count total records
                var totalRecords = await query.CountAsync();

                // Get paginated data
                var pagedData = await query
                    .OrderBy(c => c.DisplayName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        Id = c.Id,
                        DisplayName = c.DisplayName ?? "",
                        CompanyName = c.CompanyName ?? "",
                        Phone = c.Phone ?? "",
                        Balance = c.Balance,
                        Email = c.Email ?? "",
                        BillingLine1 = c.BillingLine1 ?? "",
                        BillingCity = c.BillingCity ?? "",
                        BillingState = c.BillingState ?? "",
                        BillingPostalCode = c.BillingPostalCode ?? "",
                        BillingCountry = c.BillingCountry ?? "",
                        ShippingLine1 = c.ShippingLine1 ?? "",
                        ShippingCity = c.ShippingCity ?? "",

                    })
                    .ToListAsync();

                // Construct response
                var response = new
                {
                    TotalRecords = totalRecords,
                    TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
                    CurrentPage = page,
                    PageSize = pageSize,
                    Data = pagedData
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching customers: {ex.Message}");
            }
        }


        [HttpGet("fetch-customers-from-quickbooks")]
        public async Task<IActionResult> FetchCustomersFromQuickBooks()
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

                _logger.LogInformation("Fetching customer data from QuickBooks API.");

                var url = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/query?query=SELECT * FROM Customer";

                var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(httpRequest);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, json);

                var parsedCustomers = ParseCustomerData(json, tokenRecord.QuickBooksUserId);

                var strategy = _dbContext.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                    try
                    {
                        foreach (var customer in parsedCustomers)
                        {
                            var existingCustomer = await _dbContext.Customers
                                .FirstOrDefaultAsync(c => c.QuickBooksCustomerId == customer.QuickBooksCustomerId);

                            if (existingCustomer != null)
                            {
                                existingCustomer.DisplayName = customer.DisplayName;
                                existingCustomer.CompanyName = customer.CompanyName;
                                existingCustomer.Email = customer.Email;
                            }
                            else
                            {
                                await _dbContext.Customers.AddAsync(customer);
                            }
                        }

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                return Ok(parsedCustomers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching customers from QuickBooks: {ex.Message}");
            }
        }
        

        [HttpPost("add-customer")]
        public async Task<IActionResult> AddCustomer([FromBody] CustomerDto customerDto)
        {
            try
            {
                // Step 1: Get the latest QuickBooks token from DB
                var tokenRecord = await _dbContext.QuickBooksTokens
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (tokenRecord == null)
                    return NotFound("No QuickBooks token found.");

                var accessToken = tokenRecord.AccessToken;
                var realmId = tokenRecord.RealmId;

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(realmId))
                    return BadRequest("Missing access token or realm ID.");

                // Step 2: Validate required fields in customerDto
                if (string.IsNullOrWhiteSpace(customerDto.DisplayName) ||
                    string.IsNullOrWhiteSpace(customerDto.CompanyName) ||
                    string.IsNullOrWhiteSpace(customerDto.Phone))
                {
                    return BadRequest("DisplayName, CompanyName, and Phone are required fields.");
                }

                // Step 3: Prepare payload for QuickBooks API request
                var payload = new
                {
                    DisplayName = customerDto.DisplayName,
                    CompanyName = customerDto.CompanyName,
                    PrimaryPhone = new { FreeFormNumber = customerDto.Phone },
                    PrimaryEmailAddr = string.IsNullOrWhiteSpace(customerDto.Email) ? null : new { Address = customerDto.Email },
                    BillAddr = new
                    {
                        Line1 = customerDto.BillingLine1,
                        City = customerDto.BillingCity,
                        CountrySubDivisionCode = customerDto.BillingState,
                        PostalCode = customerDto.BillingPostalCode,
                        Country = customerDto.BillingCountry
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var requestUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/customer";

                // Step 4: Set up HTTP request to QuickBooks API
                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Headers =
            {
                Authorization = new AuthenticationHeaderValue("Bearer", accessToken),
                Accept = { new MediaTypeWithQualityHeaderValue("application/json") }
            },
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                // Step 5: Send request to QuickBooks API
                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, responseBody);
                }

                // Step 6: Parse the response from QuickBooks API
                using var document = JsonDocument.Parse(responseBody);
                var customerJson = document.RootElement.GetProperty("Customer");

                var quickBooksCustomerId = customerJson.GetProperty("Id").GetString();

                // Step 7: Save the new customer data to your local database with the QuickBooksCustomerId
                var newCustomer = new Customer
                {
                    QuickBooksUserId = tokenRecord.QuickBooksUserId,
                    QuickBooksCustomerId = quickBooksCustomerId,
                    DisplayName = customerDto.DisplayName,
                    CompanyName = customerDto.CompanyName,
                    Phone = customerDto.Phone,
                    Email = customerDto.Email,
                    BillingLine1 = customerDto.BillingLine1,
                    BillingCity = customerDto.BillingCity,
                    BillingState = customerDto.BillingState,
                    BillingPostalCode = customerDto.BillingPostalCode,
                    BillingCountry = customerDto.BillingCountry,
                    Active = true,
                    Balance = 1000,  
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _dbContext.Customers.Add(newCustomer);
                await _dbContext.SaveChangesAsync();

                return StatusCode(201, new
                {
                    Message = "Customer added successfully to QuickBooks and saved locally.",
                    CustomerId = newCustomer.Id,
                    QuickBooksCustomerId = newCustomer.QuickBooksCustomerId,
                    Customer = new
                    {
                        newCustomer.Id,
                        newCustomer.DisplayName,
                        newCustomer.CompanyName,
                        newCustomer.Phone
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error adding customer: {ex.Message}");
            }
        }

        [HttpPut("update-customer/{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] CustomerDto customerDto)
        {
            try
            {
                // Step 1: Get latest QuickBooks token
                var tokenRecord = await _dbContext.QuickBooksTokens
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (tokenRecord == null)
                    return NotFound("No QuickBooks token found.");

                var accessToken = tokenRecord.AccessToken;
                var realmId = tokenRecord.RealmId;

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(realmId))
                    return BadRequest("Missing access token or realm ID.");

                // Step 2: Get existing customer from DB
                var existingCustomer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == id);
                if (existingCustomer == null)
                    return NotFound("Customer not found.");

                // Step 3: Fetch customer from QuickBooks to get SyncToken
                var getCustomerUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/customer/{existingCustomer.QuickBooksCustomerId}";
                var getRequest = new HttpRequestMessage(HttpMethod.Get, getCustomerUrl);
                getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                getRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var getResponse = await _httpClient.SendAsync(getRequest);
                var getContent = await getResponse.Content.ReadAsStringAsync();

                if (!getResponse.IsSuccessStatusCode)
                    return StatusCode((int)getResponse.StatusCode, getContent);

                using var document = JsonDocument.Parse(getContent);
                var customerJson = document.RootElement.GetProperty("Customer");
                var syncToken = customerJson.GetProperty("SyncToken").GetString();

                // Step 4: Prepare payload for update to QuickBooks
                var qbPayload = new
                {
                    Id = existingCustomer.QuickBooksCustomerId,
                    SyncToken = syncToken,
                    DisplayName = customerDto.DisplayName,
                    CompanyName = customerDto.CompanyName,
                    PrimaryPhone = new { FreeFormNumber = customerDto.Phone },
                    PrimaryEmailAddr = string.IsNullOrWhiteSpace(customerDto.Email) ? null : new { Address = customerDto.Email },
                    BillAddr = new
                    {
                        Line1 = customerDto.BillingLine1,
                        City = customerDto.BillingCity,
                        CountrySubDivisionCode = customerDto.BillingState,
                        PostalCode = customerDto.BillingPostalCode,
                        Country = customerDto.BillingCountry
                    }
                };

                var updateUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/customer?operation=update";
                var updateRequest = new HttpRequestMessage(HttpMethod.Post, updateUrl)
                {
                    Headers =
                    {
                        Authorization = new AuthenticationHeaderValue("Bearer", accessToken),
                        Accept = { new MediaTypeWithQualityHeaderValue("application/json") }
                    },
                    Content = new StringContent(JsonSerializer.Serialize(qbPayload), Encoding.UTF8, "application/json")
                };

                // Step 5: Send update request to QuickBooks
                var updateResponse = await _httpClient.SendAsync(updateRequest);
                var updateContent = await updateResponse.Content.ReadAsStringAsync();

                if (!updateResponse.IsSuccessStatusCode)
                    return StatusCode((int)updateResponse.StatusCode, updateContent);

                // Step 6: Update local DB
                existingCustomer.DisplayName = customerDto.DisplayName;
                existingCustomer.CompanyName = customerDto.CompanyName;
                existingCustomer.Phone = customerDto.Phone;
                existingCustomer.Email = customerDto.Email;
                existingCustomer.BillingLine1 = customerDto.BillingLine1;
                existingCustomer.BillingCity = customerDto.BillingCity;
                existingCustomer.BillingState = customerDto.BillingState;
                existingCustomer.BillingPostalCode = customerDto.BillingPostalCode;
                existingCustomer.BillingCountry = customerDto.BillingCountry;
                existingCustomer.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    Message = "Customer updated successfully in both QuickBooks and local database.",
                    CustomerId = existingCustomer.Id,
                    QuickBooksCustomerId = existingCustomer.QuickBooksCustomerId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating customer: {ex.Message}");
            }
        }
        [HttpDelete("delete-customer/{id}")]
        public async Task<IActionResult> DeleteCustomer(string id)
        {
            try
            {
                // Step 1: Get latest token
                var tokenRecord = await _dbContext.QuickBooksTokens
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (tokenRecord == null)
                    return NotFound("No QuickBooks token found.");

                var accessToken = tokenRecord.AccessToken;
                var realmId = tokenRecord.RealmId;

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(realmId))
                    return BadRequest("Missing access token or realm ID.");

                // Parse local DB ID
                if (!int.TryParse(id, out int parsedId))
                {
                    return BadRequest("Invalid customer ID format.");
                }

                // Get customer from local DB
                var customer = await _dbContext.Customers
                    .FirstOrDefaultAsync(c => c.Id == parsedId);

                if (customer == null)
                    return NotFound("Customer not found in local DB.");

                string quickBooksCustomerId = customer.QuickBooksCustomerId;

                // Step 2: Get customer from QuickBooks to get SyncToken
                var getCustomerUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/customer/{quickBooksCustomerId}";
                var getRequest = new HttpRequestMessage(HttpMethod.Get, getCustomerUrl);
                getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                getRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var getResponse = await _httpClient.SendAsync(getRequest);
                var getContent = await getResponse.Content.ReadAsStringAsync();

                // If customer not found in QuickBooks, just update the local DB to inactive
                if (!getResponse.IsSuccessStatusCode)
                {
                    customer.Active = false;
                    _dbContext.Customers.Update(customer);
                    await _dbContext.SaveChangesAsync();
                    return Ok("Customer marked as inactive in local DB (not found in QuickBooks).");
                }

                using var document = JsonDocument.Parse(getContent);
                var syncToken = document.RootElement.GetProperty("Customer").GetProperty("SyncToken").GetString();

                // Soft-delete on QuickBooks (mark as inactive)
                var deletePayload = new
                {
                    Id = quickBooksCustomerId, // Use QuickBooks ID here
                    SyncToken = syncToken,
                    Active = false, // marking as inactive instead of full delete
                    DisplayName = customer.DisplayName,
                    FamilyName = customer.FamilyName
                };

                var deleteUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/customer?operation=update";
                var deleteRequest = new HttpRequestMessage(HttpMethod.Post, deleteUrl)
                {
                    Headers =
            {
                Authorization = new AuthenticationHeaderValue("Bearer", accessToken),
                Accept = { new MediaTypeWithQualityHeaderValue("application/json") }
            },
                    Content = new StringContent(JsonSerializer.Serialize(deletePayload), Encoding.UTF8, "application/json")
                };

                var deleteResponse = await _httpClient.SendAsync(deleteRequest);
                var deleteContent = await deleteResponse.Content.ReadAsStringAsync();

                // Handle QuickBooks errors but still update local DB if appropriate
                if (!deleteResponse.IsSuccessStatusCode)
                {
                    // If it's a "not found" or similar error, we should still mark locally
                    if (deleteContent.Contains("Object Not Found") || deleteContent.Contains("made inactive"))
                    {
                        customer.Active = false; // Mark as inactive locally
                        _dbContext.Customers.Update(customer);
                        await _dbContext.SaveChangesAsync();
                        return Ok($"Customer marked as inactive in local DB. QuickBooks reported: {deleteContent}");
                    }

                    return StatusCode((int)deleteResponse.StatusCode, deleteContent);
                }

                // Step 3: Mark customer as inactive in local DB
                customer.Active = false; // Mark as inactive
                _dbContext.Customers.Update(customer);
                await _dbContext.SaveChangesAsync();

                return Ok("Customer marked as inactive in QuickBooks and local DB.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error marking customer as inactive: {ex.Message}");
            }
        }

        #region Helper Methods

        private List<Customer> ParseCustomerData(string json, string quickBooksUserId)
        {
            var customers = new List<Customer>();
            var jsonResponse = JObject.Parse(json);

            if (jsonResponse["QueryResponse"]?["Customer"] is JArray customerArray)
            {
                foreach (var item in customerArray)
                {
                    var customer = new Customer
                    {
                        QuickBooksCustomerId = item["Id"]?.ToString(),
                        QuickBooksUserId = quickBooksUserId,
                        DisplayName = item["DisplayName"]?.ToString(),
                        CompanyName = string.IsNullOrWhiteSpace(item["CompanyName"]?.ToString())
                        ? item["DisplayName"]?.ToString()
                        : item["CompanyName"]?.ToString(),
                        GivenName = item["GivenName"]?.ToString(),
                        MiddleName = item["MiddleName"]?.ToString(),
                        FamilyName = item["FamilyName"]?.ToString(),
                        Title = item["Title"]?.ToString(),
                        Suffix = item["Suffix"]?.ToString(),
                        Email = item["PrimaryEmailAddr"]?["Address"]?.ToString(),
                        Phone = item["PrimaryPhone"]?["FreeFormNumber"]?.ToString(),
                        Active = item["Active"]?.ToObject<bool>() ?? false,
                        Balance = item["Balance"]?.ToObject<decimal>() ?? 0,
                        Notes = item["Notes"]?.ToString(),
                        PreferredDeliveryMethod = item["PreferredDeliveryMethod"]?.ToString(),
                        PrintOnCheckName = item["PrintOnCheckName"]?.ToString(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    // Billing address
                    if (item["BillAddr"] != null)
                    {
                        customer.BillingLine1 = item["BillAddr"]?["Line1"]?.ToString();
                        customer.BillingCity = item["BillAddr"]?["City"]?.ToString();
                        customer.BillingState = item["BillAddr"]?["CountrySubDivisionCode"]?.ToString();
                        customer.BillingPostalCode = item["BillAddr"]?["PostalCode"]?.ToString();
                        customer.BillingCountry = item["BillAddr"]?["Country"]?.ToString();
                    }

                    // Shipping address
                    if (item["ShipAddr"] != null)
                    {
                        customer.ShippingLine1 = item["ShipAddr"]?["Line1"]?.ToString();
                        customer.ShippingCity = item["ShipAddr"]?["City"]?.ToString();
                        customer.ShippingState = item["ShipAddr"]?["CountrySubDivisionCode"]?.ToString();
                        customer.ShippingPostalCode = item["ShipAddr"]?["PostalCode"]?.ToString();
                        customer.ShippingCountry = item["ShipAddr"]?["Country"]?.ToString();
                    }

                    // Metadata times
                    if (item["MetaData"] != null)
                    {
                        DateTime.TryParse(item["MetaData"]?["CreateTime"]?.ToString(), out DateTime createTime);
                        DateTime.TryParse(item["MetaData"]?["LastUpdatedTime"]?.ToString(), out DateTime updateTime);

                        if (createTime != DateTime.MinValue)
                            customer.QuickBooksCreateTime = createTime;

                        if (updateTime != DateTime.MinValue)
                            customer.QuickBooksLastUpdateTime = updateTime;
                    }

                    customers.Add(customer);
                }
            }

            return customers;
        }

        #endregion
    }
}
