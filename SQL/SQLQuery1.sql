create database QuickBookDB
use QuickBookDB

CREATE TABLE QuickBooksTokens (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    QuickBooksUserId NVARCHAR(255) NOT NULL,
    RealmId NVARCHAR(50) NOT NULL,
    AccessToken NVARCHAR(MAX) NOT NULL,
    RefreshToken NVARCHAR(MAX) NOT NULL,
    IdToken NVARCHAR(MAX) NULL,
    TokenType NVARCHAR(20) NOT NULL,
    ExpiresIn INT NOT NULL,
    XRefreshTokenExpiresIn INT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME NULL
);

CREATE TABLE ChartOfAccounts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    QuickBooksAccountId NVARCHAR(MAX) NOT NULL,
    Name NVARCHAR(MAX) NOT NULL,
    AccountType NVARCHAR(MAX) NOT NULL,
    AccountSubType NVARCHAR(MAX) NOT NULL,
    CurrentBalance DECIMAL(18, 2) NULL,
    CurrencyValue NVARCHAR(MAX) NOT NULL,
    CurrencyName NVARCHAR(MAX) NOT NULL,
    Classification NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    QuickBooksUserId NVARCHAR(MAX) NOT NULL
);

select @@SERVERNAME



ALTER TABLE ChartOfAccounts
ALTER COLUMN CurrencyValue NVARCHAR(MAX) NULL;

ALTER TABLE ChartOfAccounts
ALTER COLUMN CurrencyName NVARCHAR(MAX) NULL;

truncate table  ChartOfAccounts
truncate table  QuickBooksTokens
truncate table Customers

CREATE TABLE Customers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    QuickBooksCustomerId VARCHAR(20),
    QuickBooksUserId VARCHAR(50), -- link to token table if needed
    DisplayName NVARCHAR(150),
    CompanyName NVARCHAR(150),
    GivenName NVARCHAR(50),
    MiddleName NVARCHAR(50),
    FamilyName NVARCHAR(50),
    Email NVARCHAR(100),
    Phone NVARCHAR(30),
    
    BillingLine1 NVARCHAR(200),
    BillingCity NVARCHAR(100),
    BillingState NVARCHAR(50),
    BillingPostalCode NVARCHAR(20),
    BillingCountry NVARCHAR(50),

    ShippingLine1 NVARCHAR(200),
    ShippingCity NVARCHAR(100),
    ShippingState NVARCHAR(50),
    ShippingPostalCode NVARCHAR(20),
    ShippingCountry NVARCHAR(50),

    Balance DECIMAL(18,2),
    Taxable BIT,
    Active BIT,
    Notes NVARCHAR(MAX),

    PreferredDeliveryMethod NVARCHAR(20),
    PrintOnCheckName NVARCHAR(150),

    QuickBooksCreateTime DATETIME,
    QuickBooksLastUpdateTime DATETIME,
    
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME DEFAULT GETDATE()
);


ALTER TABLE Customers
ADD Title NVARCHAR(50),
    Suffix NVARCHAR(50);


select * from Customers
select * from QuickBooksTokens
select * from ChartOfAccounts

select * from Customers where QuickBooksCustomerId = 62
delete from Customers where QuickBooksCustomerId = 58
