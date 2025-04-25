using System.Net.Http.Headers;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuickBooksController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<AuthController> _logger;
        public QuickBooksController(ApplicationDbContext dbContext, ILogger<AuthController> logger)
        {
            _httpClient = new HttpClient();
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("fetch-from-db")]
        public async Task<IActionResult> FetchChartOfAccountsFromDb(
    int page = 1,
    int pageSize = 10,
    string? searchTerm = null,
    string? company = null)
        {
            // Check for at least one valid QuickBooks or Xero token
            var hasQuickBooksToken = await _dbContext.QuickBooksTokens.AnyAsync();
            var hasXeroToken = await _dbContext.XeroTokens.AnyAsync();

            if (!hasQuickBooksToken && !hasXeroToken)
            {
                return Unauthorized("No active QuickBooks or Xero token found. Please authenticate first.");
            }

            var query = _dbContext.ChartOfAccounts.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(a => a.Name.Contains(searchTerm));

            if (!string.IsNullOrEmpty(company))
                query = query.Where(a => a.Company == company);

            var totalRecords = await query.CountAsync();
            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                totalRecords,
                currentPage = page,
                pageSize,
                data
            });
        }


        [HttpGet("fetch-chart-of-accounts-from-quickbooks")]
        public async Task<IActionResult> FetchChartOfAccountsFromQuickBooks()
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

                _logger.LogInformation("Fetching Chart of Accounts from QuickBooks API.");

                var url = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/query?query=SELECT * FROM Account";

                var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(httpRequest);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, json);

                var accounts = new List<ChartOfAccount>();

                var jsonDoc = JsonDocument.Parse(json);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("QueryResponse", out var queryResponse) &&
                    queryResponse.TryGetProperty("Account", out var accountArray))
                {
                    foreach (var account in accountArray.EnumerateArray())
                    {
                        string id = account.GetProperty("Id").ValueKind == JsonValueKind.Number
                            ? account.GetProperty("Id").GetInt32().ToString()
                            : account.GetProperty("Id").GetString();

                        var currencyValue = account.TryGetProperty("CurrencyRef", out var currencyRef) &&
                                            currencyRef.TryGetProperty("value", out var curValue)
                                            ? curValue.GetString()
                                            : "USD";

                        var currencyName = account.TryGetProperty("CurrencyRef", out var currencyRef2) &&
                                           currencyRef2.TryGetProperty("name", out var curName)
                                           ? curName.GetString()
                                           : null;

                        var balance = account.TryGetProperty("CurrentBalance", out var balanceProp) &&
                                      balanceProp.ValueKind == JsonValueKind.Number
                            ? balanceProp.GetDecimal()
                            : 0.0m;

                        var newAccount = new ChartOfAccount
                        {
                            QuickBooksAccountId = id,
                            Name = account.GetProperty("Name").GetString(),
                            AccountType = account.GetProperty("AccountType").GetString(),
                            AccountSubType = account.GetProperty("AccountSubType").GetString(),
                            Classification = account.TryGetProperty("Classification", out var classProp) ? classProp.GetString() : null,
                            CurrentBalance = balance,
                            CurrencyValue = currencyValue,
                            CurrencyName = currencyName,
                            QuickBooksUserId = tokenRecord.QuickBooksUserId,
                            CreatedAt = DateTime.UtcNow,
                            Company = "QBO"
                        };

                        accounts.Add(newAccount);
                    }
                }

                var strategy = _dbContext.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                    try
                    {
                       

                        _dbContext.ChartOfAccounts.RemoveRange(_dbContext.ChartOfAccounts.Where(c => c.Company == "QBO"));
                      

                        await _dbContext.ChartOfAccounts.AddRangeAsync(accounts);
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

                return Ok(accounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Chart of Accounts.");
                return StatusCode(500, $"Error fetching accounts from QuickBooks: {ex.Message}");
            }
        }



        private List<ChartOfAccount> ParseAccountData(string json, string quickBooksUserId)
        {
            var result = new List<ChartOfAccount>();

            using var document = JsonDocument.Parse(json);

            var accounts = document.RootElement
                .GetProperty("QueryResponse")
                .GetProperty("Account")
                .EnumerateArray();

            foreach (var account in accounts)
            {
                var currencyRef = account.TryGetProperty("CurrencyRef", out var currencyRefElement)
                    ? currencyRefElement.GetProperty("value").GetString()
                    : "USD"; // Set a default value or fallback

                var chartOfAccount = new ChartOfAccount
                {
                    QuickBooksAccountId = account.GetProperty("Id").GetString(),
                    Name = account.GetProperty("Name").GetString(),
                    AccountType = account.TryGetProperty("AccountType", out var type) ? type.GetString() : null,
                    AccountSubType = account.TryGetProperty("AccountSubType", out var subType) ? subType.GetString() : null,
                    //Description = account.TryGetProperty("Description", out var desc) ? desc.GetString() : null,
                    //Active = account.TryGetProperty("Active", out var active) && active.GetBoolean(),
                    Classification = account.TryGetProperty("Classification", out var classification) ? classification.GetString() : null,
                    QuickBooksUserId = quickBooksUserId,
                    CurrencyValue = currencyRef // ✅ Ensure it's never null
                };

                result.Add(chartOfAccount);
            }

            return result;
        }


        private string GetJsonPropertyValue(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out JsonElement property) &&
                property.ValueKind != JsonValueKind.Null)
            {
                return property.GetString();
            }

            return null;
        }


        private async Task<List<Account>> FetchAccountsFromQuickBooks(string accessToken, string realmId, string accountType)
        {
            var query = $"SELECT * FROM Account WHERE AccountType = '{accountType}'";
            var url = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/query?query={Uri.EscapeDataString(query)}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new ApplicationException($"QuickBooks API error: {json}");

            var parsed = JsonConvert.DeserializeObject<QuickBooksResponse>(json);
            return parsed?.QueryResponse?.Account?.Select(a => new Account
            {
                QbId = a.Id,
                Name = a.Name,
                AccountType = a.AccountType,
                AccountSubType = a.AccountSubType
            }).ToList() ?? new List<Account>();
        }

        private async Task SaveAccountsToDatabase(List<Account> accounts)
        {
            foreach (var account in accounts)
            {
                if (!_dbContext.Accounts.Any(a => a.QbId == account.QbId))
                    _dbContext.Accounts.Add(account);
            }

            await _dbContext.SaveChangesAsync();
        }

        [HttpGet("sync-income-accounts")]
        public async Task<IActionResult> SyncIncomeAccounts(
    [FromHeader(Name = "accessToken")] string accessToken,
    [FromHeader(Name = "realmId")] string realmId)
        {
            var accounts = await FetchAccountsFromQuickBooks(accessToken, realmId, "Income");
            await SaveAccountsToDatabase(accounts);
            return Ok(accounts);
        }

        [HttpGet("sync-expense-accounts")]
        public async Task<IActionResult> SyncExpenseAccounts(
    [FromHeader(Name = "accessToken")] string accessToken,
    [FromHeader(Name = "realmId")] string realmId)
        {
            var accounts = await FetchAccountsFromQuickBooks(accessToken, realmId, "Cost of Goods Sold");
            await SaveAccountsToDatabase(accounts);
            return Ok(accounts);
        }

        [HttpGet("sync-asset-accounts")]
        public async Task<IActionResult> SyncAssetAccounts(
            [FromHeader(Name = "accessToken")] string accessToken,
            [FromHeader(Name = "realmId")] string realmId)
        {
            var accounts = await FetchAccountsFromQuickBooks(accessToken, realmId, "Other Current Asset");
            await SaveAccountsToDatabase(accounts);
            return Ok(accounts);
        }

        private string GetAccessTokenFromHeader()
        {
            if (!Request.Headers.TryGetValue("quickbooks_accessToken", out var accessToken) || string.IsNullOrEmpty(accessToken))
                throw new InvalidOperationException("Access token is missing in the request header.");

            return accessToken;
        }

        private string GetRealmIdFromHeader()
        {
            if (!Request.Headers.TryGetValue("quickbooks_realmId", out var realmId) || string.IsNullOrEmpty(realmId))
                throw new InvalidOperationException("Realm ID is missing in the request header.");

            return realmId;
        }

    }

}
