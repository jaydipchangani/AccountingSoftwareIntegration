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

    public async Task ParseAndSaveAsync(IFormFile file)
    {
       
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
        response.EnsureSuccessStatusCode(); // This will throw if the token is invalid or expired

  
        if (file == null || file.Length == 0)
            throw new ArgumentException("CSV file is empty.");

        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null
        });

        var newRecords = csv.GetRecords<CSVParse>().ToList();

        _context.CSVParses.RemoveRange(_context.CSVParses);
        _context.CSVParses.AddRange(newRecords);
        await _context.SaveChangesAsync();
    }
}
