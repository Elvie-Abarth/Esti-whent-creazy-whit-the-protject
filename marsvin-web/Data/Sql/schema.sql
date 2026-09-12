-- Marsvin catalog schema.
-- Products is the base table (ProductType discriminates the subtype);
-- Animals and StockProducts hold the fields specific to each subtype,
-- mirroring the Product / Animal / StockProduct class hierarchy.

IF OBJECT_ID('dbo.Animals', 'U') IS NOT NULL DROP TABLE dbo.Animals;
IF OBJECT_ID('dbo.StockProducts', 'U') IS NOT NULL DROP TABLE dbo.StockProducts;
IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL DROP TABLE dbo.Products;

CREATE TABLE dbo.Products
(
    ProductId     INT           NOT NULL PRIMARY KEY,
    ProductType   TINYINT       NOT NULL,             -- 1 = Animal, 2 = StockProduct
    Name          NVARCHAR(200) NOT NULL,
    NameEn        NVARCHAR(200) NULL,
    Description   NVARCHAR(500) NOT NULL,
    DescriptionEn NVARCHAR(500) NULL,
    Price         DECIMAL(10, 2) NOT NULL
);

CREATE TABLE dbo.Animals
(
    ProductId     INT           NOT NULL PRIMARY KEY REFERENCES dbo.Products (ProductId),
    Breed         NVARCHAR(100) NOT NULL,
    BreedEn       NVARCHAR(100) NULL,
    Sex           TINYINT       NOT NULL,             -- 0 = Boar, 1 = Sow
    DateOfBirth   DATE          NOT NULL,
    Colour        NVARCHAR(100) NOT NULL,
    ColourEn      NVARCHAR(100) NULL,
    CoatPrimary   CHAR(7)       NOT NULL,
    CoatSecondary CHAR(7)       NOT NULL,
    Status        TINYINT       NOT NULL DEFAULT 0,   -- 0 Available, 1 Reserved, 2 Sold, 3 NotForSale
    BondedWithId  INT           NULL REFERENCES dbo.Products (ProductId),
    Personality   NVARCHAR(500) NOT NULL DEFAULT '',
    PersonalityEn NVARCHAR(500) NULL,
    PhotoUrl      NVARCHAR(300) NULL
);

CREATE TABLE dbo.StockProducts
(
    ProductId     INT           NOT NULL PRIMARY KEY REFERENCES dbo.Products (ProductId),
    Sku           NVARCHAR(50)  NOT NULL UNIQUE,
    Category      TINYINT       NOT NULL,             -- 0 Hay,1 Food,2 Cage,3 House,4 Toy,5 Bedding
    StockQuantity INT           NOT NULL,
    Unit          NVARCHAR(20)  NULL
);
