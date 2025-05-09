using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace QBO.Interface
{
    public interface IBillService
    {
        Task<List<Bill>> FetchAndStoreAllBillsAsync();
        Task<(List<Bill> Data, int TotalCount)> GetPagedBillsAsync(string searchTerm, int page, int pageSize);
        Task<object> AddBillToQboAsync(CreateBillDto dto);
    }
}
