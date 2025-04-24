using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebApplication1.Services;

[ApiController]
[Route("api/[controller]")]
public class XeroAuthController : ControllerBase
{
    private readonly XeroAuthService _xeroService;

    public XeroAuthController(XeroAuthService xeroService)
    {
        _xeroService = xeroService;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        var authUrl = _xeroService.BuildAuthorizationUrl();
        return Redirect(authUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        var result = await _xeroService.ExchangeCodeForTokenAsync(code,state);
        return Redirect("http://localhost:5173/home");
    }
}
