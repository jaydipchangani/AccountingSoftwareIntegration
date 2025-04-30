using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class XeroAuthController : ControllerBase
{
    private readonly XeroAuthService _xeroService;

    public XeroAuthController(XeroAuthService xeroService)
    {
        _xeroService = xeroService;
    }

    // Redirect user to Xero login page for OAuth
    [HttpGet("login")]
    public IActionResult Login()
    {
        var authUrl = _xeroService.BuildAuthorizationUrl();
        return Redirect(authUrl); // Redirect to Xero authorization page
    }

    // Xero OAuth callback to exchange the code for an access token
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        try
        {
            var result = await _xeroService.ExchangeCodeForTokenAsync(code, state);

            // After successful exchange, redirect to the homepage or a relevant page
            return Redirect("http://localhost:5173/home");
        }
        catch (Exception ex)
        {
            // In case of error during the token exchange
            return StatusCode(500, ex.Message);
        }
    }


    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        var success = await _xeroService.LogoutFromXeroAsync();

        if (!success)
        {
            return NotFound(new { message = "No Xero token found to logout." });
        }

        return Ok(new { message = "Successfully logged out from Xero." });
    }

}
