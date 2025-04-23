using System.Text;
using System.Text.Json;
using WebApplication1.Data;
using WebApplication1.Models;
using Microsoft.EntityFrameworkCore;


namespace WebApplication1.Services
{
    public class TokenService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<TokenService> _logger;

        public TokenService(ApplicationDbContext context, IConfiguration config, ILogger<TokenService> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        public async Task<string> GetValidAccessTokenAsync()
        {
            var token = await _context.QuickBooksTokens
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (token == null)
                throw new InvalidOperationException("No token found in the database.");

            var expiryTime = token.CreatedAt.AddSeconds(token.ExpiresIn - 60); 
            if (DateTime.UtcNow < expiryTime)
                return token.AccessToken;

            _logger.LogInformation("Access token expired. Refreshing...");

            var refreshedToken = await RefreshTokenAsync(token.RefreshToken);
            token.AccessToken = refreshedToken.AccessToken;
            token.RefreshToken = refreshedToken.RefreshToken;
            token.ExpiresIn = refreshedToken.ExpiresIn;
            token.XRefreshTokenExpiresIn = refreshedToken.XRefreshTokenExpiresIn;
            token.CreatedAt = DateTime.UtcNow;

            _context.Update(token);
            await _context.SaveChangesAsync();

            return token.AccessToken;
        }

        private async Task<TokenResponse> RefreshTokenAsync(string refreshToken)
        {
            var clientId = _config["QuickBooks:ClientId"];
            var clientSecret = _config["QuickBooks:ClientSecret"];
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

            using var httpClient = new HttpClient();

            var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer")
            {
                Headers = { { "Authorization", $"Basic {authHeader}" } },
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken }
            })
            };

            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception("Failed to refresh token: " + content);

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content);
            if (tokenResponse == null)
                throw new Exception("Failed to deserialize token response.");

            return tokenResponse;
        }
    }

}
