using Microsoft.AspNetCore.Mvc;
using QBO.QBOAuth;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet("login")]
        public IActionResult Login() => _authService.GenerateLoginRedirect();

        [HttpPost("exchange")]
        public async Task<IActionResult> ExchangeCode([FromBody] ExchangeRequest request) =>
            await _authService.ExchangeCodeAsync(request);

        [HttpDelete("logout")]
        public async Task<IActionResult> Logout() => await _authService.LogoutAsync();

        [HttpGet("token-status")]
        public IActionResult GetTokenStatus() => _authService.GetTokenStatus();
    }
}
