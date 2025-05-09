using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;
using System.Threading.Tasks;
using WebApplication1.Models;
using WebApplication1.Models.Xero.WebApplication1.Dtos;
using XeroLayer.Interfaces;

namespace WebApplication1.Controllers.Xero
{
    [Route("api/[controller]")]
    [ApiController]
    public class XeroController : ControllerBase
    {
        private readonly IXeroCustomerService _xeroService;

        public XeroController(IXeroCustomerService xeroService)
        {
            _xeroService = xeroService;
        }

        [HttpPost("sync-contacts")]
        public async Task<IActionResult> SyncXeroContactsAsync()
        {
            await _xeroService.SyncXeroContactsAsync();
            return Ok("Contacts synced successfully");
        }

        [HttpPost("add-customer-to-xero")]
        public async Task<IActionResult> AddCustomerToXeroAsync(AddCustomerToXeroDto dto)
        {
            var result = await _xeroService.AddCustomerToXeroAsync(dto);
            return Ok(result);
        }

        [HttpPost("update-customer")]
        public async Task<IActionResult> UpdateCustomerInXeroAsync(UpdateCustomerInXeroDto dto)
        {
            var result = await _xeroService.UpdateCustomerInXeroAsync(dto);
            return Ok(result);
        }

        [HttpPost("archive-contact/{contactId}")]
        public async Task<IActionResult> ArchiveContactAsync(string contactId)
        {
            var result = await _xeroService.ArchiveContactAsync(contactId);
            return Ok(result);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetXeroCustomersAsync()
        {
            var result = await _xeroService.GetXeroCustomersAsync();
            return Ok(result);
        }
    }
}
