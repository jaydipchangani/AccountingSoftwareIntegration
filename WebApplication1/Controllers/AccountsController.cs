using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;
using Businesslayer.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly AccountService _accountService;

        public AccountController(AccountService accountService)
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
