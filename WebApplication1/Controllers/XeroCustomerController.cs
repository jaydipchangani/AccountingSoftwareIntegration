using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;
using System.Threading.Tasks;
using WebApplication1.Models;
using WebApplication1.Models.Xero.WebApplication1.Dtos;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class XeroController : ControllerBase
    {
        private readonly XeroService _xeroService;

        public XeroController(XeroService xeroService)
        {
            _xeroService = xeroService;
        }

        [HttpPost("sync-contacts")]
        public async Task<IActionResult> SyncContacts()
        {
            try
            {
                await _xeroService.SyncXeroContactsAsync();
                return Ok("Xero contacts synced successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error syncing Xero contacts: {ex.Message}");
            }
        }


        [HttpPost("add-customer-to-xero")]
        public async Task<IActionResult> AddCustomerToXero([FromBody] AddCustomerToXeroDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest("Input is required.");

                var contactId = await _xeroService.AddCustomerToXeroAsync(dto);

                return Ok(new { message = "Customer added to Xero and local DB.", contactId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpPost("deactivate-contact/{contactId}")]
        public async Task<IActionResult> DeactivateContact(string contactId)
        {
            try
            {
                // Call the service to deactivate the contact in Xero
                await _xeroService.DeactivateContactAsync(contactId);
                return Ok("Contact deactivated successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }


    }
}
