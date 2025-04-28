using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace XeroIntegration.Models
{
    public class ChartOfAccount
    {
        [Key]
        [JsonPropertyName("internalId")]
        public int Id { get; set; } 

        [JsonPropertyName("id")]
        public string QuickBooksAccountId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("accountType")]
        public string AccountType { get; set; }

        [JsonPropertyName("accountSubType")]
        public string AccountSubType { get; set; }

        [JsonPropertyName("currentBalance")]
        public decimal? CurrentBalance { get; set; }

        [JsonPropertyName("classification")]
        public string Classification { get; set; }

        [JsonPropertyName("currencyRef")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CurrencyRef CurrencyRef { get; set; }

        public string? Company { get; set; }
        public string CurrencyValue { get; set; }
        public string? CurrencyName { get; set; }

        public string QuickBooksUserId { get; set; }
        public DateTime CreatedAt { get; set; }

        public void UpdateCurrencyRefProperties()
        {
            if (CurrencyRef != null)
            {
                CurrencyValue = CurrencyRef.Value;
                CurrencyName = CurrencyRef.Name;
            }
        }
    }

    [NotMapped]
    public class CurrencyRef
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}