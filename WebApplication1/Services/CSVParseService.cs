using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using WebApplication1.Data;
using WebApplication1.Models;

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

        // 🔐 Authenticate QuickBooks
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

        // 📄 Read CSV
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null
        });

        var records = csv.GetRecords<CSVParse>().ToList();

        var invoiceSet = new HashSet<string>(); // for duplicate check within CSV

        for (int i = 0; i < records.Count; i++)
        {
            var row = records[i];
            int rowNum = i + 2; // 1-based + header

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

        // If any validation errors, return without saving
        if (errors.Any())
        {
            return (false, errors);
        }

        // 🧹 Clear and insert new data
        _context.CSVParses.RemoveRange(_context.CSVParses);
        _context.CSVParses.AddRange(records);
        await _context.SaveChangesAsync();

        return (true, new List<string>());
    }
}
