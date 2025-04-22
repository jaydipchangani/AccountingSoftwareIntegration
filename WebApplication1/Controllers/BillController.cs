using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

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

        [HttpGet("sync-from-qbo")]
        public async Task<IActionResult> SyncBillsFromQuickBooks()
        {
            try
            {  var syncedBills = await _billService.FetchAndStoreAllBillsAsync();
                return Ok(new
                {
                    Message = $"{syncedBills.Count} bills synced from QuickBooks.",
                    Data = syncedBills
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Message = "Error syncing bills",
                    Error = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBills([FromQuery] string search = "", [FromQuery] int page = 1, [FromQuery] int pageSize = 5)
        {
            var (bills, total) = await _billService.GetPagedBillsAsync(search, page, pageSize);

            return Ok(new
            {
                data = bills,
                total,
                page,
                pageSize
            });
        }

        [HttpPost("add-to-qbo")]
        public async Task<IActionResult> AddBillToQbo([FromBody] CreateBillDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _billService.AddBillToQboAsync(dto);
            return Ok(result);
        }

    }
}
