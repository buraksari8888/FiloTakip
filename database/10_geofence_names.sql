/* =========================================================
   FiloTakip - "Aktif bölgelerin adı tekrar edemez" kuralı
   Çalıştırma: sqlcmd -S localhost -E -C -i 10_geofence_names.sql

   Bölgeler artık arayüzden eklenip düzenlenebiliyor. Alarm mesajında bölgenin adı geçtiği için ("Beykoz Şantiye
   bölgesine girdi") iki aktif bölgenin adı aynı olamaz; silinmiş (pasif) bölgeler hariç. API bunu önceden kontrol
   ediyor ama iki kullanıcı aynı anda aynı adı kaydederse ikisi de geçebilirdi, kuralı veritabanı uygular.
   Önce varsa çakışmaları düzeltir: aynı ad birden fazla aktif bölgedeyse sonrakilerin adına " (2)", " (3)" eklenir.
   01_schema.sql bunu zaten içeriyor: veritabanını baştan oluşturduysan bunu çalıştırman gerekmez.
   Veriyi silmeden VAR OLAN bir veritabanını güncellemek için (birden fazla çalıştırmak zararsız).
   ========================================================= */

USE FiloTakip;
GO

WITH ranked AS
(
    SELECT Name, ROW_NUMBER() OVER (PARTITION BY Name ORDER BY GeofenceId) AS rn
    FROM dbo.Geofences
    WHERE IsActive = 1
)
UPDATE ranked SET Name = LEFT(Name, 90) + N' (' + CAST(rn AS NVARCHAR(10)) + N')' WHERE rn > 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Geofences_Name' AND object_id = OBJECT_ID(N'dbo.Geofences'))
    CREATE UNIQUE INDEX UX_Geofences_Name ON dbo.Geofences (Name) WHERE IsActive = 1;
GO

PRINT N'UX_Geofences_Name hazır: aktif bölgelerin adı tekrar edemez.';
GO
