using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Text;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class CSVParseService
    {
        private readonly ApplicationDbContext _context;

        public CSVParseService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task ParseAndSaveAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("CSV file is empty.");

            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            var records = csv.GetRecords<CSVParse>().ToList();

            _context.CSVParses.AddRange(records);
            await _context.SaveChangesAsync();
        }
    }
}
