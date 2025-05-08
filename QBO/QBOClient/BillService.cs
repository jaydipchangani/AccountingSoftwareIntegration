using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebApplication1.Data;
using WebApplication1.Models;

public class BillService
{
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public BillService(HttpClient httpClient, ApplicationDbContext context, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _context = context;
        _configuration = configuration;
    }

    public async Task<List<Bill>> FetchAndStoreAllBillsAsync()
    {
        // Step 1: Clear existing Bills and LineItems
        var allBills = await _context.Bills.Include(b => b.LineItems).ToListAsync();

        if (allBills.Any())
        {
            _context.BillLineItems.RemoveRange(allBills.SelectMany(b => b.LineItems));
            _context.Bills.RemoveRange(allBills);
            await _context.SaveChangesAsync();
        }

        // Step 2: Fetch accessToken and realmId
        var auth = await _context.QuickBooksTokens
            .OrderByDescending(q => q.CreatedAt)
            .Where(q => q.Company == "QBO")
            .FirstOrDefaultAsync();

        if (auth == null || string.IsNullOrEmpty(auth.AccessToken) || string.IsNullOrEmpty(auth.RealmId))
        {
            throw new Exception("QuickBooks credentials not found in the database.");
        }


        var baseUrl = _configuration["QuickBooks:BaseUrl"];
        var requestUri = $"{baseUrl}/{auth.RealmId}/query?query=select * from Bill&minorversion=65";
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to fetch bills from QuickBooks.");
        }

        var json = await response.Content.ReadAsStringAsync();
        var qbData = JsonDocument.Parse(json);

        if (!qbData.RootElement.TryGetProperty("QueryResponse", out var queryResponse) ||
            !queryResponse.TryGetProperty("Bill", out var qbBills))
        {
            return new List<Bill>(); // No bills returned
        }

        var storedBills = new List<Bill>();

        foreach (var qbBill in qbBills.EnumerateArray())
        {
            var qboBillId = qbBill.GetProperty("Id").GetString();

            var bill = new Bill
            {
                QboBillId = qboBillId,
                VendorId = await GetVendorIdByQboId(qbBill.GetProperty("VendorRef").GetProperty("value").GetString()),
                VendorAddress = qbBill.TryGetProperty("VendorAddr", out var addr) ? addr.GetProperty("Line1").GetString() : null,
                Currency = qbBill.TryGetProperty("CurrencyRef", out var currency) ? currency.GetProperty("name").GetString() : null,
                DocNumber = qbBill.TryGetProperty("DocNumber", out var docNum) ? docNum.GetString() : null,
                TxnDate = DateTime.Parse(qbBill.GetProperty("TxnDate").GetString()),
                DueDate = DateTime.Parse(qbBill.GetProperty("DueDate").GetString()),
                Balance = qbBill.TryGetProperty("Balance", out var balance) ? balance.GetDecimal() : 0,
                TotalAmt = qbBill.GetProperty("TotalAmt").GetDecimal(),
                APAccountName = qbBill.TryGetProperty("APAccountRef", out var ap) ? ap.GetProperty("name").GetString() : null,
                SyncToken = qbBill.TryGetProperty("SyncToken", out var token) ? token.GetString() : null,
                LineItems = new List<BillLineItem>()
            };

            if (qbBill.TryGetProperty("Line", out var lines))
            {
                foreach (var line in lines.EnumerateArray())
                {
                    var item = new BillLineItem
                    {
                        DetailType = line.GetProperty("DetailType").GetString(),
                        Description = line.TryGetProperty("Description", out var desc) ? desc.GetString() : null,
                        Amount = line.GetProperty("Amount").GetDecimal()
                    };

                    if (line.TryGetProperty("AccountBasedExpenseLineDetail", out var detail))
                    {
                        item.AccountId = await GetAccountIdByQboId(detail.GetProperty("AccountRef").GetProperty("value").GetString());

                        if (detail.TryGetProperty("Qty", out var qty))
                            item.Quantity = qty.GetDecimal();

                        if (detail.TryGetProperty("UnitPrice", out var price))
                            item.UnitPrice = price.GetDecimal();
                    }

                    bill.LineItems.Add(item);
                }
            }

            _context.Bills.Add(bill);
            storedBills.Add(bill);
        }

        await _context.SaveChangesAsync();
        return storedBills;
    }


    private async Task<int> GetVendorIdByQboId(string qboVendorId)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.VId == qboVendorId);
        if (vendor == null)
            throw new Exception($"Vendor with QBO ID {qboVendorId} not found.");
        return vendor.Id;
    }

    private async Task<int?> GetAccountIdByQboId(string qboAccountId)
    {
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.QbId == qboAccountId);
        return account?.Id;
    }

    public async Task<(List<Bill> Data, int TotalCount)> GetPagedBillsAsync(string searchTerm, int page, int pageSize)
    {
        var query = _context.Bills
            .Include(b => b.Vendor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(b =>
                b.DocNumber.Contains(searchTerm) ||
                b.Vendor.DisplayName.Contains(searchTerm));
        }

        var totalCount = await query.CountAsync();

        var data = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (data, totalCount);
    }

    public async Task<object> AddBillToQboAsync(CreateBillDto dto)
    {
        var auth = await _context.QuickBooksTokens
            .OrderByDescending(q => q.CreatedAt)
            .Where(q => q.Company == "QBO")
            .FirstOrDefaultAsync();

        if (auth == null || string.IsNullOrEmpty(auth.AccessToken) || string.IsNullOrEmpty(auth.RealmId))
        {
            throw new Exception("QuickBooks credentials not found in the database.");
        }

        var vendor = await _context.Vendors.FindAsync(dto.VendorId);
        if (vendor == null)
            throw new Exception("Vendor not found");

        var qboBill = new
        {
            VendorRef = new { value = vendor.VId.ToString() },

            DocNumber = dto.DocNumber,
            TxnDate = dto.TxnDate.ToString("yyyy-MM-dd"),
            DueDate = dto.DueDate.ToString("yyyy-MM-dd"),

            Line = dto.LineItems.Select(item =>
            {
                var line = new Dictionary<string, object>
    {
        { "DetailType", item.DetailType },
        { "Amount", item.Amount }
    };

                if (item.DetailType == "AccountBasedExpenseLineDetail")
                {
                    var account = _context.Accounts.FirstOrDefault(a => a.Id == item.AccountId);
                    if (account == null) throw new Exception("Account not found");

                    line["AccountBasedExpenseLineDetail"] = new
                    {
                        AccountRef = new { value = account.QbId.ToString() }
                    };
                }

                return line;
            }).ToList()

        };

        var json = JsonSerializer.Serialize(qboBill, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        });


        var baseUrl = _configuration["QuickBooks:BaseUrl"];
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{baseUrl}/{auth.RealmId}/bill");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"QBO error: {response.StatusCode}, {responseContent}");

        return JsonSerializer.Deserialize<JsonElement>(responseContent);
    }




}
