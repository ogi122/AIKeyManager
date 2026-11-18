IF DB_ID('AIKeyManager') IS NULL
    CREATE DATABASE AIKeyManager;
GO

USE AIKeyManager;
GO

CREATE TABLE Roles (
    RoleId INT PRIMARY KEY IDENTITY(1,1),
    RoleName NVARCHAR(50) NOT NULL UNIQUE
);

CREATE TABLE Users (
    UserId INT PRIMARY KEY IDENTITY(1,1),
    Username NVARCHAR(100) NOT NULL UNIQUE,
    Email NVARCHAR(200) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(256) NOT NULL,
    RoleId INT NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    IsActive BIT DEFAULT 1,
    FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

CREATE TABLE Providers (
    ProviderId INT PRIMARY KEY IDENTITY(1,1),
    ProviderName NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(500),
    LogoUrl NVARCHAR(300),
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME DEFAULT GETDATE()
);

CREATE TABLE Models (
    ModelId INT PRIMARY KEY IDENTITY(1,1),
    ProviderId INT NOT NULL,
    ModelName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500),
    CostPerRequest DECIMAL(10,6) NOT NULL DEFAULT 0.01,
    IsActive BIT DEFAULT 1,
    FOREIGN KEY (ProviderId) REFERENCES Providers(ProviderId)
);

CREATE TABLE Subscriptions (
    SubscriptionId INT PRIMARY KEY IDENTITY(1,1),
    PlanName NVARCHAR(50) NOT NULL UNIQUE,
    MonthlyCredit DECIMAL(10,2) NOT NULL,
    MaxApiKeys INT NOT NULL,
    Description NVARCHAR(300)
);

CREATE TABLE UserSubscriptions (
    UserSubscriptionId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL,
    SubscriptionId INT NOT NULL,
    StartDate DATETIME DEFAULT GETDATE(),
    EndDate DATETIME,
    IsActive BIT DEFAULT 1,
    FOREIGN KEY (UserId) REFERENCES Users(UserId),
    FOREIGN KEY (SubscriptionId) REFERENCES Subscriptions(SubscriptionId)
);

CREATE TABLE Credits (
    CreditId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL UNIQUE,
    Balance DECIMAL(10,6) NOT NULL DEFAULT 0.000000,
    LastUpdated DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

CREATE TABLE Transactions (
    TransactionId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL,
    Amount DECIMAL(10,6) NOT NULL,
    TransactionType NVARCHAR(50) NOT NULL,
    Description NVARCHAR(300),
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

CREATE TABLE ApiKeys (
    ApiKeyId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL,
    ModelId INT NOT NULL,
    KeyValue NVARCHAR(64) NOT NULL UNIQUE,
    KeyName NVARCHAR(100),
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME DEFAULT GETDATE(),
    LastUsedAt DATETIME,
    FOREIGN KEY (UserId) REFERENCES Users(UserId),
    FOREIGN KEY (ModelId) REFERENCES Models(ModelId)
);

CREATE TABLE Requests (
    RequestId INT PRIMARY KEY IDENTITY(1,1),
    ApiKeyId INT NOT NULL,
    UserId INT NOT NULL,
    ModelId INT NOT NULL,
    TokensUsed INT NOT NULL DEFAULT 0,
    CostCharged DECIMAL(10,6) NOT NULL DEFAULT 0,
    RequestedAt DATETIME DEFAULT GETDATE(),
    StatusCode INT DEFAULT 200,
    FOREIGN KEY (ApiKeyId) REFERENCES ApiKeys(ApiKeyId),
    FOREIGN KEY (UserId) REFERENCES Users(UserId),
    FOREIGN KEY (ModelId) REFERENCES Models(ModelId)
);

CREATE TABLE AuditLog (
    AuditId INT PRIMARY KEY IDENTITY(1,1),
    TableName NVARCHAR(100) NOT NULL,
    Action NVARCHAR(50) NOT NULL,
    AffectedId INT,
    Description NVARCHAR(500),
    CreatedAt DATETIME DEFAULT GETDATE(),
    PerformedBy NVARCHAR(100)
);

GO

CREATE TRIGGER trg_ApiKey_Insert
ON ApiKeys
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO AuditLog (TableName, Action, AffectedId, Description, PerformedBy)
    SELECT 
        'ApiKeys',
        'INSERT',
        i.ApiKeyId,
        'Novi API key kreiran: ' + i.KeyName + ' za korisnika ID ' + CAST(i.UserId AS NVARCHAR),
        SYSTEM_USER
    FROM inserted i;
END;
GO

CREATE TRIGGER trg_Request_DeductCredit
ON Requests
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Credits
    SET 
        Balance = Balance - i.CostCharged,
        LastUpdated = GETDATE()
    FROM Credits c
    INNER JOIN inserted i ON c.UserId = i.UserId;

    INSERT INTO Transactions (UserId, Amount, TransactionType, Description)
    SELECT 
        i.UserId,
        -i.CostCharged,
        'Deduction',
        'API request - Model ID: ' + CAST(i.ModelId AS NVARCHAR)
    FROM inserted i;
END;
GO

CREATE PROCEDURE sp_GenerateApiKey
    @UserId INT,
    @ModelId INT,
    @KeyName NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Users WHERE UserId = @UserId AND IsActive = 1)
    BEGIN
        RAISERROR('Korisnik ne postoji ili nije aktivan.', 16, 1);
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM Models WHERE ModelId = @ModelId AND IsActive = 1)
    BEGIN
        RAISERROR('Model ne postoji ili nije aktivan.', 16, 1);
        RETURN;
    END

    DECLARE @NewKey NVARCHAR(64);
    SET @NewKey = 'ak-' + LOWER(REPLACE(CAST(NEWID() AS NVARCHAR(36)), '-', '')) + LOWER(REPLACE(CAST(NEWID() AS NVARCHAR(36)), '-', ''));
    SET @NewKey = LEFT(@NewKey, 48);

    INSERT INTO ApiKeys (UserId, ModelId, KeyValue, KeyName, IsActive, CreatedAt)
    VALUES (@UserId, @ModelId, @NewKey, @KeyName, 1, GETDATE());

    SELECT @NewKey AS GeneratedKey, SCOPE_IDENTITY() AS ApiKeyId;
END;
GO

CREATE PROCEDURE sp_GetUserUsageReport
    @UserId INT,
    @DateFrom DATETIME = NULL,
    @DateTo DATETIME = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @DateFrom IS NULL SET @DateFrom = DATEADD(MONTH, -1, GETDATE());
    IF @DateTo IS NULL SET @DateTo = GETDATE();

    SELECT 
        u.Username,
        p.ProviderName,
        m.ModelName,
        COUNT(r.RequestId) AS TotalRequests,
        SUM(r.TokensUsed) AS TotalTokens,
        SUM(r.CostCharged) AS TotalCost,
        MIN(r.RequestedAt) AS FirstRequest,
        MAX(r.RequestedAt) AS LastRequest
    FROM Requests r
    INNER JOIN Users u ON r.UserId = u.UserId
    INNER JOIN Models m ON r.ModelId = m.ModelId
    INNER JOIN Providers p ON m.ProviderId = p.ProviderId
    WHERE r.UserId = @UserId
      AND r.RequestedAt BETWEEN @DateFrom AND @DateTo
    GROUP BY u.Username, p.ProviderName, m.ModelName
    ORDER BY TotalCost DESC;
END;
GO

CREATE FUNCTION fn_GetUserTotalSpent (@UserId INT)
RETURNS DECIMAL(10,6)
AS
BEGIN
    DECLARE @Total DECIMAL(10,6);
    SELECT @Total = ISNULL(SUM(CostCharged), 0)
    FROM Requests
    WHERE UserId = @UserId;
    RETURN @Total;
END;
GO

CREATE FUNCTION fn_GetActiveApiKeys (@UserId INT)
RETURNS TABLE
AS
RETURN (
    SELECT 
        ak.ApiKeyId,
        ak.KeyValue,
        ak.KeyName,
        ak.CreatedAt,
        ak.LastUsedAt,
        m.ModelName,
        p.ProviderName,
        COUNT(r.RequestId) AS TotalRequests,
        ISNULL(SUM(r.CostCharged), 0) AS TotalCost
    FROM ApiKeys ak
    INNER JOIN Models m ON ak.ModelId = m.ModelId
    INNER JOIN Providers p ON m.ProviderId = p.ProviderId
    LEFT JOIN Requests r ON ak.ApiKeyId = r.ApiKeyId
    WHERE ak.UserId = @UserId AND ak.IsActive = 1
    GROUP BY ak.ApiKeyId, ak.KeyValue, ak.KeyName, ak.CreatedAt, ak.LastUsedAt, m.ModelName, p.ProviderName
);
GO

INSERT INTO Roles (RoleName) VALUES ('Admin'), ('User');

INSERT INTO Subscriptions (PlanName, MonthlyCredit, MaxApiKeys, Description) VALUES
('Free',       5.00,   2,  'Besplatan plan, 5$ kredita mjesecno, max 2 API keya'),
('Pro',        25.00,  10, 'Pro plan, 25$ kredita mjesecno, max 10 API keyeva'),
('Enterprise', 100.00, 50, 'Enterprise plan, 100$ kredita, neograniceni keyevi');

INSERT INTO Providers (ProviderName, Description, LogoUrl) VALUES
('OpenAI',     'GPT serija modela',         '/images/openai.png'),
('Anthropic',  'Claude serija modela',       '/images/anthropic.png'),
('Google',     'Gemini serija modela',       '/images/google.png'),
('Meta',       'LLaMA open-source modeli',   '/images/meta.png');

INSERT INTO Models (ProviderId, ModelName, Description, CostPerRequest) VALUES
(1, 'GPT-4o',           'Najnoviji OpenAI multimodalni model',     0.005000),
(1, 'GPT-4o-mini',      'Brzi i jeftiniji GPT-4o',                 0.000150),
(1, 'GPT-3.5-turbo',    'Brz i ekonomican model',                  0.000050),
(2, 'Claude 3.5 Sonnet','Anthropic najnoviji model',               0.003000),
(2, 'Claude 3 Haiku',   'Brz i ekonomican Claude',                 0.000250),
(3, 'Gemini 1.5 Pro',   'Google napredni multimodalni model',      0.003500),
(3, 'Gemini 1.5 Flash', 'Brza Gemini varijanta',                   0.000075),
(4, 'LLaMA 3.1 70B',    'Meta open-source veliki model',           0.000900),
(4, 'LLaMA 3.1 8B',     'Meta open-source mali model',             0.000200);

INSERT INTO Users (Username, Email, PasswordHash, RoleId) VALUES
('admin', 'admin@aikeymanager.com', 'PrP+ZrMeO00Q+nC1ytSccRIpSvauTkdqHEBRVdRaoSE=', 1);

INSERT INTO Credits (UserId, Balance) VALUES (1, 999.999999);
INSERT INTO UserSubscriptions (UserId, SubscriptionId) VALUES (1, 3);

GO
