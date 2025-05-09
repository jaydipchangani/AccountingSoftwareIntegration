using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace XeroLayer.Interface
{
    public interface IXeroAccountService
    {
        Task<List<ChartOfAccount>> FetchAccountsFromXeroAsync();
    }
}
