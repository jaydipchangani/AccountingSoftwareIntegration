using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplication1.Models;
using Microsoft.Extensions.Configuration;
using System.Linq;
using WebApplication1.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text;

namespace WebApplication1.Services
{
    public class XeroService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;

        public XeroService(IConfiguration configuration, ApplicationDbContext context,HttpClient httpClient)
        {
            _configuration = configuration;
            _context = context;
            _httpClient = httpClient;
        }

        public async Task SyncXeroContactsAsync()
        {
            // Get the Xero Token, Tenant ID, and Scope from the database
            var xeroToken = await _context.QuickBooksTokens
                .Where(t => t.Company == "Xero")
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (xeroToken == null)
            {
                throw new Exception("Xero token not found.");
            }

            var token = xeroToken.AccessToken;
            var tenantId = xeroToken.TenantId;
            var scope = xeroToken.Scope;

            using (var client = new HttpClient())
            {
                // Set authorization and tenant headers
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                client.DefaultRequestHeaders.Add("Xero-Tenant-Id", tenantId);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                // Make the API call to Xero
                var response = await client.GetAsync("https://api.xero.com/api.xro/2.0/Contacts");
                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Xero API request failed: {response.StatusCode} - {errorResponse}");
                }

                // Deserialize the response content using Newtonsoft.Json
                var responseContent = await response.Content.ReadAsStringAsync();
                var xeroContacts = JsonConvert.DeserializeObject<XeroContactResponse>(responseContent);

                if (xeroContacts?.Contacts != null)
                {
                    foreach (var xeroContact in xeroContacts.Contacts)
                    {
                        var existingCustomer = await _context.Customers
                            .FirstOrDefaultAsync(c => c.ContactID == xeroContact.ContactID);

                        // Try to get POBOX address first, otherwise use STREET
                        var address = xeroContact.Addresses?.FirstOrDefault(a => a.AddressType == "POBOX")
                                   ?? xeroContact.Addresses?.FirstOrDefault(a => a.AddressType == "STREET");

                        string line1 = address?.AddressLine ?? string.Empty;
                        string city = address?.City ?? string.Empty;
                        string region = address?.Region ?? string.Empty;
                        string postalCode = address?.PostalCode ?? string.Empty;
                        string country = address?.Country ?? string.Empty;

                        if (existingCustomer != null)
                        {
                            existingCustomer.DisplayName = xeroContact.Name ?? string.Empty;
                            existingCustomer.GivenName = xeroContact.FirstName ?? string.Empty;
                            existingCustomer.FamilyName = xeroContact.LastName ?? string.Empty;
                            existingCustomer.Email = xeroContact.EmailAddress ?? string.Empty;
                            existingCustomer.Phone = xeroContact.Phones != null ? string.Join(", ", xeroContact.Phones.Select(p => p.PhoneNumber ?? string.Empty)) : string.Empty;

                            // Map address fields to Billing info
                            existingCustomer.BillingLine1 = line1;
                            existingCustomer.BillingCity = city;
                            existingCustomer.BillingState = region;
                            existingCustomer.BillingPostalCode = postalCode;
                            existingCustomer.BillingCountry = country;

                            existingCustomer.Active = true;
                            existingCustomer.UpdatedAt = DateTime.UtcNow;
                        }
                        else
                        {
                            var newCustomer = new Customer
                            {
                                ContactID = xeroContact.ContactID,
                                DisplayName = xeroContact.Name ?? string.Empty,
                                GivenName = xeroContact.FirstName ?? string.Empty,
                                FamilyName = xeroContact.LastName ?? string.Empty,
                                Email = xeroContact.EmailAddress ?? string.Empty,
                                Phone = xeroContact.Phones != null ? string.Join(", ", xeroContact.Phones.Select(p => p.PhoneNumber ?? string.Empty)) : string.Empty,

                                // Map address fields to Billing info
                                BillingLine1 = line1,
                                BillingCity = city,
                                BillingState = region,
                                BillingPostalCode = postalCode,
                                BillingCountry = country,

                                Active = true,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                Company = "Xero"
                            };

                            _context.Customers.Add(newCustomer);
                        }
                    }

                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        var innerException = ex.InnerException != null ? ex.InnerException.Message : "No inner exception";
                        throw new Exception($"Error syncing Xero contacts: {ex.Message} - Inner Exception: {innerException}");
                    }
                }

            }
        }


        // This method retrieves Xero token and tenant ID from the QuickBooksTokens table
        public async Task<(string accessToken, string tenantId)> GetXeroAuthDetailsAsync()
        {
            // Fetch the most recent Xero token and tenant ID from the database
            var xeroToken = await _context.QuickBooksTokens
                .Where(t => t.Company == "Xero")
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (xeroToken == null)
            {
                throw new Exception("Xero token not found.");
            }

            // Return the access token and tenant ID as a tuple
            return (xeroToken.AccessToken, xeroToken.TenantId);
        }

        public async Task DeactivateContactAsync(string contactId)
        {
            // Retrieve the Xero authentication details
            var (accessToken, tenantId) = await GetXeroAuthDetailsAsync();

            // Define the endpoint URL for Xero's API to update the contact
            var url = $"https://api.xero.com/api.xro/2.0/Contacts/{contactId}";

            // Define the payload to deactivate the contact (set Active to false)
            var payload = new
            {
                Contacts = new[]
                {
                new { Active = false } // Deactivate the contact
            }
            };

            // Serialize the payload using Newtonsoft.Json (JsonConvert)
            var jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Create an HttpRequestMessage to set the headers and body
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content // Attach the content (body) to the request
            };

            // Set the required headers on the HttpRequestMessage
            requestMessage.Headers.Add("Authorization", $"Bearer {accessToken}");
            requestMessage.Headers.Add("Xero-Tenant-Id", tenantId);
            requestMessage.Headers.Add("Accept", "application/json");

            // Make the POST request to Xero's API
            var response = await _httpClient.SendAsync(requestMessage);

            // Check the response status and handle accordingly
            if (response.IsSuccessStatusCode)
            {
                // Log or return a success message
                Console.WriteLine("Contact deactivated successfully.");
            }
            else
            {
                // Handle any errors from the API response
                var errorMessage = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error deactivating contact: {response.StatusCode} - {errorMessage}");
            }
        }

    }

    public class XeroContactResponse
    {
        public List<XeroContact> Contacts { get; set; }
    }

    public class XeroContact
    {
        public string ContactID { get; set; }
        public string Name { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmailAddress { get; set; }
        public List<XeroPhone> Phones { get; set; }
        public List<XeroAddress> Addresses { get; set; }
    }

    public class XeroPhone
    {
        public string PhoneNumber { get; set; }
    }

    public class XeroAddress
    {
        public string AddressType { get; set; }
        public string AddressLine { get; set; }
        public string City { get; set; }
        public string Region { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
    }
}
