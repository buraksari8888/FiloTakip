/* =========================================================
   FiloTakip - Örnek veriler
   Çalıştırma: sqlcmd -S localhost -E -i 02_seed.sql
   ========================================================= */

USE FiloTakip;
GO

-- Telefonlar uydurma (0500 000 00 0x), gerçek bir kişiye ait olmasın diye
INSERT INTO dbo.Drivers (FirstName, LastName, Phone, LicenseNumber)
VALUES
    (N'Ahmet',  N'Yılmaz', '05000000001', 'B-100001'),
    (N'Ayşe',   N'Demir',  '05000000002', 'B-100002'),
    (N'Mehmet', N'Çelik',  '05000000003', 'C-100003'),
    (N'Zeynep', N'Kaya',   '05000000004', 'B-100004');
GO

INSERT INTO dbo.Vehicles (Plate, VehicleType, Brand, Model, ModelYear, SpeedLimitKmh, DriverId)
VALUES
    ('34 ABC 123', 'HafifTicari', N'Ford',       N'Transit',  2021, 90,  1),
    ('34 DEF 456', 'Otomobil',    N'Renault',    N'Clio',     2022, 110, 2),
    ('06 GHI 789', 'Kamyon',      N'Mercedes',   N'Actros',   2019, 80,  3),
    ('35 JKL 012', 'HafifTicari', N'Fiat',       N'Doblo',    2020, 90,  NULL),
    ('16 MNO 345', 'HafifTicari', N'Volkswagen', N'Caddy',    2023, 90,  4);
GO

-- İstanbul civarında birkaç konum kaydı (gerçek akışı Python simülatörü üretecek)
DECLARE @now DATETIME2(0) = SYSUTCDATETIME();

INSERT INTO dbo.LocationRecords (VehicleId, Latitude, Longitude, SpeedKmh, FuelPercent, IgnitionOn, RecordedAt)
VALUES
    (1, 41.008238, 28.978359,  45.0, 72.50, 1, DATEADD(MINUTE, -10, @now)),
    (1, 41.015137, 28.979530,  62.5, 72.10, 1, DATEADD(MINUTE,  -5, @now)),
    (1, 41.022010, 28.983100, 104.0, 71.80, 1, @now),
    (2, 40.990322, 29.028720,  38.0, 55.00, 1, DATEADD(MINUTE,  -3, @now)),
    (3, 41.064540, 28.993210,   0.0,  8.40, 0, DATEADD(MINUTE,  -1, @now)),
    (5, 41.043120, 29.009870,  71.2, 90.00, 1, @now);
GO

-- 1 numaralı aracın son kaydı hız sınırını (90) aştığı için örnek alarm
INSERT INTO dbo.Alarms (VehicleId, LocationId, AlarmType, Message)
SELECT TOP (1) VehicleId, LocationId, 'SpeedLimit', N'Hız sınırı aşıldı: 104 km/s (araç sınırı 90)'
FROM dbo.LocationRecords
WHERE VehicleId = 1
ORDER BY RecordedAt DESC;

INSERT INTO dbo.Alarms (VehicleId, LocationId, AlarmType, Message)
SELECT TOP (1) VehicleId, LocationId, 'LowFuel', N'Yakıt seviyesi düşük: %8,4'
FROM dbo.LocationRecords
WHERE VehicleId = 3
ORDER BY RecordedAt DESC;
GO

-- Bölgeler: üç simülatör rotasının ucunda (araçlar orada dönüp geri gelir, bölgeye girip çıkarlar)
INSERT INTO dbo.Geofences (Name, Latitude, Longitude, RadiusMeters, AlarmOnEnter, AlarmOnExit)
VALUES
    (N'Yeşilköy Depo',            40.961988, 28.830199, 400, 0, 1),   -- depodan ayrılınca haber ver
    (N'Sarıyer Teslimat Noktası', 41.164643, 29.051411, 400, 1, 0),   -- müşteriye varınca haber ver
    (N'Beykoz Şantiye',           41.132845, 29.097354, 400, 1, 1);   -- giriş ve çıkış
GO

PRINT N'Örnek veriler eklendi.';
GO
