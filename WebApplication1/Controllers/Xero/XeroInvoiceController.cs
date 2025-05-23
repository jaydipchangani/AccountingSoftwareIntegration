﻿using Microsoft.AspNetCore.Mvc;
using DataLayer.Models.Xero;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using XeroLayer;
using XeroLayer.Interface;



namespace WebApplication1.Controllers.Xero
{
    [Route("api/[controller]")]
    [ApiController]
    public class XeroInvoiceController : ControllerBase
    {
        private readonly IXeroInvoiceService _xeroInvoiceService; 
        private readonly ApplicationDbContext _context;

        public XeroInvoiceController(IXeroInvoiceService xeroInvoiceService, ApplicationDbContext context)
        {
            _xeroInvoiceService = xeroInvoiceService;
            _context = context;
        }


        [HttpGet]
        public async Task<IActionResult> FetchAndStoreXeroInvoices([FromQuery] string? type)
        {
            var result = await _xeroInvoiceService.FetchAndStoreInvoicesAsync(type);
            return Ok(new { message = "Invoices synced successfully", count = result });
        }



        [HttpPost]
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


        [HttpDelete("{invoiceId}")]
        public async Task<IActionResult> DeleteInvoice(string invoiceId)
        {
            // Call the DeleteInvoice service method and return its result
            var result = await _xeroInvoiceService.DeleteInvoice(invoiceId);
            return result;
        }


        [HttpPost("{invoiceId}")]
        public async Task<IActionResult> UpdateInvoice(string invoiceId, [FromBody] XeroInvoiceUpdateDto dto)
        {
            var auth = await _context.QuickBooksTokens
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (auth == null)
                return BadRequest("No Xero credentials found.");

            try
            {
                var updatedInvoiceId = await _xeroInvoiceService.UpdateInvoiceInXeroAsync(invoiceId, dto, auth.AccessToken, auth.TenantId);
                return Ok(new { message = "Invoice updated", invoiceId = updatedInvoiceId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }


        [HttpGet("{invoiceId}")]
        public async Task<IActionResult> GetInvoiceById(string invoiceId)
        {
            try
            {
                var invoiceJson = await _xeroInvoiceService.GetInvoiceFromXeroByIdAsync(invoiceId);
                return Content(invoiceJson, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching invoice: {ex.Message}");
            }
        }

    }
}
