using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebApplication1.Models.Xero.WebApplication1.Dtos;
using WebApplication1.Models;

namespace XeroLayer.Interfaces
{
    public interface IXeroCustomerService
    {
        Task SyncXeroContactsAsync();
        Task<(string accessToken, string tenantId)> GetXeroAuthDetailsAsync();
        Task<string> AddCustomerToXeroAsync(AddCustomerToXeroDto dto);
        Task<bool> UpdateCustomerInXeroAsync(UpdateCustomerInXeroDto dto);
        Task<string> ArchiveContactAsync(string contactId);
        Task<List<Customer>> GetXeroCustomersAsync();
    }
}

