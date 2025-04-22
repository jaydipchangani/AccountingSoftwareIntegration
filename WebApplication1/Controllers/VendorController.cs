using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using WebApplication1.Models;
using WebApplication1.Data;
using WebApplication1.Services;

[ApiController]
[Route("api/[controller]")]
public class VendorController : ControllerBase
{
    private readonly VendorSyncService _vendorSyncService;
    private readonly VendorService _vendorService;

    public VendorController(VendorSyncService vendorSyncService, VendorService vendorService)
    {
        _vendorSyncService = vendorSyncService;
        _vendorService = vendorService;
    }

    [HttpGet("sync")]
    public async Task<IActionResult> SyncVendors()
    {
        try
        {
            var result = await _vendorSyncService.SyncVendorsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error during sync: {ex.Message}");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetVendors([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
    {
        var (vendors, totalCount) = await _vendorService.GetVendorsAsync(pageNumber, pageSize, search);

        return Ok(new
        {
            data = vendors,
            totalCount = totalCount
        });
    }

    [HttpPost("addVendor")]
    public async Task<IActionResult> AddVendor([FromBody] CreateVendorDto createVendorDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // Convert CreateVendorDto to Vendor model
            var vendor = new Vendor
            {
                DisplayName = createVendorDto.DisplayName,
                PrimaryEmailAddr = createVendorDto.PrimaryEmailAddr,
                PrimaryPhone = createVendorDto.PrimaryPhone,
                WebAddr = createVendorDto.WebAddr,
                BillAddrLine1 = createVendorDto.BillAddrLine1,
                BillAddrCity = createVendorDto.BillAddrCity,
                BillAddrPostalCode = createVendorDto.BillAddrPostalCode,
                Active = true, // Default to active
                Vendor1099 = false, // Default to not 1099
                Balance = 0, // Default balance
                CreateTime = DateTime.UtcNow,
                LastUpdatedTime = DateTime.UtcNow
            };

            var result = await _vendorService.AddVendorAsync(vendor);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("{vId}")]
    public async Task<IActionResult> GetVendorByVId(string vId)
    {
        try
        {
            var vendor = await _vendorService.GetVendorByVIdAsync(vId);
            return Ok(vendor);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }





}








