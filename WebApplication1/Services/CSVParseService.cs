using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Globalization;
using System.Net.Http;
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


    public async Task<List<SyncResult>> SyncCustomersAsync()
    {
        var results = new List<SyncResult>();
        var rows = await _context.CSVParses.ToListAsync();
        if (rows == null || rows.Count == 0) return results;

        var httpClient = _httpClientFactory.CreateClient();
        var fetchResponse = await httpClient.GetAsync("https://localhost:7241/api/Customer/fetch-customers-from-quickbooks");
        if (!fetchResponse.IsSuccessStatusCode)
        {
            throw new Exception("Failed to fetch customers from QuickBooks.");
        }

        var customers = await _context.Customers.ToListAsync();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.CustomerName)) continue;

            var result = new SyncResult { Identifier = row.CustomerName };
            try
            {
                var existingCustomer = customers.FirstOrDefault(c =>
                    c.DisplayName?.Trim().ToLower() == row.CustomerName.Trim().ToLower());

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
                };

                var json = JsonConvert.SerializeObject(customerPayload);
                var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                if (existingCustomer != null)
                {
                    response = await httpClient.PutAsync($"https://localhost:7241/api/Customer/update-customer/{existingCustomer.Id}", jsonContent);
                }
                else
                {
                    response = await httpClient.PostAsync("https://localhost:7241/api/Customer/add-customer", jsonContent);
                }

                result.Success = response.IsSuccessStatusCode;
                result.Message = result.Success ? "Synced successfully." : await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Exception: {ex.Message}";
            }

            results.Add(result);
        }

        return results;
    }



    public async Task SyncProductsAsync()
    {
        var rows = await _context.CSVParses.ToListAsync();
        if (rows == null || rows.Count == 0) return;

        var httpClient = _httpClientFactory.CreateClient();

        // Step 2: Call the QuickBooks item fetch API
        var fetchResponse = await httpClient.GetAsync("https://localhost:7241/api/Products/fetch-items-from-quickbooks");
        fetchResponse.EnsureSuccessStatusCode();

        // Step 3: Load local products
        var products = await _context.Products.ToListAsync();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.ItemName)) continue;

            var existingProduct = products
                .FirstOrDefault(p => p.Name?.Trim().ToLower() == row.ItemName.Trim().ToLower());

            if (existingProduct != null)
            {
                // Step 4: Prepare update payload
                var updatePayload = new
                {
                    id = existingProduct.Id.ToString(),
                    name = row.ItemName,
                    description = row.ItemDescription,
                    type = _configuration["ProductDefaults:Type"],
                    IncomeAccountId = _configuration["ProductDefaults:IncomeAccountId"],
                    AssetAccountId = _configuration["ProductDefaults:AssetAccountId"],
                    ExpenseAccountId = _configuration["ProductDefaults:ExpenseAccountId"],
                    IncomeAccount = _configuration["ProductDefaults:IncomeAccount"],
                    AssetAccount = _configuration["ProductDefaults:AssetAccount"],
                    ExpenseAccount = _configuration["ProductDefaults:ExpenseAccount"]
                };

                var json = JsonConvert.SerializeObject(updatePayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var updateUrl = $"https://localhost:7241/api/Products/edit-product/{existingProduct.QuickBooksItemId}";
                var updateResponse = await httpClient.PutAsync(updateUrl, content);
                updateResponse.EnsureSuccessStatusCode();
            }
            else
            {
                // Step 5: Add new product
                var addPayload = new
                {
                    name = row.ItemName,
                    description = row.ItemDescription,
                    unitPrice = row.Rate,
                    type = _configuration["ProductDefaults:Type"],
                    IncomeAccountId = _configuration["ProductDefaults:IncomeAccountId"],
                    AssetAccountId = _configuration["ProductDefaults:AssetAccountId"],
                    ExpenseAccountId = _configuration["ProductDefaults:ExpenseAccountId"],
                    IncomeAccount = _configuration["ProductDefaults:IncomeAccount"],
                    AssetAccount = _configuration["ProductDefaults:AssetAccount"],
                    ExpenseAccount = _configuration["ProductDefaults:ExpenseAccount"]
                }
            ;

                var json = JsonConvert.SerializeObject(addPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var addUrl = $"https://localhost:7241/api/Products/add-product";
                var addResponse = await httpClient.PostAsync(addUrl, content);
                addResponse.EnsureSuccessStatusCode();
            }
        }
    }



    public async Task SyncInvoicesAsync()
    {
        var rows = await _context.CSVParses.ToListAsync();
        if (rows == null || rows.Count == 0) return;

        var httpClient = _httpClientFactory.CreateClient();

        // Sync all necessary data first
        await httpClient.GetAsync("https://localhost:7241/api/Customer/fetch-customers-from-quickbooks");
        await httpClient.GetAsync("https://localhost:7241/api/Products/fetch-items-from-quickbooks");
        await httpClient.GetAsync("https://localhost:7241/api/InvoiceContoller/sync-invoices");

        var customers = await _context.Customers.ToListAsync();
        var products = await _context.Products.ToListAsync();
        var localInvoices = await _context.Invoices.ToListAsync(); // assuming this exists

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.CustomerName) || string.IsNullOrWhiteSpace(row.ItemName)) continue;

            var customer = customers.FirstOrDefault(c => c.DisplayName?.Trim().ToLower() == row.CustomerName.Trim().ToLower());
            var product = products.FirstOrDefault(p => p.Name?.Trim().ToLower() == row.ItemName.Trim().ToLower());

            if (customer == null || product == null) continue;

            var existingInvoice = localInvoices.FirstOrDefault(inv => inv.DocNumber == row.InvoiceNumber);

            var invoicePayload = new
            {
                customerId = customer.QuickBooksCustomerId.ToString(), 
                invoiceNumber = row.InvoiceNumber,
                date = DateTime.UtcNow,
                dueDate = DateTime.UtcNow.AddDays(7),
                lineItems = new[]
                {
            new
            {
                id = product.Id.ToString(),
                itemId = product.Id.ToString(),
                description = row.ItemDescription ?? product.Description ?? "No description",
                quantity = row.Quantity,
                unitPrice = row.Rate,
                amount = row.Quantity * row.Rate
            }
        },
                notes = "Imported from CSV"
            };

            var json = JsonConvert.SerializeObject(invoicePayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            if (existingInvoice != null)
            {
                // Update invoice in DB & QBO
                var updateInvoiceUrl = $"https://localhost:7241/api/InvoiceContoller/update-invoice/{existingInvoice.QuickBooksId}";
                var updateResponse = await httpClient.PutAsync(updateInvoiceUrl, content);

                if (!updateResponse.IsSuccessStatusCode)
                {
                    var body = await updateResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ Failed to update invoice {existingInvoice.Id}: {body}");
                    continue;
                }
            }
            else
            {
                // Add new invoice to DB & QBO
                var addInvoiceUrl = $"https://localhost:7241/api/InvoiceContoller/add-invoice";
                var addResponse = await httpClient.PostAsync(addInvoiceUrl, content);

                if (!addResponse.IsSuccessStatusCode)
                {
                    var body = await addResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ Failed to add invoice: {body}");
                    continue;
                }
            }
        }


    }

}
