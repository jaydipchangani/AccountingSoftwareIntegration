using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using WebApplication1.Data;
using WebApplication1.Models;
using static System.Net.WebRequestMethods;

public class CSVParseService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public CSVParseService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<(bool Success, List<string> Errors)> ParseAndSaveAsync(IFormFile file)
    {
        var errors = new List<string>();

        var qbToken = await _context.QuickBooksTokens
            .OrderByDescending(q => q.Id)
            .FirstOrDefaultAsync();

        if (qbToken == null || string.IsNullOrWhiteSpace(qbToken.AccessToken) || string.IsNullOrWhiteSpace(qbToken.RealmId))
        {
            throw new Exception("QuickBooks credentials not found.");
        }

        var baseUrl = _configuration["QuickBooks:BaseUrl"];
        var apiUrl = $"{baseUrl}/{qbToken.RealmId}/query?query=select * from vendor&minorversion=75";

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", qbToken.AccessToken);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var response = await httpClient.GetAsync(apiUrl);
        response.EnsureSuccessStatusCode();

        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null
        });

        var records = csv.GetRecords<CSVParse>().ToList();

        var invoiceSet = new HashSet<string>();

        for (int i = 0; i < records.Count; i++)
        {
            var row = records[i];
            int rowNum = i + 2;

            // Check required fields
            if (string.IsNullOrWhiteSpace(row.InvoiceNumber)) errors.Add($"Row {rowNum}: InvoiceNumber is required.");
            if (string.IsNullOrWhiteSpace(row.CustomerName)) errors.Add($"Row {rowNum}: CustomerName is required.");
            if (string.IsNullOrWhiteSpace(row.CustomerEmail)) errors.Add($"Row {rowNum}: CustomerEmail is required.");
            if (string.IsNullOrWhiteSpace(row.ItemName)) errors.Add($"Row {rowNum}: ItemName is required.");
            if (row.InvoiceDate == default) errors.Add($"Row {rowNum}: Invalid InvoiceDate.");
            if (row.DueDate == default) errors.Add($"Row {rowNum}: Invalid DueDate.");
            if (row.Quantity <= 0) errors.Add($"Row {rowNum}: Quantity must be greater than 0.");
            if (row.Rate < 0) errors.Add($"Row {rowNum}: Rate cannot be negative.");

            // Check duplicates within file
            if (!string.IsNullOrWhiteSpace(row.InvoiceNumber))
            {
                if (!invoiceSet.Add(row.InvoiceNumber))
                {
                    errors.Add($"Row {rowNum}: Duplicate InvoiceNumber '{row.InvoiceNumber}' in file.");
                }
            }
        }

        if (errors.Any())
        {
            return (false, errors);
        }

        _context.CSVParses.RemoveRange(_context.CSVParses);
        _context.CSVParses.AddRange(records);
        await _context.SaveChangesAsync();

        return (true, new List<string>());
    }


    public async Task SyncCustomersAsync()
    {
        var rows = await _context.CSVParses.ToListAsync();
        if (rows == null || rows.Count == 0) return;

        var httpClient = _httpClientFactory.CreateClient();

        // Fetch latest customers
        var fetchResponse = await httpClient.GetAsync("https://localhost:7241/api/Customer/fetch-customers-from-quickbooks");
        if (!fetchResponse.IsSuccessStatusCode)
        {
            var error = await fetchResponse.Content.ReadAsStringAsync();
            throw new Exception($"Failed to fetch customers. Status: {fetchResponse.StatusCode}, Error: {error}");
        }


        // 2. Get local customer list
        var customers = await _context.Customers.ToListAsync();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.CustomerName)) continue;

            var existingCustomer = customers
                .FirstOrDefault(c => c.DisplayName?.Trim().ToLower() == row.CustomerName.Trim().ToLower());

            var customerPayload = new
            {
                displayName = row.CustomerName,
                companyName = row.CustomerName,
                email = row.CustomerEmail,
                phone = "string",
                billingLine1 = "string",
                billingCity = "string",
                billingState = "string",
                billingPostalCode = "string",
                billingCountry = "string"
            }
        ;

            var json = JsonConvert.SerializeObject(customerPayload);
            var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
            if (existingCustomer != null)
            {
                var updateUrl = $"https://localhost:7241/api/Customer/update-customer/{existingCustomer.Id}";
                var updateResponse = await httpClient.PutAsync(updateUrl, jsonContent);
                if (!updateResponse.IsSuccessStatusCode)
                {
                    var error = await updateResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to update customer {row.CustomerName}. Status: {updateResponse.StatusCode}, Error: {error}");
                }
            }
            else
            {
                var addUrl = "https://localhost:7241/api/Customer/add-customer";
                var addResponse = await httpClient.PostAsync(addUrl, jsonContent);
                if (!addResponse.IsSuccessStatusCode)
                {
                    var error = await addResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to add customer {row.CustomerName}. Status: {addResponse.StatusCode}, Error: {error}");
                }
            }

        }
    }


    public async Task SyncProductsAsync()
    {
        var rows = await _context.CSVParses.ToListAsync();
        if (rows == null || rows.Count == 0) return;

        var httpClient = _httpClientFactory.CreateClient();

        // 1. Fetch latest products from QuickBooks into local DB
        var fetchResponse = await httpClient.GetAsync("https://localhost:7241/api/Products/fetch-items-from-quickbooks");
        fetchResponse.EnsureSuccessStatusCode();

        // 2. Get local product list
        var localProducts = await _context.Products.ToListAsync();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.ItemName)) continue;

            var existingProduct = localProducts
                .FirstOrDefault(p => p.Name?.Trim().ToLower() == row.ItemName.Trim().ToLower());

            var productPayload = new
            {
                Name = row.ItemName,
                Description = row.ItemDescription,
                Type= "Service",
                IncomeAccountId= "54",
                AssetAccountId= "81",
                ExpenseAccountId= "80",
                IncomeAccount= "Sales of Product Income",
                AssetAccount= "Inventory Asset",
                ExpenseAccount= "Cost of Goods Sold"

            }
        ;

            var json = JsonConvert.SerializeObject(productPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            if (existingProduct != null)
            {
                var updateUrl = $"https://localhost:7241/api/Products/edit-product/{existingProduct.Id}";
                var updateResponse = await httpClient.PutAsync(updateUrl, content);
                updateResponse.EnsureSuccessStatusCode();
            }
            else
            {
                var addUrl = "https://localhost:7241/api/Products/add-product";
                var addResponse = await httpClient.PostAsync(addUrl, content);
                addResponse.EnsureSuccessStatusCode();
            }
        }
    }


}
