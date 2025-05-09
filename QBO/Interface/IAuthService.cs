using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace QBO.QBOAuth
{
    public interface IAuthService
    {
        IActionResult GenerateLoginRedirect();
        Task<IActionResult> ExchangeCodeAsync(ExchangeRequest request);
        Task<IActionResult> LogoutAsync();
        IActionResult GetTokenStatus();
    }
}

