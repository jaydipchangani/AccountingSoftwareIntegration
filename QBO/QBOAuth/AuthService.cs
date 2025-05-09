using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebApplication1.Data;
using WebApplication1.Models;


namespace QBO.QBOAuth
{


    public class AuthService : IAuthService
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<AuthService> _logger;
        private readonly string _quickBooksBaseUrl;

        public AuthService(IConfiguration config, ApplicationDbContext dbContext, ILogger<AuthService> logger)
        {
            _config = config;
            _dbContext = dbContext;
            _logger = logger;
            // Get the QuickBooks base URL from the appsettings.json
            _quickBooksBaseUrl = _config["QuickBooks:BaseUrl"];
        }

        public IActionResult GenerateLoginRedirect()
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["client_id"] = _config["QuickBooks:ClientId"];
            query["redirect_uri"] = _config["QuickBooks:RedirectUri"];
            query["response_type"] = "code";
            query["scope"] = "com.intuit.quickbooks.accounting openid profile email phone address";
            query["state"] = Guid.NewGuid().ToString();

            var authUrl = $"https://appcenter.intuit.com/connect/oauth2?{query}";
            return new RedirectResult(authUrl);
        }

        public async Task<IActionResult> ExchangeCodeAsync(ExchangeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.RealmId))
                return new BadRequestObjectResult("Code or RealmId is missing.");

            try
            {
                var clientId = _config["QuickBooks:ClientId"];
                var clientSecret = _config["QuickBooks:ClientSecret"];
                var redirectUri = _config["QuickBooks:RedirectUri"];
                var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

                var postData = new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", request.Code },
                    { "redirect_uri", redirectUri }
                };

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer")
                {
                    Headers = { { "Authorization", $"Basic {authHeader}" } },
                    Content = new FormUrlEncodedContent(postData)
                };

                var httpClient = new HttpClient();
                var response = await httpClient.SendAsync(requestMessage);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return new BadRequestObjectResult("Token exchange failed: " + content);

                var tokenData = JsonSerializer.Deserialize<TokenResponse>(content);
                if (tokenData == null)
                    return new StatusCodeResult(500);

                var userId = ExtractSubClaimFromIdToken(tokenData.IdToken);
                var existingToken = await _dbContext.QuickBooksTokens.FirstOrDefaultAsync(t => t.Company == "QBO");

                if (existingToken != null)
                {
                    existingToken.QuickBooksUserId = userId;
                    existingToken.RealmId = request.RealmId;
                    existingToken.AccessToken = tokenData.AccessToken;
                    existingToken.RefreshToken = tokenData.RefreshToken;
                    existingToken.IdToken = tokenData.IdToken;
                    existingToken.TokenType = tokenData.TokenType;
                    existingToken.ExpiresIn = tokenData.ExpiresIn;
                    existingToken.XRefreshTokenExpiresIn = tokenData.XRefreshTokenExpiresIn;
                }
                else
                {
                    var token = new QuickBooksToken
                    {
                        QuickBooksUserId = userId,
                        RealmId = request.RealmId,
                        AccessToken = tokenData.AccessToken,
                        RefreshToken = tokenData.RefreshToken,
                        IdToken = tokenData.IdToken,
                        TokenType = tokenData.TokenType,
                        ExpiresIn = tokenData.ExpiresIn,
                        XRefreshTokenExpiresIn = tokenData.XRefreshTokenExpiresIn,
                        CreatedAt = DateTime.UtcNow,
                        Company = "QBO"
                    };
                    _dbContext.QuickBooksTokens.Add(token);
                }

                await _dbContext.SaveChangesAsync();

                return new OkObjectResult(new
                {
                    message = "Token saved/updated successfully",
                    token = new
                    {
                        accessToken = tokenData.AccessToken,
                        refreshToken = tokenData.RefreshToken,
                        idToken = tokenData.IdToken,
                        realmId = request.RealmId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token exchange.");
                return new StatusCodeResult(500);
            }
        }

        public async Task<IActionResult> LogoutAsync()
        {
            try
            {
                var qboTokens = await _dbContext.QuickBooksTokens.Where(t => t.Company == "QBO").ToListAsync();
                if (qboTokens.Any())
                {
                    var userIds = qboTokens.Select(t => t.QuickBooksUserId).ToList();
                    var accounts = await _dbContext.ChartOfAccounts
                        .Where(a => userIds.Contains(a.QuickBooksUserId) && a.Company == "QBO")
                        .ToListAsync();
                    var customers = await _dbContext.Customers
                        .Where(c => userIds.Contains(c.QuickBooksUserId))
                        .ToListAsync();

                    _dbContext.ChartOfAccounts.RemoveRange(accounts);
                    _dbContext.Customers.RemoveRange(customers);
                    _dbContext.QuickBooksTokens.RemoveRange(qboTokens);
                }

                var xeroTokens = await _dbContext.QuickBooksTokens.Where(t => t.Company == "Xero").ToListAsync();
                if (xeroTokens.Any())
                {
                    var xeroAccounts = await _dbContext.ChartOfAccounts
                        .Where(c => c.Company == "Xero")
                        .ToListAsync();
                    var xeroCustomers = await _dbContext.Customers
                        .Where(c => c.Company == "Xero")
                        .ToListAsync();

                    _dbContext.ChartOfAccounts.RemoveRange(xeroAccounts);
                    _dbContext.Customers.RemoveRange(xeroCustomers);
                    _dbContext.QuickBooksTokens.RemoveRange(xeroTokens);
                }

                await _dbContext.SaveChangesAsync();
                return new OkObjectResult("All QuickBooks and Xero tokens and associated data deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout error");
                return new StatusCodeResult(500);
            }
        }

        public IActionResult GetTokenStatus()
        {
            var hasXero = _dbContext.QuickBooksTokens.Any(t => t.Company == "Xero" && t.AccessToken != null && t.TenantId != null);
            var hasQbo = _dbContext.QuickBooksTokens.Any(t => t.Company == "QBO" && t.AccessToken != null && t.RealmId != null);

            return new OkObjectResult(new TokenStatusResponse { Xero = hasXero, QuickBooks = hasQbo });
        }

        private string ExtractSubClaimFromIdToken(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken) || idToken.Split('.').Length < 2)
                return null;

            try
            {
                var payload = idToken.Split('.')[1];
                var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var jsonBytes = Convert.FromBase64String(padded);
                var json = Encoding.UTF8.GetString(jsonBytes);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("sub", out var sub) ? sub.GetString() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
