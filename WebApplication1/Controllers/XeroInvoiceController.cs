using Microsoft.AspNetCore.Mvc;
using BusinessLayer.Services.Xero;
using DataLayer.Models.Xero;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Services;
using System;
using WebApplication1.Data;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class XeroInvoiceController : ControllerBase
    {
        private readonly XeroInvoiceService _xeroInvoiceService;
        private readonly ApplicationDbContext _context;

        public XeroInvoiceController(XeroInvoiceService xeroInvoiceService,ApplicationDbContext context)
        {
            _xeroInvoiceService = xeroInvoiceService;
            _context = context;
        }

        [HttpGet("fetch")]
        public async Task<IActionResult> FetchAndStoreXeroInvoices()
        {
            var result = await _xeroInvoiceService.FetchAndStoreInvoicesAsync();
            return Ok(new { message = "Invoices synced successfully", count = result });
        }



        [HttpPost("add-invoice")]
        public async Task<IActionResult> AddInvoice([FromBody] XeroInvoiceCreateDto dto)
        {
            var auth = await _context.QuickBooksTokens
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (auth == null)
                return BadRequest("No Xero credentials found.");

            try
            {
                var invoiceId = await _xeroInvoiceService.AddInvoiceToXeroAndDbAsync(dto, auth.AccessToken, auth.TenantId);
                return Ok(new { message = "Invoice created", invoiceId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
