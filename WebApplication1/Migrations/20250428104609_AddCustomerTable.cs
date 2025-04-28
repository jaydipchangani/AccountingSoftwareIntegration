using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RealmId",
                table: "QuickBooksTokens",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "QuickBooksUserId",
                table: "QuickBooksTokens",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<double>(
                name: "ExpiresIn",
                table: "QuickBooksTokens",
                type: "float",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Company",
                table: "QuickBooksTokens",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "QuickBooksTokens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "QuickBooksTokens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "QuickBooksTokens",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "QuickBooksTokens",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyncToken",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Addresses",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountDetails",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Company",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactID",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "Customers",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phones",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxNumber",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XeroNetworkKey",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyName",
                table: "ChartOfAccounts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Company",
                table: "ChartOfAccounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CSVParses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CSVParses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QboItemId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LineNum = table.Column<int>(type: "int", nullable: true),
                    DetailType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ItemRef = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Vendor1099 = table.Column<bool>(type: "bit", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrimaryEmailAddr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrimaryPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrencyValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrencyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillAddrLine1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillAddrCity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillAddrPostalCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SyncToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    V4IDPseudonym = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WebAddr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "XeroTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccessToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TokenType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XeroTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QboBillId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    VendorAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DocNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TxnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    APAccountName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SyncToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bills_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillLineItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    ItemId = table.Column<int>(type: "int", nullable: true),
                    DetailType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillLineItems_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillLineItems_BillId",
                table: "BillLineItems",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_VendorId",
                table: "Bills",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillLineItems");

            migrationBuilder.DropTable(
                name: "CSVParses");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "XeroTokens");

            migrationBuilder.DropTable(
                name: "Bills");

            migrationBuilder.DropTable(
                name: "Vendors");

            migrationBuilder.DropColumn(
                name: "Company",
                table: "QuickBooksTokens");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "QuickBooksTokens");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "QuickBooksTokens");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "QuickBooksTokens");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "QuickBooksTokens");

            migrationBuilder.DropColumn(
                name: "SyncToken",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Addresses",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BankAccountDetails",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Company",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ContactID",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Phones",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TaxNumber",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "XeroNetworkKey",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Company",
                table: "ChartOfAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "RealmId",
                table: "QuickBooksTokens",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "QuickBooksUserId",
                table: "QuickBooksTokens",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ExpiresIn",
                table: "QuickBooksTokens",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyName",
                table: "ChartOfAccounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
