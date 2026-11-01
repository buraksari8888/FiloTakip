/* =========================================================
   FiloTakip - Bölge (geofence) alarmı için veritabanı güncellemesi
   Çalıştırma: sqlcmd -S localhost -E -C -i 05_geofences.sql

   Geofences tablosunu oluşturur, örnek bölgeleri ekler ve CK_Alarms_Type kuralına
   yeni alarm türlerini ('GeofenceEnter', 'GeofenceExit') ekler.
   01_schema.sql + 02_seed.sql bunları zaten içeriyor: veritabanını baştan oluşturduysan bunu çalıştırman gerekmez.
   Bu betik, veriyi silmeden VAR OLAN bir veritabanını güncellemek için (birden fazla çalıştırmak zararsız).
   ========================================================= */

USE FiloTakip;
GO

IF OBJECT_ID(N'dbo.Geofences', N'U') IS NULL
BEGIN
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
        CONSTRAINT CK_Geofences_AlarmOn CHECK (AlarmOnEnter = 1 OR AlarmOnExit = 1)
    );
END
GO

-- Örnek bölgeler (02_seed.sql ile aynı), sadece tablo boşsa: tekrar çalıştırınca ikinci kez eklenmesin
IF NOT EXISTS (SELECT 1 FROM dbo.Geofences)
    INSERT INTO dbo.Geofences (Name, Latitude, Longitude, RadiusMeters, AlarmOnEnter, AlarmOnExit)
    VALUES
        (N'Yeşilköy Depo',            40.961988, 28.830199, 400, 0, 1),
        (N'Sarıyer Teslimat Noktası', 41.164643, 29.051411, 400, 1, 0),
        (N'Beykoz Şantiye',           41.132845, 29.097354, 400, 1, 1);
GO

-- CHECK kuralı yerinde değiştirilemez: silinip yeni haliyle tekrar eklenir
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Alarms_Type')
    ALTER TABLE dbo.Alarms DROP CONSTRAINT CK_Alarms_Type;

ALTER TABLE dbo.Alarms ADD CONSTRAINT CK_Alarms_Type
    CHECK (AlarmType IN ('SpeedLimit', 'LowFuel', 'Idle', 'FuelDrain', 'GeofenceEnter', 'GeofenceExit'));
GO

PRINT N'Geofences tablosu hazır, Alarms tablosuna bölge giriş/çıkış alarm türleri eklendi.';
GO
