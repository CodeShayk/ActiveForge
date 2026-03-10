-- =============================================================================
-- ActiveForge.ORM Examples — Database Setup Script
-- Run this against a SQL Server database before starting the examples app.
-- =============================================================================

-- Create database (optional — run as sysadmin if needed)
-- CREATE DATABASE ActiveForgeDemo;
-- GO
-- USE ActiveForgeDemo;
-- GO

-- ── Categories ────────────────────────────────────────────────────────────────

CREATE TABLE Categories (
    ID          INT           IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL
);

-- ── Products ──────────────────────────────────────────────────────────────────

CREATE TABLE Products (
    ID          INT             IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(200)   NOT NULL,
    Description NVARCHAR(MAX)   NULL,
    Price       DECIMAL(10, 2)  NOT NULL DEFAULT 0,
    CategoryID  INT             NULL REFERENCES Categories(ID),
    InStock     BIT             NOT NULL DEFAULT 1,
    CreatedAt   DATETIME        NULL
);

-- ── Orders ────────────────────────────────────────────────────────────────────

CREATE TABLE Orders (
    ID           INT             IDENTITY(1,1) PRIMARY KEY,
    CustomerName NVARCHAR(200)   NOT NULL,
    OrderDate    DATETIME        NOT NULL,
    TotalAmount  DECIMAL(12, 2)  NOT NULL DEFAULT 0,
    Status       NVARCHAR(50)    NOT NULL DEFAULT 'Pending'
);

-- ── OrderLines ────────────────────────────────────────────────────────────────

CREATE TABLE OrderLines (
    ID        INT            IDENTITY(1,1) PRIMARY KEY,
    OrderID   INT            NOT NULL REFERENCES Orders(ID),
    ProductID INT            NOT NULL REFERENCES Products(ID),
    Quantity  INT            NOT NULL DEFAULT 1,
    UnitPrice DECIMAL(10,2)  NOT NULL DEFAULT 0
);

-- ── Seed data ─────────────────────────────────────────────────────────────────

INSERT INTO Categories (Name, Description) VALUES
    ('Electronics', 'Electronic gadgets and accessories'),
    ('Books',       'Books, ebooks, and audiobooks'),
    ('Clothing',    'Apparel for all occasions');

INSERT INTO Products (Name, Description, Price, CategoryID, InStock, CreatedAt) VALUES
    ('Wireless Headphones',  'Premium noise-cancelling headphones', 89.99, 1, 1, GETUTCDATE()),
    ('USB-C Hub',            'Seven-port USB-C hub',                29.99, 1, 1, GETUTCDATE()),
    ('Clean Code',           'A handbook of agile software craftsmanship', 34.99, 2, 1, GETUTCDATE()),
    ('Design Patterns',      'Elements of reusable object-oriented software', 44.99, 2, 0, GETUTCDATE()),
    ('Cotton T-Shirt',       'Comfortable everyday t-shirt',         9.99, 3, 1, GETUTCDATE()),
    ('Denim Jeans',          NULL,                                  49.99, 3, 1, GETUTCDATE()),
    ('Bluetooth Speaker',    'Portable waterproof speaker',         59.99, 1, 0, GETUTCDATE()),
    ('Python Crash Course',  'Beginner-friendly Python guide',       24.99, 2, 1, GETUTCDATE());
