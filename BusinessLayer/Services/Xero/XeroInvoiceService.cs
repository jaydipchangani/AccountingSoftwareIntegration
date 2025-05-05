using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplication1.Data;
using BusinessLayer.Services.Xero;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Xero
{
   public class XeroInvoiceService
    {
      
            private readonly ApplicationDbContext _db;
            private readonly IHttpClientFactory _httpClientFactory;

            public XeroInvoiceService(ApplicationDbContext db, IHttpClientFactory httpClientFactory)
            {
                _db = db;
                _httpClientFactory = httpClientFactory;
            }

        public async Task<int> FetchAndStoreInvoicesAsync()
        {
            var auth = await _db.QuickBooksTokens
                .Where(x => x.Company == "Xero")  // Filter by Xero company
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync();

            if (auth == null)
                throw new Exception("Xero auth details not found.");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            client.DefaultRequestHeaders.Add("xero-tenant-id", auth.TenantId);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.GetAsync("https://api.xero.com/api.xro/2.0/Invoices");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Console.WriteLine(json);  // Or use any logger to output the JSON string

            var doc = JsonDocument.Parse(json);
            var invoicesJson = doc.RootElement.GetProperty("Invoices");

            int count = 0;

            foreach (var invoiceJson in invoicesJson.EnumerateArray())
            {
                var invoiceId = invoiceJson.GetProperty("InvoiceID").GetString();

                var existing = await _db.Invoices.FirstOrDefaultAsync(i => i.QuickBooksId == invoiceId);
                if (existing != null) continue;

                var invoice = new Invoice
                {
                    QuickBooksId = invoiceId,
                    CustomerName = invoiceJson.GetProperty("Contact").TryGetProperty("Name", out var contactName) ? contactName.GetString() : string.Empty,
                    CustomerEmail = invoiceJson.GetProperty("Contact").TryGetProperty("EmailAddress", out var contactEmail) ? contactEmail.GetString() : string.Empty,
                    DocNumber = invoiceJson.TryGetProperty("InvoiceNumber", out var docNumber) ? docNumber.GetString() : string.Empty,
                    CustomerMemo = invoiceJson.TryGetProperty("Reference", out var customerMemo) ? customerMemo.GetString() : string.Empty,
                    TxnDate = TryGetDateTime(invoiceJson, "Date"),
                    DueDate = TryGetDateTime(invoiceJson, "DueDate"),
                    Subtotal = invoiceJson.TryGetProperty("SubTotal", out var subtotal) ? subtotal.GetDecimal() : 0,
                    TotalAmt = invoiceJson.TryGetProperty("Total", out var totalAmt) ? totalAmt.GetDecimal() : 0,
                    Balance = invoiceJson.TryGetProperty("AmountDue", out var balance) ? balance.GetDecimal() : 0,
                    XeroInvoiceType = invoiceJson.TryGetProperty("Type", out var xeroInvoiceType) ? xeroInvoiceType.GetString() : string.Empty,
                    XeroStatus = invoiceJson.TryGetProperty("Status", out var xeroStatus) ? xeroStatus.GetString() : string.Empty,
                    XeroBrandingThemeID = invoiceJson.TryGetProperty("BrandingThemeID", out var xeroBrandingThemeID) ? xeroBrandingThemeID.GetString() : string.Empty,
                    XeroCurrencyCode = invoiceJson.TryGetProperty("CurrencyCode", out var xeroCurrencyCode) ? xeroCurrencyCode.GetString() : string.Empty,
                    XeroCurrencyRate = invoiceJson.TryGetProperty("CurrencyRate", out var xeroCurrencyRate) ? xeroCurrencyRate.GetDecimal() : 1, // Assuming default value as 1
                    XeroIsDiscounted = invoiceJson.TryGetProperty("IsDiscounted", out var xeroIsDiscounted) ? xeroIsDiscounted.GetBoolean() : false,
                    XeroLineAmountTypes = invoiceJson.TryGetProperty("LineAmountTypes", out var xeroLineAmountTypes) ? xeroLineAmountTypes.GetString() : string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Platform = "Xero",
                };

                // Line Items
                foreach (var lineJson in invoiceJson.GetProperty("LineItems").EnumerateArray())
                {
                    invoice.LineItems.Add(new InvoiceLineItem
                    {
                        Description = lineJson.TryGetProperty("Description", out var description) ? description.GetString() : string.Empty,
                        Quantity = lineJson.TryGetProperty("Quantity", out var quantity) ? quantity.GetInt32() : 0,
                        Rate = lineJson.TryGetProperty("UnitAmount", out var rate) ? rate.GetDecimal() : 0,
                        Amount = lineJson.TryGetProperty("LineAmount", out var amount) ? amount.GetDecimal() : 0,
                        XeroLineItemId = lineJson.TryGetProperty("LineItemID", out var lineItemId) ? lineItemId.GetString() : string.Empty,
                        XeroAccountCode = lineJson.TryGetProperty("AccountCode", out var accountCode) ? accountCode.GetString() : string.Empty,
                        XeroTaxType = lineJson.TryGetProperty("TaxType", out var taxType) ? taxType.GetString() : string.Empty,
                        XeroTaxAmount = lineJson.TryGetProperty("TaxAmount", out var taxAmount) ? taxAmount.GetDecimal() : 0,
                        XeroDiscountRate = lineJson.TryGetProperty("DiscountRate", out var discountRate) ? discountRate.GetDecimal() : 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                _db.Invoices.Add(invoice);
                count++;
            }

            await _db.SaveChangesAsync();
            return count;
        }

        // Helper method to safely parse DateTime
        private DateTime TryGetDateTime(JsonElement jsonElement, string propertyName)
        {
            if (jsonElement.TryGetProperty(propertyName, out var dateElement))
            {
                if (dateElement.TryGetDateTime(out var result))
                {
                    return result;
                }
            }
            return DateTime.MinValue; // Return a default DateTime value if parsing fails
        }



    }
}
