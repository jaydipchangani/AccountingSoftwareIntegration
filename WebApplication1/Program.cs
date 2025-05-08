using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WebApplication1.Services;
using WebApplication1.Models.Xero;
using Businesslayer.Services;
using DataLayer.Models;
using XeroLayer;


var builder = WebApplication.CreateBuilder(args);


builder.Services.Configure<XeroApiOptions>(builder.Configuration.GetSection("XeroApi"));
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddHttpClient<ProductService>();


builder.Services.AddScoped<TokenService>();

builder.Services.AddScoped<XeroInvoiceService>();

builder.Services.AddScoped<VendorSyncService>();
builder.Services.AddScoped<VendorService>();
builder.Services.AddScoped<BillService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<CSVParseService>();
builder.Services.AddHttpClient<XeroAuthService>();
builder.Services.AddScoped<XeroAuthService>();
builder.Services.AddHttpClient<XeroAccountService>(); // registers with HttpClient
builder.Services.AddScoped<XeroAccountService>();      // registers as a scoped service




builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,                     // number of retry attempts
            maxRetryDelay: TimeSpan.FromSeconds(10), // delay between retries
            errorNumbersToAdd: null               // you can add specific SQL error codes if needed
        )
    )
);


// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173") // Frontend origin
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // optional if you are using cookies/auth headers
    });
});
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter JWT token as: Bearer {your token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHttpClient();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ✅ Add this before `UseAuthorization`
app.UseCors("AllowFrontend");
app.UseAuthorization();

app.MapControllers();

app.Run();
