/* =========================================================
   FiloTakip - "Bir sürücü aynı anda tek araçta" kuralı
   Çalıştırma: sqlcmd -S localhost -E -C -i 09_driver_one_vehicle.sql

   API sürücüyü başka bir araca atarken eski araçtan alıyor; ama iki kullanıcı aynı sürücüyü aynı anda iki
   farklı araca atarsa ikisi de geçebiliyordu. Bu betik kuralı veritabanına koyar: sürücüsü olan araçlarda
   DriverId tekrar edemez (sürücüsüz araçlar, NULL, hariç). Önce varsa çakışmaları temizler: aynı sürücü
   birden fazla araçtaysa en küçük numaralı araçta kalır, diğerleri sürücüsüz olur.
   01_schema.sql bunu zaten içeriyor: veritabanını baştan oluşturduysan bunu çalıştırman gerekmez.
   Veriyi silmeden VAR OLAN bir veritabanını güncellemek için (birden fazla çalıştırmak zararsız).
   ========================================================= */

USE FiloTakip;
GO

WITH ranked AS
(
    SELECT DriverId, ROW_NUMBER() OVER (PARTITION BY DriverId ORDER BY VehicleId) AS rn
    FROM dbo.Vehicles
    WHERE DriverId IS NOT NULL
)
UPDATE ranked SET DriverId = NULL WHERE rn > 1;
GO

-- Eski (tekrara izin veren) index'in yerine
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Vehicles_DriverId' AND object_id = OBJECT_ID(N'dbo.Vehicles'))
    DROP INDEX IX_Vehicles_DriverId ON dbo.Vehicles;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Vehicles_DriverId' AND object_id = OBJECT_ID(N'dbo.Vehicles'))
    CREATE UNIQUE INDEX UX_Vehicles_DriverId ON dbo.Vehicles (DriverId) WHERE DriverId IS NOT NULL;
GO

PRINT N'UX_Vehicles_DriverId hazır: bir sürücü aynı anda tek araçta.';
GO
