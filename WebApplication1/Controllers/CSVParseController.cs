using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
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
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        var allowedExtensions = new[] { ".csv" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Only CSV files are allowed." });
        }

        try
        {
            var (success, errors) = await _csvParseService.ParseAndSaveAsync(file);

            if (!success)
            {
                return BadRequest(new { message = "Validation failed.", errors });
            }

            return Ok(new { message = "CSV data processed successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
