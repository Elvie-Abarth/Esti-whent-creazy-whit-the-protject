# Database creation - the actual commands

`DATABASE-NOTES.txt` covers the operational side (starting the project,
resetting, credentials); `DATABASE-ARCHITECTURE.md` explains the *design*
behind the schema. This is the missing piece: the literal commands that
build `MarsvinDb` from nothing, both the ones the app runs itself and the
equivalent you'd type by hand.

**The authoritative copy is `Data/Sql/schema.sql`** - the SQL below is a
snapshot of it for easy reading here; if the two ever disagree, the file in
`Data/Sql/` is the one the app actually runs. It's re-executed on every
`dotnet run`, guarded by `IF OBJECT_ID(...) IS NULL` / `IF COL_LENGTH(...)
IS NULL` / `IF NOT EXISTS (...)` checks, so a table, column, or constraint
is only ever created once and nothing is ever dropped.

## 1. What `dotnet run` does automatically

No manual step is required - this happens every time the app starts,
before it accepts its first request (`Program.cs` ->
`DbInitializer.EnsureCreatedAndSeeded`):

1. **Create the database, if it isn't there yet** (`DbInitializer.
   EnsureDatabaseExists`, connecting to `master` first since `MarsvinDb`
   itself may not exist):
   ```sql
   IF DB_ID(@name) IS NULL
   BEGIN
       DECLARE @sql NVARCHAR(MAX) = N'CREATE DATABASE ' + QUOTENAME(@name);
       EXEC (@sql);
   END
   ```
   (`@name` is `MarsvinDb`, read from `appsettings.json`'s
   `ConnectionStrings:MarsvinDb`.)
2. **Run the full schema script** (`DbInitializer.RunSchemaScript` reads
   `Data/Sql/schema.sql` off disk and executes it as one batch) - see §2
   below for the complete script.
3. **Seed the catalog and demo accounts, only if empty** (`SeedIfEmpty` /
   `SeedAccountsIfEmpty`) - plain parameterised `INSERT`s from
   `Data/DemoCatalog.cs`, skipped entirely once `dbo.Products` or
   `dbo.Users` already has rows, so a live database is never overwritten.

## 2. The full schema script (`Data/Sql/schema.sql`)

```sql
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
        BondedWithId  INT           NULL,                 -- another animal's ProductId, not a FK (see note below)
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

IF COL_LENGTH('dbo.StockProducts', 'PhotoUrl') IS NULL
BEGIN
    ALTER TABLE dbo.StockProducts ADD PhotoUrl NVARCHAR(300) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_StockProducts_StockQuantity')
BEGIN
    ALTER TABLE dbo.StockProducts WITH CHECK ADD CONSTRAINT CK_StockProducts_StockQuantity CHECK (StockQuantity >= 0);
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
        CreatedAt    DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        LastActiveAt DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        InactivityWarningStage TINYINT NOT NULL DEFAULT 0
    );
END

IF COL_LENGTH('dbo.Users', 'LastActiveAt') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD LastActiveAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();
END

IF COL_LENGTH('dbo.Users', 'InactivityWarningStage') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD InactivityWarningStage TINYINT NOT NULL DEFAULT 0;
END

IF COL_LENGTH('dbo.Users', 'TotpSecret') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TotpSecret NVARCHAR(64) NULL;
END

IF COL_LENGTH('dbo.Users', 'TotpEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TotpEnabled BIT NOT NULL DEFAULT 0;
END

IF OBJECT_ID('dbo.PendingLogins', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PendingLogins
    (
        PendingLoginId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId         INT NOT NULL REFERENCES dbo.Users (UserId),
        TokenHash      CHAR(64) NOT NULL UNIQUE, -- SHA-256 hex of the raw token emailed to the user
        ReturnUrl      NVARCHAR(512) NULL,
        ExpiresAt      DATETIME2 NOT NULL,
        CreatedAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID('dbo.CartItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CartItems
    (
        CartItemId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId     INT NOT NULL REFERENCES dbo.Users (UserId),
        ProductId  INT NOT NULL,             -- not a FK (see note below)
        Quantity   INT NOT NULL,
        AddedAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_CartItems_UserProduct UNIQUE (UserId, ProductId)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_CartItems_Quantity')
BEGIN
    ALTER TABLE dbo.CartItems WITH CHECK ADD CONSTRAINT CK_CartItems_Quantity CHECK (Quantity > 0);
END

IF OBJECT_ID('dbo.Orders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Orders
    (
        OrderId         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId          INT NULL REFERENCES dbo.Users (UserId), -- NULL once the buyer's account is deleted
        TotalPrice      DECIMAL(10, 2) NOT NULL,
        CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        DeliveryMethod  TINYINT NOT NULL DEFAULT 0,   -- 0 Pickup, 1 Shipping
        ShippingAddress NVARCHAR(500) NULL            -- only set when DeliveryMethod = Shipping
    );
END

ALTER TABLE dbo.Orders ALTER COLUMN UserId INT NULL;

IF COL_LENGTH('dbo.Orders', 'DeliveryMethod') IS NULL
BEGIN
    ALTER TABLE dbo.Orders ADD DeliveryMethod TINYINT NOT NULL DEFAULT 0;
END

IF COL_LENGTH('dbo.Orders', 'ShippingAddress') IS NULL
BEGIN
    ALTER TABLE dbo.Orders ADD ShippingAddress NVARCHAR(500) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_UserId' AND object_id = OBJECT_ID('dbo.Orders'))
BEGIN
    CREATE INDEX IX_Orders_UserId ON dbo.Orders (UserId);
END

IF OBJECT_ID('dbo.OrderItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderItems
    (
        OrderItemId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        OrderId     INT NOT NULL REFERENCES dbo.Orders (OrderId),
        ProductId   INT NOT NULL,             -- not a FK - a snapshot of what was bought
        ProductName NVARCHAR(200) NOT NULL,
        UnitPrice   DECIMAL(10, 2) NOT NULL,
        Quantity    INT NOT NULL,
        IsAnimal    BIT NOT NULL DEFAULT 0
    );
END

IF COL_LENGTH('dbo.OrderItems', 'IsAnimal') IS NULL
BEGIN
    ALTER TABLE dbo.OrderItems ADD IsAnimal BIT NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_OrderItems_Quantity')
BEGIN
    ALTER TABLE dbo.OrderItems WITH CHECK ADD CONSTRAINT CK_OrderItems_Quantity CHECK (Quantity > 0);
END

IF OBJECT_ID('dbo.Promotions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Promotions
    (
        PromotionId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Title           NVARCHAR(200) NOT NULL,
        Description     NVARCHAR(500) NOT NULL,
        DiscountPercent INT NOT NULL,
        ProductId       INT NULL,             -- not a FK (see note below)
        StartDate       DATE NOT NULL,
        EndDate         DATE NOT NULL,
        IsActive        BIT NOT NULL DEFAULT 1
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Promotions_DiscountPercent')
BEGIN
    ALTER TABLE dbo.Promotions WITH CHECK ADD CONSTRAINT CK_Promotions_DiscountPercent CHECK (DiscountPercent BETWEEN 1 AND 100);
END

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Promotions_DateRange')
BEGIN
    ALTER TABLE dbo.Promotions WITH CHECK ADD CONSTRAINT CK_Promotions_DateRange CHECK (EndDate >= StartDate);
END

IF OBJECT_ID('dbo.Shifts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Shifts
    (
        ShiftId   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId    INT NOT NULL REFERENCES dbo.Users (UserId),
        StartAt   DATETIME2 NOT NULL,
        EndAt     DATETIME2 NOT NULL,
        Note      NVARCHAR(200) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Shifts_UserId' AND object_id = OBJECT_ID('dbo.Shifts'))
BEGIN
    CREATE INDEX IX_Shifts_UserId ON dbo.Shifts (UserId);
END

IF OBJECT_ID('dbo.TimeOffRequests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TimeOffRequests
    (
        RequestId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId        INT NOT NULL REFERENCES dbo.Users (UserId),
        StartDate     DATE NOT NULL,
        EndDate       DATE NOT NULL,
        Reason        NVARCHAR(500) NULL,
        Status        TINYINT NOT NULL DEFAULT 0, -- 0 Pending, 1 Approved, 2 Denied
        RequestedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        DecidedAt     DATETIME2 NULL,
        DecidedByName NVARCHAR(200) NULL
    );
END

IF OBJECT_ID('dbo.AuditLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLog
    (
        AuditLogId  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ActorUserId INT NULL,               -- not a FK (see note below)
        ActorName   NVARCHAR(200) NOT NULL,
        Action      NVARCHAR(100) NOT NULL, -- short machine-readable verb, e.g. "Product.Deleted"
        Details     NVARCHAR(1000) NOT NULL,
        CreatedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
```

*(Column/table comments trimmed for length here - `Data/Sql/schema.sql`
has the full reasoning behind each design choice, e.g. why some
`ProductId`/`UserId` columns are a real foreign key and others are a plain
column checked in application code.)*

## 3. Doing it by hand (outside the app)

None of this is required to run the project - `dotnet run` does all of it
automatically. But if you want to build the database yourself, step by
step, from a terminal:

```bash
# 1. Create an empty database
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "CREATE DATABASE MarsvinDb;"

# 2. Run the schema script against it (creates every table, constraint, index)
sqlcmd -S "(localdb)\MSSQLLocalDB" -d MarsvinDb -i "Data\Sql\schema.sql"

# 3. Confirm the tables exist
sqlcmd -S "(localdb)\MSSQLLocalDB" -d MarsvinDb -Q "SELECT name FROM sys.tables ORDER BY name;"
```

At that point the database has every table and constraint, but no data -
`dotnet run` still needs to start once to seed the catalog and the demo
accounts (`DbInitializer.SeedIfEmpty` / `SeedAccountsIfEmpty`), since that
data comes from C# (`Data/DemoCatalog.cs`), not from `schema.sql`.

See `DATABASE-NOTES.txt` for how to reset an existing database back to
empty, and what to do if LocalDB itself won't connect.
