using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using DataLayer.Models;
using WebApplication1.Data;
using XeroLayer.Interface;
using WebApplication1.Models;
using WebApplication1.Models.Xero;

namespace XeroLayer.XeroAuth
{
    public class XeroAuthService : IXeroAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;

        public XeroAuthService(HttpClient httpClient, IConfiguration config, ApplicationDbContext db)
        {
            _httpClient = httpClient;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _db = db;

            if (string.IsNullOrEmpty(_config["Xero:ClientId"]))
                throw new ArgumentNullException("Xero:ClientId is missing in appsettings.json");

            if (string.IsNullOrEmpty(_config["Xero:RedirectUri"]))
                throw new ArgumentNullException("Xero:RedirectUri is missing in appsettings.json");

            if (string.IsNullOrEmpty(_config["Xero:Scopes"]))
                throw new ArgumentNullException("Xero:Scopes is missing in appsettings.json");
        }

        public string BuildAuthorizationUrl()
        {
            var clientId = _config["Xero:ClientId"];
            var redirectUri = _config["Xero:RedirectUri"];
            var scopes = _config["Xero:Scopes"];

            if (string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(redirectUri) ||
                string.IsNullOrWhiteSpace(scopes))
            {
                throw new InvalidOperationException("Xero OAuth configuration is missing required values.");
            }

            var contactsScope = "accounting.contacts";
            if (!scopes.Contains(contactsScope))
            {
                scopes += " " + contactsScope;
            }

            return $"https://login.xero.com/identity/connect/authorize" +
                   $"?response_type=code" +
                   $"&client_id={Uri.EscapeDataString(clientId)}" +
                   $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                   $"&scope={Uri.EscapeDataString(scopes)}" +
                   $"&state=xyz";
        }

        public async Task<QuickBooksToken> ExchangeCodeForTokenAsync(string code, string state)
        {
            var tokenEndpoint = "https://identity.xero.com/connect/token";

            var payload = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _config["Xero:RedirectUri"],
                ["client_id"] = _config["Xero:ClientId"],
                ["client_secret"] = _config["Xero:ClientSecret"]
            };

            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(payload)
            };

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new ApplicationException($"Xero token exchange failed: {json}");

            var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
            var accessToken = tokenData.GetProperty("access_token").GetString();

            var connectionsRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.xero.com/connections");
            connectionsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            connectionsRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var connectionsResponse = await _httpClient.SendAsync(connectionsRequest);
            var connectionsJson = await connectionsResponse.Content.ReadAsStringAsync();

            if (!connectionsResponse.IsSuccessStatusCode)
                throw new ApplicationException($"Failed to retrieve Xero tenant: {connectionsJson}");

            var connections = JsonSerializer.Deserialize<JsonElement>(connectionsJson);
            string tenantId = null;

            if (connections.ValueKind == JsonValueKind.Array && connections.GetArrayLength() > 0)
            {
                tenantId = connections[0].GetProperty("tenantId").GetString();
            }
            else
            {
                throw new ApplicationException("No Xero tenant found in the connections response.");
            }

            var existingToken = await _db.QuickBooksTokens
                .FirstOrDefaultAsync(t => t.Company == "Xero");

            if (existingToken != null)
            {
                existingToken.AccessToken = accessToken;
                existingToken.RefreshToken = tokenData.GetProperty("refresh_token").GetString();
                existingToken.IdToken = tokenData.GetProperty("id_token").GetString();
                existingToken.TokenType = tokenData.GetProperty("token_type").GetString();
                existingToken.Scope = tokenData.GetProperty("scope").GetString();
                existingToken.ExpiresIn = tokenData.GetProperty("expires_in").GetInt32();
                existingToken.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32());
                existingToken.CreatedAtUtc = DateTime.UtcNow;
                existingToken.TenantId = tenantId;
            }
            else
            {
                var token = new QuickBooksToken
                {
                    AccessToken = accessToken,
                    RefreshToken = tokenData.GetProperty("refresh_token").GetString(),
                    IdToken = tokenData.GetProperty("id_token").GetString(),
                    TokenType = tokenData.GetProperty("token_type").GetString(),
                    Scope = tokenData.GetProperty("scope").GetString(),
                    ExpiresIn = tokenData.GetProperty("expires_in").GetInt32(),
                    ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32()),
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    TenantId = tenantId,
                    Company = "Xero"
                };

                _db.QuickBooksTokens.Add(token);
            }

            await _db.SaveChangesAsync();

            return existingToken;
        }

        public async Task<bool> LogoutFromXeroAsync()
        {
            var token = await _db.QuickBooksTokens
                .FirstOrDefaultAsync(t => t.Company == "Xero");

            if (token == null)
                return false;

            _db.QuickBooksTokens.Remove(token);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<XeroToken> GetXeroAuthDetailsAsync()
        {
            var token = await _db.QuickBooksTokens.FirstOrDefaultAsync(t => t.Company == "Xero");
            if (token == null)
                return null;

            return new XeroToken
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                TenantId = token.TenantId,
                ExpiresAtUtc = (DateTime)token.ExpiresAtUtc
            };
        }
    }
}
