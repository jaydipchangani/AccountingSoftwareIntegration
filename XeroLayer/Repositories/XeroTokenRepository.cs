using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using DataLayer.Models;
using System.Linq;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace XeroLayer.XeroClient
{
    public class XeroTokenRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public XeroTokenRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Get a specific Xero token by company (Xero)
        public async Task<QuickBooksToken> GetTokenByCompanyAsync(string company)
        {
            return await _dbContext.QuickBooksTokens
                .FirstOrDefaultAsync(t => t.Company == company);
        }

        // Add or update Xero token in the database
        public async Task AddOrUpdateTokenAsync(QuickBooksToken token)
        {
            var existingToken = await GetTokenByCompanyAsync(token.Company);

            if (existingToken != null)
            {
                existingToken.AccessToken = token.AccessToken;
                existingToken.RefreshToken = token.RefreshToken;
                existingToken.IdToken = token.IdToken;
                existingToken.TokenType = token.TokenType;
                existingToken.Scope = token.Scope;
                existingToken.ExpiresIn = token.ExpiresIn;
                existingToken.ExpiresAtUtc = token.ExpiresAtUtc;
                existingToken.CreatedAtUtc = token.CreatedAtUtc;
                existingToken.TenantId = token.TenantId;
            }
            else
            {
                _dbContext.QuickBooksTokens.Add(token);
            }

            await _dbContext.SaveChangesAsync();
        }

        // Remove a token by company (Xero)
        public async Task RemoveTokenAsync(string company)
        {
            var token = await GetTokenByCompanyAsync(company);
            if (token != null)
            {
                _dbContext.QuickBooksTokens.Remove(token);
                await _dbContext.SaveChangesAsync();
            }
        }
    }
}
