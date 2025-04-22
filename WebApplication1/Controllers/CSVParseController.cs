using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CSVParseController : ControllerBase
    {
        private readonly CSVParseService _csvParseService;
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public CSVParseController(CSVParseService csvParseService, ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _csvParseService = csvParseService;
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadCsv(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded." });
            }

            var allowedContentTypes = new[] { "text/csv", "application/vnd.ms-excel" };
            var allowedExtensions = new[] { ".csv" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension) || !allowedContentTypes.Contains(file.ContentType))
            {
                return BadRequest(new { message = "Only CSV files are allowed." });
            }

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
