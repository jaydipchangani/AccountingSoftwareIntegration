using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using WebApplication1.Data;
using WebApplication1.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Text;


public class ProductService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ApplicationDbContext _context;

    public ProductService(HttpClient httpClient, IConfiguration config, ApplicationDbContext context)
    {
        _httpClient = httpClient;
        _config = config;
        _context = context;
    }

    public async Task<(string accessToken, string tenantId)> GetXeroAuthDetailsAsync()
    {
        // Fetch the most recent Xero token and tenant ID from the database
        var xeroToken = await _context.QuickBooksTokens
            .Where(t => t.Company == "Xero")
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (xeroToken == null)
        {
            throw new Exception("Xero token not found.");
        }

        // Return the access token and tenant ID as a tuple
        return (xeroToken.AccessToken, xeroToken.TenantId);
    }
    //public async Task FetchAndStoreXeroProductsAsync()
    //{

    //    var accessToken = await _context.XeroTokens.Select(x => x.AccessToken).FirstOrDefaultAsync();
    //    var tenantId = await _context.XeroTokens.Select(x => x.TenantId).FirstOrDefaultAsync();

    //    var request = new HttpRequestMessage(HttpMethod.Get, "https://api.xero.com/api.xro/2.0/Items");
    //    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    //    request.Headers.Add("Xero-Tenant-Id", tenantId);
    //    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    //    var response = await _httpClient.SendAsync(request);
    //    response.EnsureSuccessStatusCode();

    //    var responseContent = await response.Content.ReadAsStringAsync();
    //    var xeroData = JsonConvert.DeserializeObject<XeroItemsResponse>(responseContent);

    //    foreach (var item in xeroData.Items)
    //    {
    //        var product = new Product
    //        {
    //            Name = item.Name,
    //            Description = item.Description ?? item.PurchaseDescription,
    //            Type = item.IsTrackedAsInventory ? "Inventory" : "Service",
    //            Price = item.SalesDetails?.UnitPrice ?? item.PurchaseDetails?.UnitPrice ?? 0,
    //            IncomeAccount = item.SalesDetails?.AccountCode ?? "Unknown",
    //            AssetAccount = item.InventoryAssetAccountCode,
    //            ExpenseAccount = item.PurchaseDetails?.COGSAccountCode,
    //            QuantityOnHand = item.QuantityOnHand,
    //            AsOfDate = DateTime.UtcNow,
    //            QuickBooksItemId = item.ItemID.ToString(),
    //            UpdatedAt = DateTime.UtcNow,
    //            CreatedAt = DateTime.UtcNow
    //        };

    //        var existing = await _context.Products
    //            .FirstOrDefaultAsync(p => p.Code == product.Code);

    //        if (existing == null)
    //        {
    //            _context.Products.Add(product);
    //        }
    //        else
    //        {
    //            existing.Name = product.Name;
    //            existing.Description = product.Description;
    //            existing.Type = product.Type;
    //            existing.Price = product.Price;
    //            existing.IncomeAccount = product.IncomeAccount;
    //            existing.AssetAccount = product.AssetAccount;
    //            existing.ExpenseAccount = product.ExpenseAccount;
    //            existing.QuantityOnHand = product.QuantityOnHand;
    //            existing.UpdatedAt = DateTime.UtcNow;
    //        }
    //    }

    //    await _context.SaveChangesAsync();
    //}

    public async Task<List<Product>> FetchAndStoreXeroProductsAsync(string type, int page, int pageSize)
    {
        // Remove all products where Platform = "Xero"
        var xeroProducts = _context.Products.Where(p => p.Platform == "Xero");
        _context.Products.RemoveRange(xeroProducts);
        await _context.SaveChangesAsync();

        var (accessToken, tenantId) = await GetXeroAuthDetailsAsync();

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("Xero-Tenant-Id", tenantId);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await client.GetAsync("https://api.xero.com/api.xro/2.0/Items");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var xeroData = JsonConvert.DeserializeObject<XeroItemsResponse>(content);

        var allProducts = new List<Product>();

        // Delete ONLY existing Xero records of the same type
        if (type == "Inventory" || type == "Service")
        {
            var existingXeroProducts = await _context.Products
                .Where(p => p.Platform == "Xero" && p.Type == type)
                .ToListAsync();

            _context.Products.RemoveRange(existingXeroProducts);
            await _context.SaveChangesAsync();
        }

        foreach (var item in xeroData.Items)
        {
            var isTracked = item.IsTrackedAsInventory;

            if ((type == "Inventory" && !isTracked) || (type == "Service" && isTracked))
                continue;

            var product = new Product
            {
                QuickBooksItemId = item.ItemID,
                Name = item.Name ?? "",
                Code = item.Code,
                Platform = "Xero",
                Description = item.Description ?? "",
                Type = isTracked ? "Inventory" : "Service",
                IsTrackedAsInventory = isTracked,

                QuantityOnHand = item.QuantityOnHand ?? 0,
                AsOfDate = DateTime.UtcNow,
                AssetAccount = item.InventoryAssetAccountCode,

                Price = isTracked
                    ? (item.SalesDetails?.UnitPrice ?? item.PurchaseDetails?.UnitPrice ?? 0)
                    : 0,

                IncomeAccount = isTracked ? item.SalesDetails?.AccountCode?.ToString() ?? "" : "",
                ExpenseAccount = isTracked ? item.PurchaseDetails?.COGSAccountCode?.ToString() ?? "" : "",
                PurchaseCOGSAccountCode = isTracked ? item.PurchaseDetails?.COGSAccountCode?.ToString() ?? "" : "",
                SalesAccountCode = isTracked ? item.SalesDetails?.AccountCode?.ToString() ?? "" : "",


                QuickBooksUserId = tenantId,
                UpdatedAt = DateTime.UtcNow,

                PurchaseUnitPrice = isTracked ? item.PurchaseDetails?.UnitPrice : 0,

                PurchaseTaxType = isTracked ? item.PurchaseDetails?.TaxType : "",

                SalesUnitPrice = isTracked ? item.SalesDetails?.UnitPrice : 0,

                SalesTaxType = isTracked ? item.SalesDetails?.TaxType : "",
            };

            var existing = await _context.Products
                .FirstOrDefaultAsync(p => p.QuickBooksItemId == product.QuickBooksItemId);

            if (existing != null)
            {
                existing.Name = product.Name;
                existing.Code = product.Code;
                existing.Description = product.Description;
                existing.Type = product.Type;
                existing.Price = product.Price;
                existing.IsTrackedAsInventory = product.IsTrackedAsInventory;
                existing.QuantityOnHand = product.QuantityOnHand;
                existing.AssetAccount = product.AssetAccount;
                existing.ExpenseAccount = product.ExpenseAccount;
                existing.IncomeAccount = product.IncomeAccount;
                existing.Platform = "Xero";

                existing.PurchaseUnitPrice = product.PurchaseUnitPrice ?? existing.PurchaseUnitPrice;
                existing.PurchaseCOGSAccountCode = product.PurchaseCOGSAccountCode ?? existing.PurchaseCOGSAccountCode;
                existing.PurchaseTaxType = product.PurchaseTaxType ?? existing.PurchaseTaxType;

                existing.SalesUnitPrice = product.SalesUnitPrice ?? existing.SalesUnitPrice;
                existing.SalesAccountCode = product.SalesAccountCode ?? existing.SalesAccountCode;
                existing.SalesTaxType = product.SalesTaxType ?? existing.SalesTaxType;

                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.Products.Add(product);
            }

            allProducts.Add(product);
        }

        await _context.SaveChangesAsync();

        return allProducts
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    public async Task AddProductsToXeroAndDbAsync(List<Product> products)
    {
        var (accessToken, tenantId) = await GetXeroAuthDetailsAsync();

        // Split products into inventory and service types
        var inventoryItems = products
     .Where(p => p.IsTrackedAsInventory)
     .Select(p =>
     {
         var item = new Dictionary<string, object?>
         {
             ["Code"] = p.Code,
             ["Name"] = p.Name,
             ["Description"] = p.Description,
             ["InventoryAssetAccountCode"] = p.AssetAccount
         };

         var purchase = new Dictionary<string, object?>();
         if (!string.IsNullOrWhiteSpace(p.PurchaseCOGSAccountCode))
             purchase["COGSAccountCode"] = p.PurchaseCOGSAccountCode;
         if (p.PurchaseUnitPrice.HasValue)
             purchase["UnitPrice"] = p.PurchaseUnitPrice.Value;
         if (purchase.Any())
             item["PurchaseDetails"] = purchase;

         var sales = new Dictionary<string, object?>();
         if (!string.IsNullOrWhiteSpace(p.SalesAccountCode))
             sales["AccountCode"] = p.SalesAccountCode;
         if (p.SalesUnitPrice.HasValue)
             sales["UnitPrice"] = p.SalesUnitPrice.Value;
         if (sales.Any())
             item["SalesDetails"] = sales;

         return item;
     }).ToList();


        var serviceItems = products
            .Where(p => !p.IsTrackedAsInventory)
            .Select(p => new
            {
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                isTrackedAsInventory = false
            }).ToList();

        // Send inventory items to Xero
        if (inventoryItems.Any())
        {
            await SendItemsToXeroAsync(inventoryItems, accessToken, tenantId);
        }

        // Send service items to Xero
        if (serviceItems.Any())
        {
            await SendItemsToXeroAsync(serviceItems, accessToken, tenantId);
        }

        // Save to local database
        foreach (var product in products)
        {
            product.Price ??= 0;
            product.Type = product.IsTrackedAsInventory ? "Inventory" : "Service";
            product.Platform = "Xero";
            product.CreatedAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;
            _context.Products.Add(product);
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Log and inspect the inner exception
            var innerMessage = ex.InnerException?.Message;
            throw new Exception($"Error saving changes: {innerMessage}", ex);
        }

    }

    private async Task SendItemsToXeroAsync(object itemsPayload, string accessToken, string tenantId)
    {
        var json = JsonConvert.SerializeObject(new { Items = itemsPayload });
        var request = new HttpRequestMessage(HttpMethod.Put, "https://api.xero.com/api.xro/2.0/Items")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Xero-Tenant-Id", tenantId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Xero API Error: {response.StatusCode}, Details: {error}");
        }
    }

    public async Task UpdateProductInXeroAndDbAsync(string itemId, Product product)
    {
        var (accessToken, tenantId) = await GetXeroAuthDetailsAsync();

        object itemPayload;

        if (product.IsTrackedAsInventory)
        {
            itemPayload = new
            {
                Code = product.Code,
                Name = product.Name,
                Description = product.Description,
                InventoryAssetAccountCode = product.AssetAccount,
                PurchaseDetails = new
                {
                    COGSAccountCode = product.PurchaseCOGSAccountCode,
                    UnitPrice = product.PurchaseUnitPrice ?? 0
                },
                SalesDetails = new
                {
                    UnitPrice = product.SalesUnitPrice ?? 0,
                    AccountCode = product.SalesAccountCode
                }
            };
        }
        else
        {
            itemPayload = new
            {
                Code = product.Code,
                Name = product.Name,
                Description = product.Description
            };
        }

        var json = JsonConvert.SerializeObject(new { Items = new[] { itemPayload } });

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.xero.com/api.xro/2.0/Items/{itemId}")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Xero-Tenant-Id", tenantId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to update item in Xero. Status: {response.StatusCode}, Error: {error}");
        }


        var existing = await _context.Products.FirstOrDefaultAsync(p => p.QuickBooksItemId == itemId);
        if (existing != null)
        {
            existing.Code = product.Code;
            existing.Name = product.Name;
            existing.Description = product.Description;
            existing.IsTrackedAsInventory = product.IsTrackedAsInventory;
            existing.AssetAccount = product.AssetAccount;
            existing.PurchaseCOGSAccountCode = product.PurchaseCOGSAccountCode;
            existing.PurchaseUnitPrice = product.PurchaseUnitPrice;
            existing.SalesUnitPrice = product.SalesUnitPrice;
            existing.SalesAccountCode = product.SalesAccountCode;
            existing.UpdatedAt = DateTime.UtcNow;

            _context.Products.Update(existing);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteProductFromXeroAndDbAsync(string itemId)
    {
        var (accessToken, tenantId) = await GetXeroAuthDetailsAsync();

        var request = new HttpRequestMessage(HttpMethod.Delete, $"https://api.xero.com/api.xro/2.0/Items/{itemId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("xero-tenant-id", tenantId);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to delete product from Xero. Status: {response.StatusCode}. Details: {errorContent}");
        }

        // Delete from local database if needed
        var localProduct = await _context.Products.FirstOrDefaultAsync(p => p.QuickBooksItemId == itemId);
        if (localProduct != null)
        {
            localProduct.IsActive = false; // Mark as inactive
            localProduct.UpdatedAt = DateTime.UtcNow; // Update the timestamp
            _context.Products.Update(localProduct);
            await _context.SaveChangesAsync();
        }
    }

}
