using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using WebApplication1.Models.Xero;
using WebApplication1.Models.Xero;
using WebApplication1.Data;

public class XeroAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ApplicationDbContext _db;

    public XeroAuthService(HttpClient httpClient, IConfiguration config, ApplicationDbContext db)
    {
        _httpClient = httpClient;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _db = db;


        // Optional sanity check to catch misconfiguration early
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

        return $"https://login.xero.com/identity/connect/authorize" +
               $"?response_type=code" +
               $"&client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&scope={Uri.EscapeDataString(scopes)}" +
               $"&state=xyz";
    }


    public async Task<XeroToken> ExchangeCodeForTokenAsync(string code, string state)
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

        var token = new XeroToken
        {
            AccessToken = tokenData.GetProperty("access_token").GetString(),
            RefreshToken = tokenData.GetProperty("refresh_token").GetString(),
            IdToken = tokenData.GetProperty("id_token").GetString(),
            TokenType = tokenData.GetProperty("token_type").GetString(),
            Scope = tokenData.GetProperty("scope").GetString(),
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32()),
            TenantId = "" // Will be set after fetching from /connections
        };

        _db.XeroTokens.Add(token);
        await _db.SaveChangesAsync();

        return token;
    }

}
