using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;

[ApiController]
[Route("api/[controller]")]
public class XeroAccountsController : ControllerBase
{
    private readonly XeroAccountService _xeroAccountService;

    public XeroAccountsController(XeroAccountService xeroAccountService)
    {
        _xeroAccountService = xeroAccountService;
    }

    [HttpGet("fetch")]
    public async Task<IActionResult> FetchAccounts([FromQuery] string accessToken, [FromQuery] string tenantId)
    {
        var accounts = await _xeroAccountService.FetchAccountsAsync(accessToken, tenantId);
        return Ok(accounts);
    }
}
