using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebApplication1.Data;
using WebApplication1.Models;

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
                var root = JObject.Parse(json);
                var items = root["QueryResponse"]?["Item"] as JArray;

                if (items == null)
                {
                    _logger.LogWarning("No items found in QuickBooks response.");
                    return result;
                }

                foreach (var item in items)
                {
                    var product = new Product
                    {
                        QuickBooksItemId = item["Id"]?.ToString(),
                        Name = item["Name"]?.ToString(),
                        Description = item["Description"]?.ToString() ?? "",
                        Price = item["UnitPrice"]?.ToObject<decimal>() ?? 0,
                        Type = item["Type"]?.ToString() ?? "Service",
                        QuickBooksUserId = quickBooksUserId,
                        QuantityOnHand = item["QtyOnHand"]?.ToObject<decimal?>(),
                        AsOfDate = DateTime.TryParse(item["InvStartDate"]?.ToString(), out var parsedDate) ? parsedDate : (DateTime?)null
                    };

                    if (item["IncomeAccountRef"] != null)
                    {
                        product.IncomeAccount = item["IncomeAccountRef"]?["name"]?.ToString();
                        product.IncomeAccountId = item["IncomeAccountRef"]?["value"]?.ToString();
                    }

                    if (item["AssetAccountRef"] != null)
                    {
                        product.AssetAccount = item["AssetAccountRef"]?["name"]?.ToString();
                        product.AssetAccountId = item["AssetAccountRef"]?["value"]?.ToString();
                    }

                    if (item["ExpenseAccountRef"] != null)
                    {
                        product.ExpenseAccount = item["ExpenseAccountRef"]?["name"]?.ToString();
                        product.ExpenseAccountId = item["ExpenseAccountRef"]?["value"]?.ToString();
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

            var itemObj = JObject.Parse(getContent)?["Item"];
            var syncToken = itemObj?["SyncToken"]?.ToString();



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
                Content = new StringContent(JsonConvert.SerializeObject(updatePayload), Encoding.UTF8, "application/json")

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
                    ["Name"] = productDto.Name,
                    ["Type"] = productDto.Type,
                    ["IncomeAccountRef"] = new { value = productDto.IncomeAccountId },
                    ["UnitPrice"] = productDto.Price,
                    ["Active"] = true,
                    ["TrackQtyOnHand"] = productDto.Type == "Inventory"
                };

                // Basic validation
                if (string.IsNullOrWhiteSpace(productDto.Name) ||
                    string.IsNullOrWhiteSpace(productDto.Type))
                {
                    return BadRequest("Name and Type are required fields.");
                }

                // Inventory-specific validation
                if (productDto.Type == "Inventory")
                {
                    if (string.IsNullOrWhiteSpace(productDto.AssetAccount) ||
                        string.IsNullOrWhiteSpace(productDto.ExpenseAccount) ||
                        string.IsNullOrWhiteSpace(productDto.IncomeAccount) ||
                        productDto.QuantityOnHand == null ||
                        productDto.AsOfDate == null)
                    {
                        return BadRequest("AssetAccount, ExpenseAccount, IncomeAccount, QuantityOnHand, and AsOfDate are required for inventory items.");
                    }
                }

                var jsonPayload = JsonConvert.SerializeObject(payload);

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
                var responseObj = JObject.Parse(responseBody);
                var itemJson = responseObj["Item"];


                var quickBooksItemId = itemJson?["Id"]?.ToString();


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

                httpRequest.Content = new StringContent(JsonConvert.SerializeObject(sparsePayload), Encoding.UTF8, "application/json");

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

