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

namespace WebApplication1.Services
{
    public class XeroService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public XeroService(IConfiguration configuration, ApplicationDbContext context)
        {
            _configuration = configuration;
            _context = context;
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

                // Deserialize the response content
                var responseContent = await response.Content.ReadAsStringAsync();
                var xeroContacts = JsonSerializer.Deserialize<XeroContactResponse>(responseContent);

                if (xeroContacts?.Contacts != null)
                {
                    foreach (var xeroContact in xeroContacts.Contacts)
                    {
                        var existingCustomer = await _context.Customers
                            .FirstOrDefaultAsync(c => c.ContactID == xeroContact.ContactID);

                        if (existingCustomer != null)
                        {
                            // Update the existing customer
                            existingCustomer.DisplayName = xeroContact.Name ?? string.Empty;
                            existingCustomer.GivenName = xeroContact.FirstName ?? string.Empty;
                            existingCustomer.FamilyName = xeroContact.LastName ?? string.Empty;
                            existingCustomer.Email = xeroContact.EmailAddress ?? string.Empty;

                            // Concatenate phone numbers into a comma-separated string
                            existingCustomer.Phones = xeroContact.Phones != null ? string.Join(", ", xeroContact.Phones.Select(p => p.PhoneNumber ?? string.Empty)) : string.Empty;

                            // Handle addresses (using JSON string or comma-separated string)
                            if (xeroContact.Addresses != null)
                            {
                                // Convert addresses to JSON format
                                existingCustomer.Addresses = JsonSerializer.Serialize(xeroContact.Addresses);
                            }
                            else
                            {
                                existingCustomer.Addresses = string.Empty;
                            }

                            // Update other fields
                            existingCustomer.Active = true;
                            existingCustomer.UpdatedAt = DateTime.UtcNow;
                        }
                        else
                        {
                            // Mapping Xero Contact data to the local Customer model
                            var newCustomer = new Customer
                            {
                                ContactID = xeroContact.ContactID,
                                DisplayName = xeroContact.Name ?? string.Empty,
                                GivenName = xeroContact.FirstName ?? string.Empty,
                                FamilyName = xeroContact.LastName ?? string.Empty,
                                Email = xeroContact.EmailAddress ?? string.Empty,

                                // Concatenate phone numbers into a comma-separated string
                                Phones = xeroContact.Phones != null ? string.Join(", ", xeroContact.Phones.Select(p => p.PhoneNumber ?? string.Empty)) : string.Empty,

                                // Handle addresses (using JSON string or comma-separated string)
                                Addresses = xeroContact.Addresses != null ? JsonSerializer.Serialize(xeroContact.Addresses) : string.Empty,

                                Active = true,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                Company = "Xero"
                            };

                            // Add the new customer to the context
                            _context.Customers.Add(newCustomer);
                        }
                    }

                    try
                    {
                        // Save changes to the database
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
