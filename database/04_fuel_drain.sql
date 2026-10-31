/* =========================================================
   FiloTakip - Yakıt hırsızlığı alarmı için veritabanı güncellemesi
   Çalıştırma: sqlcmd -S localhost -E -C -i 04_fuel_drain.sql

   Alarms tablosundaki CK_Alarms_Type kuralına yeni alarm türünü ('FuelDrain') ekler.
   01_schema.sql'de bu tür zaten var: veritabanını baştan oluşturduysan bunu çalıştırman gerekmez.
   Bu betik, veriyi silmeden VAR OLAN bir veritabanını güncellemek için (birden fazla çalıştırmak zararsız).
   ========================================================= */

USE FiloTakip;
GO

-- CHECK kuralı yerinde değiştirilemez: silinip yeni haliyle tekrar eklenir
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Alarms_Type')
    ALTER TABLE dbo.Alarms DROP CONSTRAINT CK_Alarms_Type;

ALTER TABLE dbo.Alarms ADD CONSTRAINT CK_Alarms_Type
    CHECK (AlarmType IN ('SpeedLimit', 'LowFuel', 'Idle', 'FuelDrain'));
GO

PRINT N'Alarms tablosu güncellendi: FuelDrain (yakıt hırsızlığı şüphesi) alarm türü eklendi.';
GO
