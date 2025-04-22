using WebApplication1.Data;
using WebApplication1.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace WebApplication1.Services
{
    public class VendorService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;


        public VendorService(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<(List<Vendor> vendors, int totalCount)> GetVendorsAsync(int pageNumber, int pageSize, string? search)
        {
            var query = _context.Vendors.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(v => v.DisplayName.Contains(search));
            }

            var totalCount = await query.CountAsync();

            var vendors = await query
                .OrderBy(v => v.DisplayName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (vendors, totalCount);
        }

        private bool IsValidEmail(string? email)
        {
            return !string.IsNullOrWhiteSpace(email) &&
                   System.Net.Mail.MailAddress.TryCreate(email, out _);
        }

        private bool IsValidUrl(string? url)
        {
            return !string.IsNullOrWhiteSpace(url) &&
                   Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        public async Task<Vendor?> AddVendorAsync(Vendor inputVendor)
        {
            // Get QuickBooks credentials
            var qbToken = await _context.QuickBooksTokens
                .OrderByDescending(q => q.Id)
                .FirstOrDefaultAsync();

            if (qbToken == null || string.IsNullOrWhiteSpace(qbToken.AccessToken) || string.IsNullOrWhiteSpace(qbToken.RealmId))
            {
                throw new InvalidOperationException("QuickBooks credentials not found.");
            }

            var accessToken = qbToken.AccessToken;
            var realmId = qbToken.RealmId;

            // Prepare request body
            var payload = new
            {
                DisplayName = inputVendor.DisplayName,
                PrimaryEmailAddr = IsValidEmail(inputVendor.PrimaryEmailAddr) ? new { Address = inputVendor.PrimaryEmailAddr } : null,
                PrimaryPhone = string.IsNullOrWhiteSpace(inputVendor.PrimaryPhone) ? null : new { FreeFormNumber = inputVendor.PrimaryPhone },
                WebAddr = IsValidUrl(inputVendor.WebAddr) ? new { URI = inputVendor.WebAddr } : null,
                BillAddr = string.IsNullOrEmpty(inputVendor.BillAddrLine1) &&
                           string.IsNullOrEmpty(inputVendor.BillAddrCity) &&
                           string.IsNullOrEmpty(inputVendor.BillAddrPostalCode)
                    ? null
                    : new
                    {
                        Line1 = inputVendor.BillAddrLine1,
                        City = inputVendor.BillAddrCity,
                        PostalCode = inputVendor.BillAddrPostalCode
                    }
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { IgnoreNullValues = true });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Send request to QuickBooks API
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var baseUrl = _configuration["QuickBooks:BaseUrl"];
            var apiUrl = $"{baseUrl}/{realmId}/vendor?minorversion=75";
            var response = await client.PostAsync(apiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                throw new ApplicationException($"Failed to add vendor to QuickBooks: {errorMessage}");
            }

            // Extract response data
            var responseStream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(responseStream);

            var vendorJson = document.RootElement.GetProperty("Vendor");

            var newVendor = new Vendor
            {
                VId = Guid.NewGuid().ToString(),
                DisplayName = vendorJson.GetProperty("DisplayName").GetString(),
                Active = vendorJson.TryGetProperty("Active", out var active) && active.GetBoolean(),
                Vendor1099 = vendorJson.TryGetProperty("Vendor1099", out var is1099) && is1099.GetBoolean(),
                Balance = vendorJson.TryGetProperty("Balance", out var balance) ? balance.GetDecimal() : 0,
                PrimaryEmailAddr = vendorJson.TryGetProperty("PrimaryEmailAddr", out var email) && email.TryGetProperty("Address", out var emailVal) ? emailVal.GetString() : null,
                PrimaryPhone = vendorJson.TryGetProperty("PrimaryPhone", out var phone) && phone.TryGetProperty("FreeFormNumber", out var phoneVal) ? phoneVal.GetString() : null,
                WebAddr = vendorJson.TryGetProperty("WebAddr", out var web) && web.TryGetProperty("URI", out var webVal) ? webVal.GetString() : null,
                BillAddrLine1 = vendorJson.TryGetProperty("BillAddr", out var addr) && addr.TryGetProperty("Line1", out var line1) ? line1.GetString() : null,
                BillAddrCity = addr.TryGetProperty("City", out var city) ? city.GetString() : null,
                BillAddrPostalCode = addr.TryGetProperty("PostalCode", out var postal) ? postal.GetString() : null,
                CreateTime = vendorJson.GetProperty("MetaData").GetProperty("CreateTime").GetDateTime(),
                LastUpdatedTime = vendorJson.GetProperty("MetaData").GetProperty("LastUpdatedTime").GetDateTime(),
                SyncToken = vendorJson.TryGetProperty("SyncToken", out var syncToken) ? syncToken.GetString() : null
            };

            // Save to local DB
            _context.Vendors.Add(newVendor);
            await _context.SaveChangesAsync();

            return newVendor;
        }

        public async Task<Vendor> GetVendorByVIdAsync(string vId)
        {
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.VId == vId);
            if (vendor == null)
            {
                throw new KeyNotFoundException($"Vendor with VId {vId} not found.");
            }
            return vendor;
        }


        public async Task<List<Vendor>> SyncVendorsAsync()
        {
            var qbToken = await _context.QuickBooksTokens
                .OrderByDescending(q => q.Id)
                .FirstOrDefaultAsync();

            if (qbToken == null || string.IsNullOrWhiteSpace(qbToken.AccessToken) || string.IsNullOrWhiteSpace(qbToken.RealmId))
            {
                throw new Exception("QuickBooks credentials not found.");
            }

            var baseUrl = _configuration["QuickBooks:BaseUrl"];
            var apiUrl = $"{baseUrl}/{qbToken.RealmId}/query?query=select * from vendor&minorversion=75";

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", qbToken.AccessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await httpClient.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();

            var contentStream = await response.Content.ReadAsStreamAsync();

            using var document = await JsonDocument.ParseAsync(contentStream);
            if (!document.RootElement.TryGetProperty("QueryResponse", out var queryResponse) ||
                !queryResponse.TryGetProperty("Vendor", out var vendorsJson))
            {
                return new List<Vendor>();
            }

            // Load existing Vendor VIds from database
            var existingVendorIds = new HashSet<string>(
                await _context.Vendors.Select(v => v.VId).ToListAsync()
            );

            var vendorsToAdd = new List<Vendor>();

            foreach (var v in vendorsJson.EnumerateArray())
            {
                var vId = v.GetProperty("Id").GetString();
                if (existingVendorIds.Contains(vId))
                    continue; // Skip if already exists

                string currencyValue = null, currencyName = null;
                if (v.TryGetProperty("CurrencyRef", out var currencyRef))
                {
                    if (currencyRef.TryGetProperty("value", out var val)) currencyValue = val.GetString();
                    if (currencyRef.TryGetProperty("name", out var name)) currencyName = name.GetString();
                }

                string line1 = null, city = null, postal = null;
                if (v.TryGetProperty("BillAddr", out var addr))
                {
                    if (addr.TryGetProperty("Line1", out var l)) line1 = l.GetString();
                    if (addr.TryGetProperty("City", out var c)) city = c.GetString();
                    if (addr.TryGetProperty("PostalCode", out var p)) postal = p.GetString();
                }

                string phone = null;
                if (v.TryGetProperty("PrimaryPhone", out var phoneJson) &&
                    phoneJson.TryGetProperty("FreeFormNumber", out var number))
                    phone = number.GetString();

                string email = null;
                if (v.TryGetProperty("PrimaryEmailAddr", out var emailJson) &&
                    emailJson.TryGetProperty("Address", out var emailAddr))
                    email = emailAddr.GetString();

                string webAddr = null;
                if (v.TryGetProperty("WebAddr", out var web) &&
                    web.TryGetProperty("URI", out var uri))
                    webAddr = uri.GetString();

                DateTime createTime = default, lastUpdatedTime = default;
                if (v.TryGetProperty("MetaData", out var meta))
                {
                    if (meta.TryGetProperty("CreateTime", out var cTime)) createTime = DateTime.Parse(cTime.GetString());
                    if (meta.TryGetProperty("LastUpdatedTime", out var uTime)) lastUpdatedTime = DateTime.Parse(uTime.GetString());
                }

                vendorsToAdd.Add(new Vendor
                {
                    VId = vId,
                    DisplayName = v.GetProperty("DisplayName").GetString(),
                    Active = v.TryGetProperty("Active", out var active) && active.GetBoolean(),
                    Vendor1099 = v.TryGetProperty("Vendor1099", out var is1099) && is1099.GetBoolean(),
                    Balance = v.TryGetProperty("Balance", out var balance) ? balance.GetDecimal() : 0,
                    CurrencyValue = currencyValue,
                    CurrencyName = currencyName,
                    BillAddrLine1 = line1,
                    BillAddrCity = city,
                    BillAddrPostalCode = postal,
                    SyncToken = v.TryGetProperty("SyncToken", out var token) ? token.GetString() : null,
                    V4IDPseudonym = null,
                    PrimaryPhone = phone,
                    PrimaryEmailAddr = email,
                    WebAddr = webAddr,
                    CreateTime = createTime,
                    LastUpdatedTime = lastUpdatedTime
                });
            }

            try
            {
                if (vendorsToAdd.Any())
                {
                    await _context.Vendors.AddRangeAsync(vendorsToAdd);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving vendors: {ex.Message}");
                throw new Exception("Error occurred while saving vendors to the database", ex);
            }

            return vendorsToAdd;
        }



    }
}
