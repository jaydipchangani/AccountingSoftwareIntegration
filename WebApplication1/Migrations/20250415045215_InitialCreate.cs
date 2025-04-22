using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QbId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountSubType = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChartOfAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuickBooksAccountId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountSubType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Classification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrencyValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrencyName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuickBooksUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartOfAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuickBooksCustomerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QuickBooksUserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    GivenName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MiddleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FamilyName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Suffix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    BillingLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BillingCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BillingState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BillingPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BillingCountry = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ShippingLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ShippingCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShippingState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ShippingPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ShippingCountry = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PreferredDeliveryMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PrintOnCheckName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    QuickBooksCreateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QuickBooksLastUpdateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuickBooksId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocNumber = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TxnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Store = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillingAddressJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShippingAddressJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subtotal = table.Column<float>(type: "real", nullable: true),
                    TotalAmt = table.Column<float>(type: "real", nullable: true),
                    Balance = table.Column<float>(type: "real", nullable: true),
                    SendLater = table.Column<bool>(type: "bit", nullable: true),
                    CustomerMemo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrintStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SyncToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IncomeAccount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetAccount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpenseAccount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuantityOnHand = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AsOfDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    QuickBooksItemId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuickBooksUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IncomeAccountId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetAccountId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpenseAccountId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuickBooksTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuickBooksUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RealmId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccessToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TokenType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiresIn = table.Column<int>(type: "int", nullable: false),
                    XRefreshTokenExpiresIn = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuickBooksTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_line_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    Rate = table.Column<float>(type: "real", nullable: true),
                    Amount = table.Column<float>(type: "real", nullable: true),
                    LineNum = table.Column<int>(type: "int", nullable: true),
                    DetailType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_line_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoice_line_items_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_line_items_InvoiceId",
                table: "invoice_line_items",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_DocNumber",
                table: "invoices",
                column: "DocNumber",
                unique: true,
                filter: "[DocNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_QuickBooksId",
                table: "invoices",
                column: "QuickBooksId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "ChartOfAccounts");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "invoice_line_items");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "QuickBooksTokens");

            migrationBuilder.DropTable(
                name: "invoices");
        }
    }
}
