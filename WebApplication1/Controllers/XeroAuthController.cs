using Microsoft.AspNetCore.Mvc;
using XeroLayer.Interface;

[Route("api/[controller]")]
[ApiController]
public class XeroAuthController : ControllerBase
{
    private readonly IXeroAuthService _xeroAuthService;

    public XeroAuthController(IXeroAuthService xeroAuthService)
    {
        _xeroAuthService = xeroAuthService;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        var authUrl = _xeroAuthService.BuildAuthorizationUrl();
        return Redirect(authUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        var token = await _xeroAuthService.ExchangeCodeForTokenAsync(code, state);

        if (token != null)
        {
            return Redirect("http://localhost:5173/home");
        }
        else
        {
            return BadRequest("Xero authorization failed.");
        }
    }
}
