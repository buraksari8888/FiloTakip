/* =========================================================
   FiloTakip - Yola göre hız sınırı için veritabanı güncellemesi
   Çalıştırma: sqlcmd -S localhost -E -C -i 06_road_speed_limits.sql
   Sonra yol parçalarını ekle:  sqlcmd -S localhost -E -C -i 07_road_segments.sql

   RoadSegments tablosunu oluşturur, LocationRecords'a RoadSpeedLimitKmh sütununu ekler ve
   vw_VehicleLastLocation view'ına bu sütunu ekler.
   01_schema.sql bunları zaten içeriyor: veritabanını baştan oluşturduysan bunu çalıştırman gerekmez
   (07_road_segments.sql'i ise her durumda çalıştırman gerekir).
   Bu betik, veriyi silmeden VAR OLAN bir veritabanını güncellemek için (birden fazla çalıştırmak zararsız).
   ========================================================= */

USE FiloTakip;
GO

IF OBJECT_ID(N'dbo.RoadSegments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoadSegments
    (
        RoadSegmentId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RoadSegments PRIMARY KEY,
        RoadName       NVARCHAR(200) NULL,
        HighwayType    VARCHAR(30)   NOT NULL,
        SpeedLimitKmh  SMALLINT      NOT NULL CONSTRAINT CK_RoadSegments_Limit CHECK (SpeedLimitKmh BETWEEN 10 AND 150),
        StartLatitude  DECIMAL(9,6)  NOT NULL,
        StartLongitude DECIMAL(9,6)  NOT NULL,
        EndLatitude    DECIMAL(9,6)  NOT NULL,
        EndLongitude   DECIMAL(9,6)  NOT NULL
    );
END
GO

-- Eski konum kayıtlarında NULL kalır (o zaman hangi yolda olduğu hesaplanmamıştı)
IF COL_LENGTH(N'dbo.LocationRecords', N'RoadSpeedLimitKmh') IS NULL
    ALTER TABLE dbo.LocationRecords ADD RoadSpeedLimitKmh SMALLINT NULL;
GO

-- 01_schema.sql'deki view ile aynı
CREATE OR ALTER VIEW dbo.vw_VehicleLastLocation
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
    l.RecordedAt,
    l.RoadSpeedLimitKmh
FROM dbo.Vehicles v
LEFT JOIN dbo.Drivers d ON d.DriverId = v.DriverId
OUTER APPLY
(
    SELECT TOP (1) lr.Latitude, lr.Longitude, lr.SpeedKmh, lr.FuelPercent, lr.IgnitionOn, lr.RecordedAt, lr.RoadSpeedLimitKmh
    FROM dbo.LocationRecords lr
    WHERE lr.VehicleId = v.VehicleId
    ORDER BY lr.RecordedAt DESC
) l
WHERE v.IsActive = 1;
GO

PRINT N'RoadSegments tablosu ve LocationRecords.RoadSpeedLimitKmh sütunu hazır. Şimdi 07_road_segments.sql''i çalıştır.';
GO
