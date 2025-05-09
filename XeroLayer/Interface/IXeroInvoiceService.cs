using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataLayer.Models.Xero;
using Microsoft.AspNetCore.Mvc;

namespace XeroLayer.Interface
{
    public interface IXeroInvoiceService
    {
        Task<int> FetchAndStoreInvoicesAsync(string? type = null);
        Task<string> AddInvoiceToXeroAndDbAsync(XeroInvoiceCreateDto dto, string accessToken, string tenantId);
        Task<IActionResult> DeleteInvoice(string invoiceId);
        Task<string> UpdateInvoiceInXeroAsync(string invoiceId, XeroInvoiceUpdateDto dto, string accessToken, string tenantId);
        Task<string> GetInvoiceFromXeroByIdAsync(string invoiceId);
    }
}
