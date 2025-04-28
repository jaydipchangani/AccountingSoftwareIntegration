using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;
using System.Threading.Tasks;

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
