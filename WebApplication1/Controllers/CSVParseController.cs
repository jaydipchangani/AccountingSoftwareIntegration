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

    [HttpPost("upload-csv")]
    public async Task<IActionResult> UploadCSVFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        var allowedExtensions = new[] { ".csv" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { message = "Only CSV files are allowed." });

        try
        {

            var (success, errors) = await _csvParseService.ParseAndSaveAsync(file);

            if (!success)
                return BadRequest(new { message = "Validation failed.", errors });

            try
            {
                await _csvParseService.SyncCustomersAsync();
            }
            catch (Exception customerEx)
            {
                return StatusCode(500, new { message = "Error while syncing customers.", error = customerEx.Message });
            }

            try
            {
                await _csvParseService.SyncProductsAsync();
            }
            catch (Exception productEx)
            {
                return StatusCode(500, new { message = "Error while syncing products.", error = productEx.Message });
            }

            try
            {
                await _csvParseService.SyncInvoicesAsync();
            }
            catch (Exception InvoiceEx)
            {
                return StatusCode(500, new { message = "Error while syncing Invoice.", error = InvoiceEx.Message });
            }

            return Ok(new { message = "CSV parsed and synced successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Unexpected error during file processing.", error = ex.Message });
        }
    }

}
