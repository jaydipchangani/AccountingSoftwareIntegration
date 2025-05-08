using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebApplication1.Data;
using WebApplication1.Models;
using Microsoft.Extensions.Http;
using System.Net.Http;
using DataLayer.Models;


namespace Businesslayer.Services
{
    public class AccountService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AccountService(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<List<Account>> SyncAccountsAsync()
        {
            var token = await _context.QuickBooksTokens
                .OrderByDescending(q => q.Id)
                .Where(q => q.Company == "QBO")
                .FirstOrDefaultAsync();

            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken) || string.IsNullOrWhiteSpace(token.RealmId))
                throw new Exception("QuickBooks credentials not found.");

            var baseUrl = _configuration["QuickBooks:BaseUrl"];
            var apiUrl = $"{baseUrl}/{token.RealmId}/query?query=select * from Account&minorversion=75";

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await httpClient.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var jsonDoc = await JsonDocument.ParseAsync(contentStream);

            if (!jsonDoc.RootElement.TryGetProperty("QueryResponse", out var queryResponse) ||
                !queryResponse.TryGetProperty("Account", out var accountsJson))
                return new List<Account>();


            _context.Accounts.RemoveRange(_context.Accounts);
            await _context.SaveChangesAsync();

 
            var accountsToAdd = new List<Account>();

            foreach (var acc in accountsJson.EnumerateArray())
            {
                accountsToAdd.Add(new Account
                {
                    QbId = acc.GetProperty("Id").GetString(),
                    Name = acc.GetProperty("Name").GetString(),
                    AccountType = acc.TryGetProperty("AccountType", out var accType) ? accType.GetString() : null,
                    AccountSubType = acc.TryGetProperty("AccountSubType", out var subType) ? subType.GetString() : null
                });
            }


            if (accountsToAdd.Any())
            {
                await _context.Accounts.AddRangeAsync(accountsToAdd);
                await _context.SaveChangesAsync();
            }

            return accountsToAdd;
        }


    }
}
