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
   
        [Route("api/[controller]")]
        [ApiController]
        public class ProductsController : ControllerBase
        {
        private readonly ApplicationDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly ILogger<CustomerController> _logger;

        public ProductsController(ApplicationDbContext dbContext, HttpClient httpClient, ILogger<CustomerController> logger)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllProducts()
        {
            try
            {
                var products = await _dbContext.Products.ToListAsync();

                if (products == null || !products.Any())
                    return NotFound("No products found in the database.");

                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error retrieving products: {Message}", ex.Message);
                return StatusCode(500, "Internal server error retrieving products.");
            }
        }


        [HttpGet("fetch-items-from-quickbooks")]
        public async Task<IActionResult> FetchItemsFromQuickBooks()
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

                _logger.LogInformation("Fetching item data from QuickBooks API.");

                var url = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/query?query=SELECT * FROM Item";

                var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(httpRequest);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, json);

                //  Ensure your parser populates IncomeAccountId, AssetAccountId, ExpenseAccountId too!
                var parsedItems = ParseItemData(json, tokenRecord.QuickBooksUserId);

                var strategy = _dbContext.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                    try
                    {
                        foreach (var item in parsedItems)
                        {
                            var existingItem = await _dbContext.Products
                                .FirstOrDefaultAsync(p => p.QuickBooksItemId == item.QuickBooksItemId);

                            if (existingItem != null)
                            {
                                _logger.LogInformation("Item with QuickBooksItemId {Id} already exists. Skipping.", item.QuickBooksItemId);
                                continue;
                            }

                            // ✅ Validate critical required fields
                            if (string.IsNullOrEmpty(item.Name) ||
                                string.IsNullOrEmpty(item.Type) ||
                                string.IsNullOrEmpty(item.IncomeAccount) ||
                                string.IsNullOrEmpty(item.IncomeAccountId))
                            {
                                _logger.LogWarning("Skipping item with missing required fields: {@Item}", item);
                                continue;
                            }

                            await _dbContext.Products.AddAsync(item);
                        }

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Transaction failed. Rolling back.");
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                return Ok(parsedItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during FetchItemsFromQuickBooks.");
                return StatusCode(500, $"Error fetching items from QuickBooks: {ex.Message} {(ex.InnerException?.Message ?? "")}");
            }
        }


        private List<Product> ParseItemData(string json, string quickBooksUserId)
        {
            var result = new List<Product>();

            try
            {
                var root = JsonDocument.Parse(json);

                if (!root.RootElement.TryGetProperty("QueryResponse", out var queryResponse) ||
                    !queryResponse.TryGetProperty("Item", out var items))
                {
                    _logger.LogWarning("No items found in QuickBooks response.");
                    return result;
                }

                foreach (var item in items.EnumerateArray())
                {
                    var product = new Product
                    {
                        QuickBooksItemId = item.TryGetProperty("Id", out var qbid) ? qbid.GetString() : null,
                        Name = item.GetProperty("Name").GetString(),
                        Description = item.TryGetProperty("Description", out var desc) ? desc.GetString() : "",
                        Price = item.TryGetProperty("UnitPrice", out var price) ? price.GetDecimal() : 0,
                        Type = item.TryGetProperty("Type", out var type) ? type.GetString() : "Service",
                        QuickBooksUserId = quickBooksUserId,
                        QuantityOnHand = item.TryGetProperty("QtyOnHand", out var qty) ? qty.GetDecimal() : (decimal?)null,
                        AsOfDate = item.TryGetProperty("InvStartDate", out var invDate) && DateTime.TryParse(invDate.GetString(), out var parsedDate)
                                    ? parsedDate
                                    : (DateTime?)null
                    };

                    // Income Account
                    if (item.TryGetProperty("IncomeAccountRef", out var incomeRef))
                    {
                        product.IncomeAccount = incomeRef.TryGetProperty("name", out var incomeName) ? incomeName.GetString() : null;
                        product.IncomeAccountId = incomeRef.TryGetProperty("value", out var incomeVal) ? incomeVal.GetString() : null;
                    }

                    // Asset Account (Inventory type only)
                    if (item.TryGetProperty("AssetAccountRef", out var assetRef))
                    {
                        product.AssetAccount = assetRef.TryGetProperty("name", out var assetName) ? assetName.GetString() : null;
                        product.AssetAccountId = assetRef.TryGetProperty("value", out var assetVal) ? assetVal.GetString() : null;
                    }

                    // Expense Account (optional, sometimes present in Inventory)
                    if (item.TryGetProperty("ExpenseAccountRef", out var expenseRef))
                    {
                        product.ExpenseAccount = expenseRef.TryGetProperty("name", out var expenseName) ? expenseName.GetString() : null;
                        product.ExpenseAccountId = expenseRef.TryGetProperty("value", out var expenseVal) ? expenseVal.GetString() : null;
                    }

                    result.Add(product);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error parsing QuickBooks item data: {Message}", ex.Message);
            }

            return result;
        }


        [HttpPost("delete-product/{id}")]
        public async Task<IActionResult> DeleteProduct(string id, [FromBody] UpdateStatusDto statusDto)
        {
            // Validate
            if (!int.TryParse(id, out int parsedId))
                return BadRequest("Invalid product ID format.");

            var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == parsedId);
            if (product == null)
                return NotFound("Product not found.");

            var tokenRecord = await _dbContext.QuickBooksTokens.OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync();
            if (tokenRecord == null)
                return NotFound("No QuickBooks token found.");

            var accessToken = tokenRecord.AccessToken;
            var realmId = tokenRecord.RealmId;

            // Get QuickBooks item
            var getUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/item/{product.QuickBooksItemId}";
            var getRequest = new HttpRequestMessage(HttpMethod.Get, getUrl);
            getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            getRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var getResponse = await _httpClient.SendAsync(getRequest);
            var getContent = await getResponse.Content.ReadAsStringAsync();

            if (!getResponse.IsSuccessStatusCode)
            {
                product.IsActive = statusDto.IsActive;
                _dbContext.Products.Update(product);
                await _dbContext.SaveChangesAsync();
                return Ok("Local product updated, but QuickBooks item not found.");
            }

            var item = JsonDocument.Parse(getContent).RootElement.GetProperty("Item");
            var syncToken = item.GetProperty("SyncToken").GetString();

            // Mark inactive in QuickBooks
            var updatePayload = new
            {
                sparse = true,
                Id = product.QuickBooksItemId,
                SyncToken = syncToken,
                Active = statusDto.IsActive
            };

            var updateRequest = new HttpRequestMessage(HttpMethod.Post,
                $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/item")
            {
                Headers =
        {
            Authorization = new AuthenticationHeaderValue("Bearer", accessToken),
            Accept = { new MediaTypeWithQualityHeaderValue("application/json") }
        },
                Content = new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json")
            };

            var updateResponse = await _httpClient.SendAsync(updateRequest);
            var updateContent = await updateResponse.Content.ReadAsStringAsync();

            if (!updateResponse.IsSuccessStatusCode)
                return StatusCode((int)updateResponse.StatusCode, updateContent);

            // Update local DB
            product.IsActive = statusDto.IsActive;
            _dbContext.Products.Update(product);
            await _dbContext.SaveChangesAsync();

            return Ok("Product status updated in QuickBooks and local DB.");
        }



        [HttpPost("add-product")]
        public async Task<IActionResult> AddProduct([FromBody] ProductDto productDto)
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

                // Step 2: Validate required fields
                if (string.IsNullOrWhiteSpace(productDto.Name) ||
                    string.IsNullOrWhiteSpace(productDto.Type) ||
                    string.IsNullOrWhiteSpace(productDto.IncomeAccount))
                {
                    return BadRequest("Name, Type, and Income Account are required fields.");
                }

                if (productDto.Type == "Inventory")
                {
                    if (string.IsNullOrWhiteSpace(productDto.AssetAccount) ||
                        string.IsNullOrWhiteSpace(productDto.ExpenseAccount) ||
                        productDto.QuantityOnHand == null ||
                        productDto.AsOfDate == null)
                    {
                        return BadRequest("AssetAccount, ExpenseAccount, QuantityOnHand, and AsOfDate are required for inventory items.");
                    }
                }

                // Step 3: Prepare payload for QBO
                var payload = new Dictionary<string, object>
                {
                    ["VId"] = Guid.NewGuid().ToString(),
                    ["Name"] = productDto.Name,
                    ["FullyQualifiedName"] = productDto.Name,
                    ["Type"] = productDto.Type,
                    ["IncomeAccountRef"] = new { name = productDto.IncomeAccount, value = productDto.IncomeAccountId },
                    ["UnitPrice"] = productDto.Price,
                    ["Active"] = true,
                    ["TrackQtyOnHand"] = productDto.Type == "Inventory"
                };

                if (!string.IsNullOrWhiteSpace(productDto.Description))
                {
                    payload["Description"] = productDto.Description;
                }

                if (productDto.Type == "Inventory")
                {
                    payload["AssetAccountRef"] = new { name = productDto.AssetAccount, value = productDto.AssetAccountId };
                    payload["ExpenseAccountRef"] = new { name = productDto.ExpenseAccount, value = productDto.ExpenseAccountId };
                    payload["QtyOnHand"] = productDto.QuantityOnHand;
                    payload["InvStartDate"] = productDto.AsOfDate?.ToString("yyyy-MM-dd");
                }

                var jsonPayload = JsonSerializer.Serialize(payload);
                var requestUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/item";

                // Step 4: Setup request
                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Headers =
            {
                Authorization = new AuthenticationHeaderValue("Bearer", accessToken),
                Accept = { new MediaTypeWithQualityHeaderValue("application/json") }
            },
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                // Step 5: Send request
                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, responseBody);
                }

                // Step 6: Parse response
                using var document = JsonDocument.Parse(responseBody);
                var itemJson = document.RootElement.GetProperty("Item");

                var quickBooksItemId = itemJson.GetProperty("Id").GetString();

                // Step 7: Save to DB
                var newProduct = new Product
                {
                    QuickBooksUserId = tokenRecord.QuickBooksUserId,
                    QuickBooksItemId = quickBooksItemId,
                    Name = productDto.Name,
                    Description = productDto.Description,
                    Type = productDto.Type,
                    Price = productDto.Price,
                    IncomeAccount = productDto.IncomeAccount,
                    IncomeAccountId = productDto.IncomeAccountId,
                    AssetAccount = productDto.Type == "Inventory" ? productDto.AssetAccount : null,
                    AssetAccountId = productDto.Type == "Inventory" ? productDto.AssetAccountId : null,
                    ExpenseAccount = productDto.Type == "Inventory" ? productDto.ExpenseAccount : null,
                    ExpenseAccountId = productDto.Type == "Inventory" ? productDto.ExpenseAccountId : null,
                    QuantityOnHand = productDto.Type == "Inventory" ? productDto.QuantityOnHand : null,
                    AsOfDate = productDto.Type == "Inventory" ? productDto.AsOfDate : null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _dbContext.Products.Add(newProduct);
                await _dbContext.SaveChangesAsync();

                return StatusCode(201, new
                {
                    Message = "Product added successfully to QuickBooks and saved locally.",
                    ProductId = newProduct.Id,
                    QuickBooksItemId = newProduct.QuickBooksItemId,
                    Product = new
                    {
                        newProduct.Id,
                        newProduct.Name,
                        newProduct.Type,
                        newProduct.Price
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error adding product: {ex.Message}");
            }
        }


        [HttpPut("edit-product/{id}")]
        public async Task<IActionResult> EditProduct(string id, [FromQuery] string realmId, [FromBody] UpdateProductRequest updatedProduct)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest("Product ID is required.");
            if (string.IsNullOrEmpty(realmId))
                return BadRequest("Realm ID is required.");
            if (updatedProduct == null)
                return BadRequest("Updated product data is required.");

            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return BadRequest("Missing or invalid access token.");

            var accessToken = authHeader["Bearer ".Length..].Trim();

            try
            {
                var url = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/item?minorversion=65";
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Headers =
            {
                Authorization = new AuthenticationHeaderValue("Bearer", accessToken),
                Accept = { MediaTypeWithQualityHeaderValue.Parse("application/json") }
            }
                };

                // Create sparse update payload based on product type
                object sparsePayload;

                if (updatedProduct.Type.Equals("Service", StringComparison.OrdinalIgnoreCase))
                {
                    sparsePayload = new
                    {
                        sparse = true,
                        Id = updatedProduct.Id,
                        //SyncToken = updatedProduct.SyncToken,
                        Name = updatedProduct.Name,
                        Description = updatedProduct.Description,
                        UnitPrice = updatedProduct.UnitPrice,
                        IncomeAccountRef = string.IsNullOrEmpty(updatedProduct.IncomeAccountId) ? null : new { value = updatedProduct.IncomeAccountId }
                    };
                }
                else // Inventory
                {
                    sparsePayload = new
                    {
                        sparse = true,
                        Id = updatedProduct.Id,
                        //SyncToken = updatedProduct.SyncToken,
                        Name = updatedProduct.Name,
                        Description = updatedProduct.Description,
                        UnitPrice = updatedProduct.UnitPrice,
                        IncomeAccountRef = string.IsNullOrEmpty(updatedProduct.IncomeAccountId) ? null : new { value = updatedProduct.IncomeAccountId },
                        AssetAccountRef = string.IsNullOrEmpty(updatedProduct.AssetAccountId) ? null : new { value = updatedProduct.AssetAccountId },
                        ExpenseAccountRef = string.IsNullOrEmpty(updatedProduct.ExpenseAccountId) ? null : new { value = updatedProduct.ExpenseAccountId }
                    };
                }

                httpRequest.Content = new StringContent(JsonSerializer.Serialize(sparsePayload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"QuickBooks Error Response: {content}");
                    return StatusCode((int)response.StatusCode, content);
                }

                return Ok("Product updated successfully in QuickBooks.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return StatusCode(500, "An error occurred while updating the product.");
            }
        }




    }
}

