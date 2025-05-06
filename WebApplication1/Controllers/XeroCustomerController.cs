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


        [HttpPost("update-customer")]
        public async Task<IActionResult> UpdateCustomerInXero([FromBody] UpdateCustomerInXeroDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.ContactID))
                    return BadRequest("ContactID is required.");

                await _xeroService.UpdateCustomerInXeroAsync(dto);
                return Ok("Customer updated successfully in Xero and saved to local DB.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Update failed: {ex.Message}");
            }
        }



        [HttpPost("archive-contact/{contactId}")]
        public async Task<IActionResult> ArchiveContact(string contactId)
        {
            try
            {
                var result = await _xeroService.ArchiveContactAsync(contactId);
                return Ok($"Contact {contactId} archived successfully. Response: {result}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Archiving failed: {ex.Message}");
            }
        }


        [HttpGet("get-xero-customers")]
        public async Task<IActionResult> GetXeroCustomers()
        {
            try
            {
                var customers = await _xeroService.GetXeroCustomersAsync();
                return Ok(customers);
            }
            catch (Exception ex)
            {
               
                return StatusCode(500, "An error occurred while fetching Xero customers.");
            }
        }

    }
}
