using DataLayer.Models;

namespace Businesslayer.Services
{
    public interface IAccountService
    {
        Task<List<Account>> SyncAccountsAsync();
    }
}
