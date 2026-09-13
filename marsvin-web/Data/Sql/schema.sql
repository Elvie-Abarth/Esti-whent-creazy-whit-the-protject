-- Marsvin schema.
-- Products is the base table (ProductType discriminates the subtype);
-- Animals and StockProducts hold the fields specific to each subtype,
-- mirroring the Product / Animal / StockProduct class hierarchy.
--
-- Every table here is created once, the first time it's missing, and left
-- alone after that - nothing is ever dropped and recreated on startup.
-- That used to be true only for Users/Orders/CartItems/Promotions; now that
-- admin/employee pages can add, edit and delete catalog rows too, wiping
-- Products/Animals/StockProducts on every "dotnet run" would erase their
-- work along with everyone else's accounts. DemoCatalog only seeds a fresh,
-- empty database (see DbInitializer.cs) - it never overwrites live data.
--
-- FKs point at Products only where the two rows are always created and
-- deleted together in one transaction (Animals/StockProducts, whose
-- ProductId *is* their primary key). CartItems/OrderItems/Promotions and
-- Animals.BondedWithId reference a ProductId that can later be deleted by
-- an admin, so those are left as plain columns (checked in application
-- code) rather than FKs that would block the deletion.

IF OBJECT_ID('dbo.Products', 'U') IS NULL
BEGIN
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
END

IF OBJECT_ID('dbo.Animals', 'U') IS NULL
BEGIN
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
        BondedWithId  INT           NULL,                 -- another animal's ProductId, not a FK (see note above)
        Personality   NVARCHAR(500) NOT NULL DEFAULT '',
        PersonalityEn NVARCHAR(500) NULL,
        PhotoUrl      NVARCHAR(300) NULL
    );
END

IF OBJECT_ID('dbo.StockProducts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockProducts
    (
        ProductId     INT           NOT NULL PRIMARY KEY REFERENCES dbo.Products (ProductId),
        Sku           NVARCHAR(50)  NOT NULL UNIQUE,
        Category      TINYINT       NOT NULL,             -- 0 Hay,1 Food,2 Cage,3 House,4 Toy,5 Bedding
        StockQuantity INT           NOT NULL,
        Unit          NVARCHAR(20)  NULL,
        PhotoUrl      NVARCHAR(300) NULL
    );
END

-- Column added after StockProducts already existed on live databases (create-once
-- tables don't pick up new columns from the CREATE TABLE above) - safe to run every time.
IF COL_LENGTH('dbo.StockProducts', 'PhotoUrl') IS NULL
BEGIN
    ALTER TABLE dbo.StockProducts ADD PhotoUrl NVARCHAR(300) NULL;
END

-- ---------------------------------------------------------------------------
-- Accounts, carts, orders and promotions.
-- ---------------------------------------------------------------------------

IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        UserId       INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Email        NVARCHAR(256) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(500) NOT NULL,
        DisplayName  NVARCHAR(200) NOT NULL,
        Role         TINYINT       NOT NULL,             -- 0 Customer, 1 Employee, 2 Admin
        IsActive     BIT           NOT NULL DEFAULT 1,
        CreatedAt    DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID('dbo.CartItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CartItems
    (
        CartItemId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId     INT NOT NULL REFERENCES dbo.Users (UserId),
        ProductId  INT NOT NULL,             -- not a FK (see note above)
        Quantity   INT NOT NULL,
        AddedAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_CartItems_UserProduct UNIQUE (UserId, ProductId)
    );
END

IF OBJECT_ID('dbo.Orders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Orders
    (
        OrderId    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId     INT NULL REFERENCES dbo.Users (UserId), -- NULL once the buyer's account is deleted; see note below
        TotalPrice DECIMAL(10, 2) NOT NULL,
        CreatedAt  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

-- UserId started out NOT NULL; loosened so an admin can delete a customer's
-- account (SqlUserAccountStore.DeleteUser) while keeping their past orders as
-- a historical record instead of being blocked by the FK or deleting the
-- sales history along with the account. Safe to run every time - a no-op
-- once the column is already nullable.
ALTER TABLE dbo.Orders ALTER COLUMN UserId INT NULL;

IF OBJECT_ID('dbo.OrderItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderItems
    (
        OrderItemId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        OrderId     INT NOT NULL REFERENCES dbo.Orders (OrderId),
        ProductId   INT NOT NULL,             -- not a FK - a snapshot of what was bought, kept even if the product is later deleted
        ProductName NVARCHAR(200) NOT NULL,
        UnitPrice   DECIMAL(10, 2) NOT NULL,
        Quantity    INT NOT NULL
    );
END

IF OBJECT_ID('dbo.Promotions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Promotions
    (
        PromotionId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Title           NVARCHAR(200) NOT NULL,
        Description     NVARCHAR(500) NOT NULL,
        DiscountPercent INT NOT NULL,
        ProductId       INT NULL,             -- not a FK (see note above)
        StartDate       DATE NOT NULL,
        EndDate         DATE NOT NULL,
        IsActive        BIT NOT NULL DEFAULT 1
    );
END
