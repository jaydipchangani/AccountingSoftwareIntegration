using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class XeroAccountsController : ControllerBase
{
    private readonly XeroAccountService _xeroAccountService;

    public XeroAccountsController(XeroAccountService xeroAccountService)
    {
        _xeroAccountService = xeroAccountService;
    }

    // Fetch accounts from Xero and map them to the local ChartOfAccount model
    [HttpGet("fetch-chart-of-accounts-from-xero")]
    public async Task<IActionResult> FetchAccountsFromXero()
    {
        try
        {
            var accounts = await _xeroAccountService.FetchAccountsFromXeroAsync();

            // Returning the mapped ChartOfAccount list from Xero
            return Ok(accounts);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message); // Internal server error
        }
    }
}
