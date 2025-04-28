using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using XeroIntegration.Data;
using XeroIntegration.Models;

public class XeroAccountService
{
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _db;

    public XeroAccountService(HttpClient httpClient, ApplicationDbContext db)
    {
        _httpClient = httpClient;
        _db = db;
    }

    public async Task<List<ChartOfAccount>> FetchAccountsFromXeroAsync()
    {
        // Fetch the latest valid Xero token where Company is "Xero"
        var token = await _db.QuickBooksTokens
            .Where(t => t.Company == "Xero")
            .OrderByDescending(t => t.CreatedAtUtc) // assuming latest token is most valid
            .FirstOrDefaultAsync();

        if (token == null || string.IsNullOrEmpty(token.AccessToken) || string.IsNullOrEmpty(token.TenantId))
        {
            throw new ApplicationException("Xero access token or tenant ID not found.");
        }

        var accessToken = token.AccessToken;
        var tenantId = token.TenantId;

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.xero.com/api.xro/2.0/Accounts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Xero-Tenant-Id", tenantId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new ApplicationException($"Xero accounts fetch failed: {content}");
        }

        var jsonDoc = JsonDocument.Parse(content);
        var accounts = new List<ChartOfAccount>();

        foreach (var acc in jsonDoc.RootElement.GetProperty("Accounts").EnumerateArray())
        {
            var chart = new ChartOfAccount
            {
                QuickBooksAccountId = acc.GetProperty("AccountID").GetString(),
                Name = acc.GetProperty("Name").GetString(),
                AccountType = acc.GetProperty("Type").GetString(),
                AccountSubType = acc.TryGetProperty("BankAccountType", out var subtype) ? subtype.GetString() : null,
                Classification = acc.TryGetProperty("Class", out var cls) ? cls.GetString() : null,
                CurrentBalance = 0,
                QuickBooksUserId = tenantId,
                Company = "Xero",
                CurrencyValue = "USD",
                CurrencyName = "US Dollar",
                CreatedAt = DateTime.UtcNow
            };

            accounts.Add(chart);
        }

        // Remove old Xero accounts before inserting new ones
        _db.ChartOfAccounts.RemoveRange(
            _db.ChartOfAccounts.Where(c => c.QuickBooksUserId == tenantId && c.Company == "Xero")
        );

        _db.ChartOfAccounts.AddRange(accounts);
        await _db.SaveChangesAsync();

        return accounts;

    }

}
