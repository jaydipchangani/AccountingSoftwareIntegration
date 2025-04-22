using System.Net.Http.Headers;
using System.Text.Json;
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

        [HttpGet("fetch-from-db-paginated")]
        public async Task<IActionResult> FetchChartOfAccountsFromDbPaginated(
        int page = 1,
        int pageSize = 10,
        string? searchTerm = null)
        {
            try
            {
                var tokenRecord = await _dbContext.QuickBooksTokens
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (tokenRecord == null)
                    return NotFound("No QuickBooks token found.");

                var query = _dbContext.ChartOfAccounts
                    .Where(c => c.QuickBooksUserId == tokenRecord.QuickBooksUserId);

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    string likeTerm = $"%{searchTerm}%";
                    query = query.Where(c =>
                        EF.Functions.Like(c.Name, likeTerm) ||
                        EF.Functions.Like(c.AccountType, likeTerm) ||
                        (c.AccountSubType != null && EF.Functions.Like(c.AccountSubType, likeTerm)) ||
                        (c.Classification != null && EF.Functions.Like(c.Classification, likeTerm))
                    );
                }

                var totalRecords = await query.CountAsync();

                var pagedData = await query
                    .OrderBy(c => c.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var response = new
                {
                    TotalRecords = totalRecords,
                    TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
                    CurrentPage = page,
                    PageSize = pageSize,
                    Data = pagedData
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching paginated accounts: {ex.Message}");
            }
        }


        [HttpGet("fetch-from-quickbooks")]
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

                _logger.LogInformation("Fetching data from QuickBooks API.");

                var url = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/query?query=SELECT * FROM Account";
                var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(httpRequest);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, json);

                var parsedAccounts = ParseAccountData(json, tokenRecord.QuickBooksUserId);

                using (var transaction = await _dbContext.Database.BeginTransactionAsync())
                {
                    try
                    {
                        foreach (var account in parsedAccounts)
                        {
                            // Check if the account already exists by QuickBooksAccountId
                            var existingAccount = await _dbContext.ChartOfAccounts
                                .FirstOrDefaultAsync(c => c.QuickBooksAccountId == account.QuickBooksAccountId);

                            if (existingAccount != null)
                            {
                                // Update the existing account
                                _dbContext.Entry(existingAccount).CurrentValues.SetValues(account);
                            }
                            else
                            {
                                // Insert a new account if it doesn't exist
                                await _dbContext.ChartOfAccounts.AddAsync(account);
                            }
                        }

                        // Save the changes
                        await _dbContext.SaveChangesAsync();

                        // Commit the transaction
                        await transaction.CommitAsync();

                        return Ok(parsedAccounts);
                    }
                    catch (Exception ex)
                    {
                        // Rollback if an error occurs
                        await transaction.RollbackAsync();
                        return StatusCode(500, $"Error processing QuickBooks data: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching accounts from QuickBooks: {ex.Message}");
            }
        }

        private List<ChartOfAccount> ParseAccountData(string json, string quickBooksUserId)
        {
            var result = new List<ChartOfAccount>();

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                var root = document.RootElement;

                if (root.TryGetProperty("QueryResponse", out JsonElement queryResponse) &&
                    queryResponse.TryGetProperty("Account", out JsonElement accounts))
                {
                    foreach (JsonElement acc in accounts.EnumerateArray())
                    {
                        var account = new ChartOfAccount
                        {
                            QuickBooksAccountId = GetJsonPropertyValue(acc, "Id"),
                            Name = GetJsonPropertyValue(acc, "Name"),
                            AccountType = GetJsonPropertyValue(acc, "AccountType"),
                            AccountSubType = GetJsonPropertyValue(acc, "AccountSubType"),
                            Classification = GetJsonPropertyValue(acc, "Classification"),
                            QuickBooksUserId = quickBooksUserId,
                            CreatedAt = DateTime.UtcNow
                        };

                        // Handle nullable decimal
                        if (acc.TryGetProperty("CurrentBalance", out JsonElement balanceElement) &&
                            balanceElement.ValueKind != JsonValueKind.Null)
                        {
                            account.CurrentBalance = balanceElement.GetDecimal();
                        }

                        // Handle CurrencyRef
                        if (acc.TryGetProperty("CurrencyRef", out JsonElement currencyRef))
                        {
                            //account.CurrencyRef = new CurrencyRef
                            //{
                            //    Value = GetJsonPropertyValue(currencyRef, "value"),
                            //    Name = GetJsonPropertyValue(currencyRef, "name")
                            //};

                            // Update the flattened properties
                            account.UpdateCurrencyRefProperties();
                        }

                        result.Add(account);
                    }
                }
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
