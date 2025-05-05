using Microsoft.AspNetCore.Mvc;
using BusinessLayer.Services.Xero;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class XeroInvoiceController : ControllerBase
    {
        private readonly XeroInvoiceService _xeroInvoiceService;

        public XeroInvoiceController(XeroInvoiceService xeroInvoiceService)
        {
            _xeroInvoiceService = xeroInvoiceService;
        }

        [HttpGet("fetch")]
        public async Task<IActionResult> FetchAndStoreXeroInvoices()
        {
            var result = await _xeroInvoiceService.FetchAndStoreInvoicesAsync();
            return Ok(new { message = "Invoices synced successfully", count = result });
        }
    }
}
