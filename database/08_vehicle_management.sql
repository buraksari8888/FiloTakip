/* =========================================================
   FiloTakip - Araç yönetimi için veritabanı güncellemesi
   Çalıştırma: sqlcmd -S localhost -E -C -i 08_vehicle_management.sql

   Vehicles tablosuna araç türünü (VehicleType) ekler, örnek araçların türünü doldurur ve
   vw_VehicleLastLocation view'ına aracın türünü ve sürücüsünün numarasını ekler.
   01_schema.sql + 02_seed.sql bunları zaten içeriyor: veritabanını baştan oluşturduysan bunu çalıştırman gerekmez.
   Bu betik, veriyi silmeden VAR OLAN bir veritabanını güncellemek için (birden fazla çalıştırmak zararsız).
   ========================================================= */

USE FiloTakip;
GO

IF COL_LENGTH(N'dbo.Vehicles', N'VehicleType') IS NULL
    ALTER TABLE dbo.Vehicles ADD VehicleType VARCHAR(20) NOT NULL
        CONSTRAINT DF_Vehicles_VehicleType DEFAULT ('Otomobil')
        CONSTRAINT CK_Vehicles_VehicleType CHECK (VehicleType IN ('Otomobil', 'HafifTicari', 'Kamyon', 'Otobus'));
GO

-- Örnek araçlar (02_seed.sql ile aynı). Var olan bütün araçlar varsayılan 'Otomobil' ile eklendi, bunlar düzeltilir.
UPDATE dbo.Vehicles SET VehicleType = 'HafifTicari' WHERE Plate IN ('34 ABC 123', '35 JKL 012', '16 MNO 345');
UPDATE dbo.Vehicles SET VehicleType = 'Kamyon'      WHERE Plate = '06 GHI 789';
GO

-- 01_schema.sql'deki view ile aynı
CREATE OR ALTER VIEW dbo.vw_VehicleLastLocation
AS
SELECT
    v.VehicleId,
    v.Plate,
    v.VehicleType,
    v.Brand,
    v.Model,
    v.SpeedLimitKmh,
    v.DriverId,
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

PRINT N'Vehicles.VehicleType sütunu ve view hazır.';
GO
