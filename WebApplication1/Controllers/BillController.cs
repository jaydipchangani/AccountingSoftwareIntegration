using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services; // Make sure to include the correct namespace for BillService

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BillController : ControllerBase
    {
        private readonly BillService _billService;

        public BillController(BillService billService)
        {
            _billService = billService;
        }

        /// <summary>
        /// Sync all bills from QuickBooks to the local database.
        /// </summary>
        [HttpGet("sync-from-qbo")]
        public async Task<IActionResult> SyncBillsFromQuickBooks()
        {
            try
            {
                var syncedBills = await _billService.FetchAndStoreAllBillsAsync();
                if (syncedBills == null || !syncedBills.Any())
                {
                    return Ok(new
                    {
                        Message = "No bills were synced from QuickBooks.",
                        Data = new List<object>()
                    });
                }

                return Ok(new
                {
                    Message = $"{syncedBills.Count} bills synced from QuickBooks.",
                    Data = syncedBills
                });
            }
            catch (Exception ex)
            {
                // Add proper logging here if needed
                return StatusCode(500, new
                {
                    Message = "An error occurred while syncing bills from QuickBooks.",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get paged and filtered list of bills from the database.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetBills(
            [FromQuery] string search = "",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 5)
        {
            try
            {
                var (bills, total) = await _billService.GetPagedBillsAsync(search, page, pageSize);
                return Ok(new
                {
                    Data = bills,
                    TotalCount = total,
                    Page = page,
                    PageSize = pageSize
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to retrieve bills.",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Add a new bill to QuickBooks.
        /// </summary>
        [HttpPost("add-to-qbo")]
        public async Task<IActionResult> AddBillToQbo([FromBody] CreateBillDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    Message = "Invalid request payload.",
                    Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });

            try
            {
                var result = await _billService.AddBillToQboAsync(dto);
                if (result == null)
                {
                    return StatusCode(500, new
                    {
                        Message = "Failed to add bill to QuickBooks."
                    });
                }

                return Ok(new
                {
                    Message = "Bill successfully added to QuickBooks.",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Message = "An error occurred while adding bill to QuickBooks.",
                    Error = ex.Message
                });
            }
        }
    }
}
