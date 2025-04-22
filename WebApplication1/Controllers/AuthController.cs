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
                    CreatedAt = DateTime.UtcNow
                };

                _context.QuickBooksTokens.Add(token);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Token successfully saved to database with ID: {TokenId}", token.Id);

                return Ok(new
                {
                    message = "Token saved successfully",
                    token = new
                    {
                        token.AccessToken,
                        token.RefreshToken,
                        token.IdToken,
                        token.RealmId
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
                var tokenRecord = await _dbContext.QuickBooksTokens
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (tokenRecord == null)
                    return NotFound("No QuickBooks token found.");

                var quickBooksUserId = tokenRecord.QuickBooksUserId;

                var accountsToDelete = await _dbContext.ChartOfAccounts
                    .Where(c => c.QuickBooksUserId == quickBooksUserId)
                    .ToListAsync();

                if (accountsToDelete.Any())
                {
                    _dbContext.ChartOfAccounts.RemoveRange(accountsToDelete);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation($"Deleted {accountsToDelete.Count} Chart of Accounts entries for QuickBooksUserId: {quickBooksUserId}.");
                }

                var customersToDelete = await _dbContext.Customers
                    .Where(c => c.QuickBooksUserId == quickBooksUserId)
                    .ToListAsync();

                if (customersToDelete.Any())
                {
                    _dbContext.Customers.RemoveRange(customersToDelete);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation($"Deleted {customersToDelete.Count} Customer records.");
                }

                _dbContext.QuickBooksTokens.Remove(tokenRecord);
                await _dbContext.SaveChangesAsync();

                return Ok("Token and associated data deleted successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error during logout and deletion: {ex.Message}");
            }
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
