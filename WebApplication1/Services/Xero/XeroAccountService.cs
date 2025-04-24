using System.Net.Http.Headers;
using System.Text.Json;
using WebApplication1.Models;
using WebApplication1.Data;

public class XeroAccountService
{
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _db;

    public XeroAccountService(HttpClient httpClient, ApplicationDbContext db)
    {
        _httpClient = httpClient;
        _db = db;
    }

    public async Task<List<ChartOfAccount>> FetchAccountsAsync(string accessToken, string tenantId)
    {
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
                CurrentBalance = null, // Xero doesn't return this; you may calculate if needed
                QuickBooksUserId = tenantId,
                Company = "Xero",
                CurrencyValue = "USD", // You can adjust this meaningfully
                CurrencyName = "US Dollar",
                CreatedAt = DateTime.UtcNow
            };

            accounts.Add(chart);
        }

        // Optionally clear old Xero accounts before saving
        _db.ChartOfAccounts.RemoveRange(_db.ChartOfAccounts.Where(c => c.QuickBooksUserId == tenantId && c.CurrencyValue == "Xero"));
        _db.ChartOfAccounts.AddRange(accounts);
        await _db.SaveChangesAsync();

        return accounts;
    }
}
