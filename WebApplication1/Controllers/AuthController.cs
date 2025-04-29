using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthController> _logger;
        private readonly ApplicationDbContext _dbContext;


        public AuthController(IConfiguration config, ApplicationDbContext context, ILogger<AuthController> logger, ApplicationDbContext dbContext)
        {
            _config = config;
            _context = context;
            _logger = logger;
            _dbContext = dbContext;
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            var clientId = _config["QuickBooks:ClientId"];
            var redirectUri = _config["QuickBooks:RedirectUri"];
            var scope = "com.intuit.quickbooks.accounting openid profile email phone address";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["client_id"] = clientId;
            query["redirect_uri"] = redirectUri;
            query["response_type"] = "code";
            query["scope"] = scope;
            query["state"] = Guid.NewGuid().ToString();

            var authUrl = $"https://appcenter.intuit.com/connect/oauth2?{query}";


            return Redirect(authUrl);
        }

        [HttpPost("exchange")]
        public async Task<IActionResult> ExchangeCode([FromBody] ExchangeRequest request)
        {
            _logger.LogInformation("Starting token exchange process...");

            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.RealmId))
            {
                _logger.LogWarning("Missing authorization code or realm ID.");
                return BadRequest("Code or RealmId is missing.");
            }

            try
            {
                var clientId = _config["QuickBooks:ClientId"];
                var clientSecret = _config["QuickBooks:ClientSecret"];
                var redirectUri = _config["QuickBooks:RedirectUri"];

                _logger.LogInformation("Preparing HTTP request to QuickBooks token endpoint...");

                var httpClient = new HttpClient();
                var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

                var postData = new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", request.Code },
                    { "redirect_uri", redirectUri }
                };

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer")
                {
                    Headers = { { "Authorization", $"Basic {authHeader}" } },
                    Content = new FormUrlEncodedContent(postData)
                };

                var response = await httpClient.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("QuickBooks token endpoint response: {Response}", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Token exchange failed: {StatusCode} - {Content}", response.StatusCode, content);
                    return BadRequest("Token exchange failed: " + content);
                }

                var tokenData = JsonSerializer.Deserialize<TokenResponse>(content);

                if (tokenData == null)
                {
                    _logger.LogError("Failed to deserialize token response.");
                    return StatusCode(500, "Token data could not be parsed.");
                }

                var quickBooksUserId = ExtractSubClaimFromIdToken(tokenData.IdToken);
                _logger.LogInformation("QuickBooks User ID extracted: {UserId}", quickBooksUserId);


                // Check if a token already exists for Company = "QBO"
                var existingToken = await _context.QuickBooksTokens
                    .FirstOrDefaultAsync(t => t.Company == "QBO");

                if (existingToken != null)
                {
                    // Update existing record
                    existingToken.QuickBooksUserId = quickBooksUserId;
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
                    // Insert new record
                    var token = new QuickBooksToken
                    {
                        QuickBooksUserId = quickBooksUserId,
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

                    _context.QuickBooksTokens.Add(token);
                }

                // Save changes in both cases
                await _context.SaveChangesAsync();

                _logger.LogInformation("Token successfully saved/updated in database for Company: QBO.");

                return Ok(new
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
                _logger.LogError(ex, "Unexpected error occurred during token exchange.");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        private string ExtractSubClaimFromIdToken(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken) || idToken.Split('.').Length < 2)
                return null;

            try
            {
                var payload = idToken.Split('.')[1];
                var padded = PadBase64(payload);
                var jsonBytes = Convert.FromBase64String(padded);
                var json = Encoding.UTF8.GetString(jsonBytes);

                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("sub", out JsonElement subElement))
                {
                    return subElement.GetString();
                }

                return null;
            }
            catch
            {
                return null; // Optionally log the error for debugging
            }
        }

        private string PadBase64(string base64)
        {
            return base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        }


        [HttpDelete("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                // Delete ONLY QuickBooks tokens where Company == "QBO"
                var quickBooksTokens = await _dbContext.QuickBooksTokens
                    .Where(t => t.Company == "QBO") 
                    .ToListAsync();

                if (quickBooksTokens.Any())
                {
                    var quickBooksUserIds = quickBooksTokens.Select(t => t.QuickBooksUserId).Distinct().ToList();

                    var qboAccountsToDelete = await _dbContext.ChartOfAccounts
                        .Where(c => quickBooksUserIds.Contains(c.QuickBooksUserId) && c.Company == "QBO")
                        .ToListAsync();

                    if (qboAccountsToDelete.Any())
                    {
                        _dbContext.ChartOfAccounts.RemoveRange(qboAccountsToDelete);
                        _logger.LogInformation($"Deleted {qboAccountsToDelete.Count} QBO Chart of Accounts entries.");
                    }

                    var customersToDelete = await _dbContext.Customers
                        .Where(c => quickBooksUserIds.Contains(c.QuickBooksUserId))
                        .ToListAsync();

                    if (customersToDelete.Any())
                    {
                        _dbContext.Customers.RemoveRange(customersToDelete);
                        _logger.LogInformation($"Deleted {customersToDelete.Count} Customer records.");
                    }

                    _dbContext.QuickBooksTokens.RemoveRange(quickBooksTokens);
                    _logger.LogInformation($"Deleted {quickBooksTokens.Count} QuickBooks token records.");
                }

                // Delete ONLY Xero tokens and associated data
                var xeroTokens = await _dbContext.QuickBooksTokens
                    .Where(t => t.Company == "Xero")
                    .ToListAsync();

                if (xeroTokens.Any())
                {
                    var xeroUserIds = xeroTokens
                        .Select(t => t.QuickBooksUserId)
                        .Distinct()
                        .ToList();

                    var xeroAccountsToDelete = await _dbContext.ChartOfAccounts
                        .Where(c => c.Company == "Xero")
                        .ToListAsync();

                    if (xeroAccountsToDelete.Any())
                    {
                        _dbContext.ChartOfAccounts.RemoveRange(xeroAccountsToDelete);
                        _logger.LogInformation($"Deleted {xeroAccountsToDelete.Count} Xero Chart of Accounts entries.");
                    }

                    var customersToDelete = await _dbContext.Customers
     .Where(c => c.Company == "Xero")
     .ToListAsync();

                    _dbContext.Customers.RemoveRange(customersToDelete);
                    await _dbContext.SaveChangesAsync();


                    if (customersToDelete.Any())
                    {
                        _dbContext.Customers.RemoveRange(customersToDelete);
                        _logger.LogInformation($"Deleted {customersToDelete.Count} Customer records for Xero users.");
                    }

                    _dbContext.QuickBooksTokens.RemoveRange(xeroTokens);
                    _logger.LogInformation($"Deleted {xeroTokens.Count} Xero token records.");
                }


                await _dbContext.SaveChangesAsync();

                return Ok("All QuickBooks and Xero tokens and associated data deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout and deletion.");
                return StatusCode(500, $"Error during logout and deletion: {ex.Message}");
            }


        }

        [HttpGet("token-status")]
        public IActionResult GetTokenStatus()
        {
            var hasXeroToken = _context.QuickBooksTokens
                .Any(t => t.Company == "Xero" && t.AccessToken != null && t.TenantId != null);

            var hasQuickBooksToken = _context.QuickBooksTokens
                .Any(t => t.Company == "QBO" && t.AccessToken != null && t.RealmId != null);

            var result = new TokenStatusResponse
            {
                Xero = hasXeroToken,
                QuickBooks = hasQuickBooksToken
            };

            return Ok(result);
        }


        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshTokenAsync()
        {
            try
            {
                _logger.LogInformation("Starting refresh token process...");

                var latestToken = await _dbContext.QuickBooksTokens
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (latestToken == null || string.IsNullOrEmpty(latestToken.RefreshToken))
                {
                    _logger.LogWarning("No valid refresh token found.");
                    return BadRequest("No valid refresh token found.");
                }

                var clientId = _config["QuickBooks:ClientId"];
                var clientSecret = _config["QuickBooks:ClientSecret"];
                var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

                var httpClient = new HttpClient();
                var postData = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", latestToken.RefreshToken }
        };

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer")
                {
                    Headers = { { "Authorization", $"Basic {authHeader}" } },
                    Content = new FormUrlEncodedContent(postData)
                };

                var response = await httpClient.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Refresh token response: {Content}", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Refresh token failed: {StatusCode} - {Content}", response.StatusCode, content);
                    return BadRequest("Refresh token failed: " + content);
                }

                var refreshedToken = JsonSerializer.Deserialize<TokenResponse>(content);

                if (refreshedToken == null)
                {
                    _logger.LogError("Failed to deserialize refreshed token response.");
                    return StatusCode(500, "Failed to deserialize refreshed token data.");
                }

                // Update token in database
                latestToken.AccessToken = refreshedToken.AccessToken;
                latestToken.RefreshToken = refreshedToken.RefreshToken;
                latestToken.ExpiresIn = refreshedToken.ExpiresIn;
                latestToken.XRefreshTokenExpiresIn = refreshedToken.XRefreshTokenExpiresIn;
                latestToken.CreatedAt = DateTime.UtcNow;

                _dbContext.QuickBooksTokens.Update(latestToken);
                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    message = "Token refreshed successfully",
                    token = new
                    {
                        latestToken.AccessToken,
                        latestToken.RefreshToken,
                        latestToken.RealmId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while refreshing QuickBooks token.");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }


    }
}
