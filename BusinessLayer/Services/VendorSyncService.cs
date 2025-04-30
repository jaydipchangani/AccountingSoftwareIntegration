using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebApplication1.Data;
using WebApplication1.Models;
using System.Net.Http;
using Microsoft.Extensions.Http;




namespace WebApplication1.Services
{
    public class VendorSyncService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public VendorSyncService(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration   )
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
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

            var existingVendors = await _context.Vendors.ToListAsync();
            _context.Vendors.RemoveRange(existingVendors);
            await _context.SaveChangesAsync();

            var vendorsToAdd = new List<Vendor>();

            _context.Vendors.RemoveRange(_context.Vendors);
            await _context.SaveChangesAsync();

            foreach (var v in vendorsJson.EnumerateArray())
            {
                

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
                    VId = v.GetProperty("Id").GetString(),
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

            if (vendorsToAdd.Any())
            {
                await _context.Vendors.AddRangeAsync(vendorsToAdd);
                await _context.SaveChangesAsync();
            }

            return vendorsToAdd;
        }
    }
}
