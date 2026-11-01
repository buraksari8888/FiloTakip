/* =========================================================
   FiloTakip - Veritabanı şeması
   Çalıştırma: sqlcmd -S localhost -E -i 01_schema.sql
   DİKKAT: Bu betik FiloTakip veritabanını SİLİP yeniden oluşturur.
           Sadece geliştirme ortamında kullan.
   ========================================================= */

USE master;
GO

IF DB_ID(N'FiloTakip') IS NOT NULL
BEGIN
    ALTER DATABASE FiloTakip SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE FiloTakip;
END
GO

CREATE DATABASE FiloTakip;
GO

USE FiloTakip;
GO

/* ---------- Sürücüler ---------- */
CREATE TABLE dbo.Drivers
(
    DriverId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Drivers PRIMARY KEY,
    FirstName     NVARCHAR(50)  NOT NULL,
    LastName      NVARCHAR(50)  NOT NULL,
    Phone         VARCHAR(20)   NULL,
    LicenseNumber VARCHAR(20)   NOT NULL CONSTRAINT UQ_Drivers_LicenseNumber UNIQUE,
    IsActive      BIT           NOT NULL CONSTRAINT DF_Drivers_IsActive  DEFAULT (1),
    CreatedAt     DATETIME2(0)  NOT NULL CONSTRAINT DF_Drivers_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

/* ---------- Araçlar ---------- */
CREATE TABLE dbo.Vehicles
(
    VehicleId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vehicles PRIMARY KEY,
    Plate         VARCHAR(15)   NOT NULL CONSTRAINT UQ_Vehicles_Plate UNIQUE,
    Brand         NVARCHAR(50)  NOT NULL,
    Model         NVARCHAR(50)  NOT NULL,
    ModelYear     SMALLINT      NOT NULL CONSTRAINT CK_Vehicles_ModelYear CHECK (ModelYear BETWEEN 1990 AND 2100),
    SpeedLimitKmh SMALLINT      NOT NULL CONSTRAINT DF_Vehicles_SpeedLimit DEFAULT (90)
                                         CONSTRAINT CK_Vehicles_SpeedLimit CHECK (SpeedLimitKmh BETWEEN 10 AND 200),
    DriverId      INT           NULL     CONSTRAINT FK_Vehicles_Drivers REFERENCES dbo.Drivers (DriverId),
    IsActive      BIT           NOT NULL CONSTRAINT DF_Vehicles_IsActive  DEFAULT (1),
    CreatedAt     DATETIME2(0)  NOT NULL CONSTRAINT DF_Vehicles_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

CREATE INDEX IX_Vehicles_DriverId ON dbo.Vehicles (DriverId);
GO

/* ---------- Konum kayıtları (en çok veri bu tabloya gelir) ---------- */
CREATE TABLE dbo.LocationRecords
(
    LocationId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LocationRecords PRIMARY KEY,
    VehicleId   INT           NOT NULL CONSTRAINT FK_LocationRecords_Vehicles REFERENCES dbo.Vehicles (VehicleId),
    Latitude    DECIMAL(9,6)  NOT NULL CONSTRAINT CK_LocationRecords_Lat CHECK (Latitude  BETWEEN -90  AND 90),
    Longitude   DECIMAL(9,6)  NOT NULL CONSTRAINT CK_LocationRecords_Lng CHECK (Longitude BETWEEN -180 AND 180),
    SpeedKmh    DECIMAL(5,1)  NOT NULL CONSTRAINT CK_LocationRecords_Speed CHECK (SpeedKmh >= 0),
    FuelPercent DECIMAL(5,2)  NULL     CONSTRAINT CK_LocationRecords_Fuel  CHECK (FuelPercent BETWEEN 0 AND 100),
    IgnitionOn  BIT           NOT NULL,
    RecordedAt  DATETIME2(0)  NOT NULL   -- cihazın veriyi ürettiği an (UTC)
);
GO

-- "Bu aracın son konumları" sorgusu çok sık çalışacak, bu index onu hızlandırır.
CREATE INDEX IX_LocationRecords_Vehicle_Time
    ON dbo.LocationRecords (VehicleId, RecordedAt DESC)
    INCLUDE (Latitude, Longitude, SpeedKmh);
GO

/* ---------- Alarmlar ---------- */
CREATE TABLE dbo.Alarms
(
    AlarmId    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Alarms PRIMARY KEY,
    VehicleId  INT            NOT NULL CONSTRAINT FK_Alarms_Vehicles REFERENCES dbo.Vehicles (VehicleId),
    LocationId BIGINT         NULL     CONSTRAINT FK_Alarms_LocationRecords REFERENCES dbo.LocationRecords (LocationId),
    AlarmType  VARCHAR(20)    NOT NULL CONSTRAINT CK_Alarms_Type CHECK (AlarmType IN ('SpeedLimit', 'LowFuel', 'Idle', 'FuelDrain', 'GeofenceEnter', 'GeofenceExit')),
    Message    NVARCHAR(200)  NOT NULL,
    IsRead     BIT            NOT NULL CONSTRAINT DF_Alarms_IsRead    DEFAULT (0),
    CreatedAt  DATETIME2(0)   NOT NULL CONSTRAINT DF_Alarms_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

CREATE INDEX IX_Alarms_Vehicle_Time ON dbo.Alarms (VehicleId, CreatedAt DESC);
GO

/* ---------- Bölgeler (geofence): haritada daire şeklinde alanlar ---------- */
-- Araç bölgenin sınırını geçince (girince ya da çıkınca) alarm üretilir. Hangisinde alarm isteneceği bölgeye göre seçilir:
-- depodan çıkış, müşteriye varış gibi. Bölgeler bütün araçlar için geçerli.
CREATE TABLE dbo.Geofences
(
    GeofenceId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Geofences PRIMARY KEY,
    Name         NVARCHAR(100) NOT NULL,
    Latitude     DECIMAL(9,6)  NOT NULL CONSTRAINT CK_Geofences_Lat CHECK (Latitude  BETWEEN -90  AND 90),
    Longitude    DECIMAL(9,6)  NOT NULL CONSTRAINT CK_Geofences_Lng CHECK (Longitude BETWEEN -180 AND 180),
    RadiusMeters INT           NOT NULL CONSTRAINT CK_Geofences_Radius CHECK (RadiusMeters BETWEEN 50 AND 50000),
    AlarmOnEnter BIT           NOT NULL CONSTRAINT DF_Geofences_AlarmOnEnter DEFAULT (1),
    AlarmOnExit  BIT           NOT NULL CONSTRAINT DF_Geofences_AlarmOnExit  DEFAULT (1),
    IsActive     BIT           NOT NULL CONSTRAINT DF_Geofences_IsActive     DEFAULT (1),
    CreatedAt    DATETIME2(0)  NOT NULL CONSTRAINT DF_Geofences_CreatedAt    DEFAULT (SYSUTCDATETIME()),
    -- Hiçbir durumda alarm üretmeyen bölge anlamsız: en az biri açık olmalı
    CONSTRAINT CK_Geofences_AlarmOn CHECK (AlarmOnEnter = 1 OR AlarmOnExit = 1)
);
GO

/* ---------- View: her aracın son konumu ---------- */
CREATE VIEW dbo.vw_VehicleLastLocation
AS
SELECT
    v.VehicleId,
    v.Plate,
    v.Brand,
    v.Model,
    v.SpeedLimitKmh,
    d.FirstName + N' ' + d.LastName AS DriverName,
    l.Latitude,
    l.Longitude,
    l.SpeedKmh,
    l.FuelPercent,
    l.IgnitionOn,
    l.RecordedAt
FROM dbo.Vehicles v
LEFT JOIN dbo.Drivers d ON d.DriverId = v.DriverId
OUTER APPLY
(
    SELECT TOP (1) lr.Latitude, lr.Longitude, lr.SpeedKmh, lr.FuelPercent, lr.IgnitionOn, lr.RecordedAt
    FROM dbo.LocationRecords lr
    WHERE lr.VehicleId = v.VehicleId
    ORDER BY lr.RecordedAt DESC
) l
WHERE v.IsActive = 1;
GO

PRINT N'FiloTakip şeması oluşturuldu.';
GO
