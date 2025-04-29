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
using System.Net.Http.Headers;
using WebApplication1.Models.Xero.WebApplication1.Dtos;
using Newtonsoft.Json.Linq;

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


        public async Task<string> AddCustomerToXeroAsync(AddCustomerToXeroDto dto)
        {
            var (accessToken, tenantId) = await GetXeroAuthDetailsAsync();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Add("Xero-Tenant-Id", tenantId);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var xeroPayload = new
            {
                Contacts = new[]
     {
        new
        {
            Name = dto.DisplayName ?? $"{dto.GivenName} {dto.FamilyName}".Trim(),
            EmailAddress = dto.Email,
            Phones = dto.Phones?.Select(p => new
            {
                PhoneType = p.PhoneType,
                PhoneNumber = p.PhoneNumber,
                PhoneAreaCode = p.PhoneAreaCode,
                PhoneCountryCode = p.PhoneCountryCode ?? ""
            }).ToList(),
            Addresses = dto.Addresses?.Select(a => new
            {
                AddressType = a.AddressType,
                City = a.City ?? "",
                Region = a.Region ?? "",
                PostalCode = a.PostalCode ?? "",
                Country = a.Country ?? ""
            }).ToList()
        }
    }
            };


            var jsonPayload = JsonConvert.SerializeObject(xeroPayload);
            var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("https://api.xero.com/api.xro/2.0/Contacts", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Xero API Error: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            dynamic xeroResponse = JsonConvert.DeserializeObject(responseContent);

            string contactId = xeroResponse?.Contacts[0]?.ContactID;
            if (string.IsNullOrWhiteSpace(contactId))
            {
                throw new Exception("Xero ContactID missing from response.");
            }

            // Map to Customer entity for DB
            var customer = new Customer
            {
                ContactID = contactId,
                DisplayName = dto.DisplayName,
                GivenName = dto.GivenName,
                FamilyName = dto.FamilyName,
                Email = dto.Email,
                Phones = JsonConvert.SerializeObject(dto.Phones),
                Addresses = JsonConvert.SerializeObject(dto.Addresses),
                Company = "Xero",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            return contactId;
        }

        public async Task<bool> UpdateCustomerInXeroAsync(UpdateCustomerInXeroDto dto)
        {
            var (accessToken, tenantId) = await GetXeroAuthDetailsAsync();

            var xeroPayload = new
            {
                Contacts = new[]
                {
                new
                {
                    ContactID = dto.ContactID,
                    Name = dto.DisplayName ?? $"{dto.GivenName} {dto.FamilyName}".Trim(),
                    EmailAddress = dto.Email,
                    Phones = dto.Phones?.Select(p => new
                    {
                        PhoneType = p.PhoneType,
                        PhoneNumber = p.PhoneNumber,
                        PhoneAreaCode = p.PhoneAreaCode,
                        PhoneCountryCode = p.PhoneCountryCode ?? ""
                    }).ToList(),
                    Addresses = dto.Addresses?.Select(a => new
                    {
                        AddressType = a.AddressType,
                        City = a.City ?? "",
                        Region = a.Region ?? "",
                        PostalCode = a.PostalCode ?? "",
                        Country = a.Country ?? ""
                    }).ToList()
                }
            }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.xero.com/api.xro/2.0/Contacts/{dto.ContactID}")
            {
                Content = new StringContent(JsonConvert.SerializeObject(xeroPayload), Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("Xero-Tenant-Id", tenantId);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Xero update failed: {response.StatusCode} - {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            var contact = json["Contacts"]?.FirstOrDefault();

            if (contact != null)
            {
                var email = contact["EmailAddress"]?.ToString();
                var existingCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.ContactID == dto.ContactID);

                if (existingCustomer != null)
                {
                    existingCustomer.DisplayName = contact["Name"]?.ToString();
                    existingCustomer.Email = email;
                    existingCustomer.ContactID = dto.ContactID;
                    existingCustomer.Phones = JsonConvert.SerializeObject(dto.Phones);
                    existingCustomer.Addresses = JsonConvert.SerializeObject(dto.Addresses);
                    existingCustomer.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                }
            }

            return true;
        }


        public async Task<string> ArchiveContactAsync(string contactId)
        {
            var (accessToken, tenantId) = await GetXeroAuthDetailsAsync();

            var payload = new
            {
                Contacts = new[]
                {
                new
                {
                    ContactID = contactId,
                    ContactStatus = "ARCHIVED"
                }
            }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.xero.com/api.xro/2.0/Contacts/{contactId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("Xero-Tenant-Id", tenantId);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Archiving in Xero failed: {response.StatusCode} - {errorContent}");
            }

            return await response.Content.ReadAsStringAsync();
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
