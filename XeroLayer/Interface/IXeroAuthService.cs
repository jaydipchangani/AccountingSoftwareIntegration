using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WebApplication1.Models;
using WebApplication1.Models.Xero;

namespace XeroLayer.Interface
{
    public interface IXeroAuthService
    {
        Task<XeroToken> GetXeroAuthDetailsAsync();
        string BuildAuthorizationUrl();
        Task<QuickBooksToken> ExchangeCodeForTokenAsync(string code, string state);
        Task<bool> LogoutFromXeroAsync();
    }
}
