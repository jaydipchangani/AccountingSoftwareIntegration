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
        private readonly ProductService _productService;

        public ProductsController(ApplicationDbContext dbContext, HttpClient httpClient, ILogger<CustomerController> logger, ProductService productService)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _logger = logger;
            _productService = productService;
        }

        #region
        [HttpGet("get-all-products")]
        public async Task<IActionResult> GetAllProducts(
      [FromQuery] int pageNumber = 1,
      [FromQuery] int pageSize = 10,
      [FromQuery] string? search = null)
        {
            try
            {
                var query = _dbContext.Products.AsQueryable();

                // Apply search on Name only
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(p => p.Name != null && p.Name.Contains(search));
                }

                var totalCount = await query.CountAsync();

                // Select only required fields with safe string conversion
                var products = await query
                    .OrderBy(p => p.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new
                    {
                        p.Id,
                        Name = p.Name ?? "",
                        Description = p.Description ?? "",
                        Price = p.Price.ToString(),      // force to string
                        Type = p.Type.ToString(),        // force to string
                        Platform = p.Platform ?? "",
                        QuickBooksItemId = p.QuickBooksItemId.ToString() ?? " ",
                        isActive = p.IsActive,
                        code = p.Code
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Data = products,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products.");
                return StatusCode(500, $"Internal server error retrieving products: {ex.Message}");
            }
        }


        [HttpGet("fetch-items-from-quickbooks")]
        public async Task<IActionResult> FetchItemsFromQuickBooks()
        {
            try
            {
                var tokenRecord = await _dbContext.QuickBooksTokens
                    .Where(t => t.Company == "QBO")
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (tokenRecord == null)
                    return NotFound("No QuickBooks token found.");

                var accessToken = tokenRecord.AccessToken;
                var realmId = tokenRecord.RealmId;

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(realmId))
                    return BadRequest("Missing access token or realm ID.");

                _logger.LogInformation("Fetching items from QuickBooks API.");

                var url = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/query?query=SELECT * FROM Item";
                var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(httpRequest);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, json);

                var parsedItems = ParseItemData(json, tokenRecord.QuickBooksUserId);

                var strategy = _dbContext.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                    try
                    {
                        foreach (var item in parsedItems)
                        {
                            var exists = await _dbContext.Products
                                .AnyAsync(p => p.QuickBooksItemId == item.QuickBooksItemId);

                            if (exists)
                            {
                                _logger.LogInformation("Item with QuickBooksItemId {Id} already exists. Skipping.", item.QuickBooksItemId);
                                continue;
                            }

                            if (string.IsNullOrEmpty(item.Name) ||
                                string.IsNullOrEmpty(item.Type) ||
                                string.IsNullOrEmpty(item.IncomeAccount) ||
                                string.IsNullOrEmpty(item.IncomeAccountId))
                            {
                                _logger.LogWarning("Skipping item with missing required fields: {@Item}", item);
                                continue;
                            }

                            item.Platform = "QBO";
                           
                            
                            await _dbContext.Products.AddAsync(item);
                        }

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during item import transaction. Rolling back.");
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
                        Type = item["Type"]?.ToString() ?? "Service",
                        Price = item["UnitPrice"]?.ToObject<decimal?>(),
                        QuantityOnHand = item["QtyOnHand"]?.ToObject<decimal?>(),
                        AsOfDate = DateTime.TryParse(item["InvStartDate"]?.ToString(), out var parsedDate) ? parsedDate : (DateTime?)null,
                        SyncToken = item["SyncToken"]?.ToString(),
                        QuickBooksUserId = quickBooksUserId,
                        IsTrackedAsInventory = item["TrackQtyOnHand"]?.ToObject<bool?>() ?? false,
                        Platform= "QBO"
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

                    // Purchase Details
                    var purchaseDetails = item["PurchaseCost"]?.ToObject<decimal?>();
                    if (purchaseDetails != null)
                        product.PurchaseUnitPrice = purchaseDetails;

                    product.PurchaseCOGSAccountCode = item["ExpenseAccountRef"]?["value"]?.ToString(); // Often used for COGS
                    product.PurchaseTaxType = item["PurchaseTaxCodeRef"]?["value"]?.ToString();

                    // Sales Details
                    product.SalesUnitPrice = item["UnitPrice"]?.ToObject<decimal?>();
                    product.SalesAccountCode = item["IncomeAccountRef"]?["value"]?.ToString();
                    product.SalesTaxType = item["SalesTaxCodeRef"]?["value"]?.ToString();

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
            // Convert string id to int
            if (!int.TryParse(id, out int parsedId))
                return BadRequest("Invalid product ID format.");

            // Fetch product by parsedId
            var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == parsedId);
            if (product == null)
                return NotFound("Product not found.");

            // Get latest QuickBooks token
            var tokenRecord = await _dbContext.QuickBooksTokens
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (tokenRecord == null)
                return NotFound("No QuickBooks token found.");

            var accessToken = tokenRecord.AccessToken;
            var realmId = tokenRecord.RealmId;

            // Attempt to fetch the item from QuickBooks
            var getUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/item/{product.QuickBooksItemId}";
            var getRequest = new HttpRequestMessage(HttpMethod.Get, getUrl);
            getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            getRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var getResponse = await _httpClient.SendAsync(getRequest);
            var getContent = await getResponse.Content.ReadAsStringAsync();

            // If QuickBooks fetch fails, update only local DB
            if (!getResponse.IsSuccessStatusCode)
            {
                product.IsActive = statusDto.IsActive;
                _dbContext.Products.Update(product);
                await _dbContext.SaveChangesAsync();
                return Ok("Local product updated, but QuickBooks item not found.");
            }

            // Extract SyncToken
            var itemObj = JObject.Parse(getContent)?["Item"];
            var syncToken = itemObj?["SyncToken"]?.ToString();

            // Prepare update payload for QuickBooks
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

                var tokenRecord = await _dbContext.QuickBooksTokens
                                .Where(t => t.Company == "QBO")
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
                    ["Description"] = productDto.Description,
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


        private async Task<(string SyncToken, string Id)> GetLatestItemInfoAsync(string itemId, string realmId, string accessToken)
        {
            var url = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/item/{itemId}?minorversion=65";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to fetch latest item info: {content}");

            dynamic json = JsonConvert.DeserializeObject(content);
            return (json.Item.SyncToken.ToString(), json.Item.Id.ToString());
        }


        [HttpPut("edit-product/{id}")]
        public async Task<IActionResult> EditProduct(string id, [FromBody] UpdateProductRequest updatedProduct)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest("Product ID is required.");

            if (updatedProduct == null)
                return BadRequest("Updated product data is required.");

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
                    return BadRequest("Access token or Realm ID is missing.");

                // Step 1: Fetch current product from QuickBooks
                var fetchUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/item/{id}?minorversion=65";
                var fetchRequest = new HttpRequestMessage(HttpMethod.Get, fetchUrl);
                fetchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                fetchRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var fetchResponse = await _httpClient.SendAsync(fetchRequest);
                var fetchContent = await fetchResponse.Content.ReadAsStringAsync();

                if (!fetchResponse.IsSuccessStatusCode)
                    return StatusCode((int)fetchResponse.StatusCode, fetchContent);

                var currentProduct = JsonConvert.DeserializeObject<QuickBooksProductResponse>(fetchContent)?.Item;
                if (currentProduct == null)
                    return BadRequest("Could not retrieve existing product.");

                // Step 2: Check for duplicate name
                var nameCheckUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/query?query=select * from Item where Name = '{updatedProduct.Name.Replace("'", "''")}'&minorversion=65";
                var nameCheckRequest = new HttpRequestMessage(HttpMethod.Get, nameCheckUrl);
                nameCheckRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                nameCheckRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var nameCheckResponse = await _httpClient.SendAsync(nameCheckRequest);
                var nameCheckContent = await nameCheckResponse.Content.ReadAsStringAsync();

                var nameCheckResult = JsonConvert.DeserializeObject<QuickBooksQueryResponse>(nameCheckContent);
                var duplicateItem = nameCheckResult?.QueryResponse?.Item?.FirstOrDefault(i => i.Id != id);

                // 🔁 If name is duplicate, keep original name
                var finalName = duplicateItem != null ? currentProduct.Name : updatedProduct.Name;

                // Step 3: Build sparse update payload
                object updatePayload;

                if (updatedProduct.Type.Equals("Service", StringComparison.OrdinalIgnoreCase))
                {
                    updatePayload = new
                    {
                        sparse = true,
                        Id = id,
                        SyncToken = currentProduct.SyncToken,
                        Name = finalName,
                        Description = updatedProduct.Description,
                        UnitPrice = updatedProduct.UnitPrice,
                        IncomeAccountRef = string.IsNullOrEmpty(updatedProduct.IncomeAccountId) ? null : new { value = updatedProduct.IncomeAccountId }
                    };
                }
                else // Inventory
                {
                    updatePayload = new
                    {
                        sparse = true,
                        Id = id,
                        SyncToken = currentProduct.SyncToken,
                        Name = finalName,
                        Description = updatedProduct.Description,
                        UnitPrice = updatedProduct.UnitPrice,
                        IncomeAccountRef = string.IsNullOrEmpty(updatedProduct.IncomeAccountId) ? null : new { value = updatedProduct.IncomeAccountId },
                        AssetAccountRef = string.IsNullOrEmpty(updatedProduct.AssetAccountId) ? null : new { value = updatedProduct.AssetAccountId },
                        ExpenseAccountRef = string.IsNullOrEmpty(updatedProduct.ExpenseAccountId) ? null : new { value = updatedProduct.ExpenseAccountId }
                    };
                }

                // Step 4: Send update request
                var updateUrl = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/item?minorversion=65";
                var updateRequest = new HttpRequestMessage(HttpMethod.Post, updateUrl);
                updateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                updateRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                updateRequest.Content = new StringContent(JsonConvert.SerializeObject(updatePayload), Encoding.UTF8, "application/json");

                var updateResponse = await _httpClient.SendAsync(updateRequest);
                var updateContent = await updateResponse.Content.ReadAsStringAsync();

                if (!updateResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"QuickBooks Error Response: {updateContent}");
                    return StatusCode((int)updateResponse.StatusCode, updateContent);
                }

                return Ok("Product updated successfully in QuickBooks.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return StatusCode(500, "An error occurred while updating the product.");
            }
        }

        #endregion

        #region

        [HttpGet("xero-get-all-products")]
        public async Task<IActionResult> GetProducts([FromQuery] string type = "Service", [FromQuery] int page = 1)
{
    const int pageSize = 10; // Adjust as needed
    var result = await _productService.FetchAndStoreXeroProductsAsync(type, page, pageSize);
    return Ok(result);
}

        [HttpPost("xero-add-product")]
        public async Task<IActionResult> AddProductsToXero([FromBody] List<Product> products)
        {
            if (products == null || !products.Any())
                return BadRequest("Product list cannot be empty.");

            try
            {
                await _productService.AddProductsToXeroAndDbAsync(products);
                return Ok("Products successfully added to Xero and local DB.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }


        [HttpPost("xero-update-product/{itemId}")]
        public async Task<IActionResult> UpdateProduct(string itemId, [FromBody] Product product)
        {
            try
            {
                await _productService.UpdateProductInXeroAndDbAsync(itemId, product);
                return Ok(new { message = "Product updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }


        [HttpDelete("xero-delete-product/{itemId}")]
        public async Task<IActionResult> DeleteProduct(string itemId)
        {
            try
            {
                await _productService.DeleteProductFromXeroAndDbAsync(itemId);
                return Ok(new { message = "Product deleted successfully from Xero and local database." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }



        #endregion




    }
}

