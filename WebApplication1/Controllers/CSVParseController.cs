using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CSVParseController : ControllerBase
    {
        private readonly CSVParseService _csvParseService;

        public CSVParseController(CSVParseService csvParseService)
        {
            _csvParseService = csvParseService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadCsv(IFormFile file)
        {
            try
            {
                await _csvParseService.ParseAndSaveAsync(file);
                return Ok(new { message = "CSV data parsed and saved successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
