-- 建立 [tms].[Zone3Plus3] 資料表（若已存在則跳過）
IF OBJECT_ID(N'[tms].[Zone3Plus3]', 'U') IS NULL
BEGIN
    CREATE TABLE [tms].[Zone3Plus3] (
        Id               int            IDENTITY(1,1) NOT NULL,
        CityName         nvarchar(10)   NOT NULL,
        AreaName         nvarchar(20)   NOT NULL,
        Code6            int            NOT NULL,
        RoadName         nvarchar(100)  NOT NULL,
        DeliveryRangeRaw nvarchar(100)  NOT NULL,
        OddEven          char(1)        NOT NULL,
        MinNo            int            NULL,
        MaxNo            int            NULL,
        PostOffice       nvarchar(100)  NULL,
        BulkNote         nvarchar(200)  NULL,
        IsEnable         bit            NOT NULL CONSTRAINT DF_Zone3Plus3_IsEnable DEFAULT (1),
        UpdatedAt        datetime2(0)   NOT NULL CONSTRAINT DF_Zone3Plus3_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Zone3Plus3 PRIMARY KEY CLUSTERED (Id)
    );

    -- 唯一鍵（Upsert MERGE 比對用）
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Zone3Plus3_Natural
        ON [tms].[Zone3Plus3] (CityName, AreaName, RoadName, Code6, DeliveryRangeRaw);

    -- Code6 反查
    CREATE NONCLUSTERED INDEX IX_Zone3Plus3_Code6
        ON [tms].[Zone3Plus3] (Code6);

    -- 地址 → Code6 查詢
    CREATE NONCLUSTERED INDEX IX_Zone3Plus3_Address
        ON [tms].[Zone3Plus3] (CityName, AreaName, RoadName);

    PRINT N'[tms].[Zone3Plus3] 建立完成。';
END
ELSE
BEGIN
    PRINT N'[tms].[Zone3Plus3] 已存在，略過建立。';
END
