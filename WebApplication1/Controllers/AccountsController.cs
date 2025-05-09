using Microsoft.AspNetCore.Mvc;
using Businesslayer.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpGet("sync")]
        public async Task<IActionResult> SyncAccounts()
        {
            var accounts = await _accountService.SyncAccountsAsync();
            return Ok(accounts);
        }
    }
}
