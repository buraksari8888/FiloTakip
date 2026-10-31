/* =========================================================
   FiloTakip - Rapor saklı yordamları (stored procedure)
   Çalıştırma: sqlcmd -S localhost -E -C -i 03_reports.sql
   01_schema.sql veritabanını baştan oluşturduğu için onu çalıştırdıktan sonra bunu da tekrar çalıştır.

   Denemek için (SSMS ya da sqlcmd), İstanbul saatiyle 30.10.2026 günü:
       EXEC dbo.usp_DailyFleetReport @StartUtc = '2026-10-29 21:00', @EndUtc = '2026-10-30 21:00';
   ========================================================= */

USE FiloTakip;
GO

/* ---------- Günlük filo raporu: her araç için bir günün özeti ---------- */
CREATE OR ALTER PROCEDURE dbo.usp_DailyFleetReport
    @StartUtc           DATETIME2(0),          -- günün başı (UTC); veritabanındaki saatler UTC
    @EndUtc             DATETIME2(0),          -- günün sonu (UTC, bu an hariç)
    -- İki kayıt arası bundan uzunsa cihaz kapalıydı: aradaki yol bilinmiyor, mesafeye sayılmaz
    @MaxGapSeconds      INT          = 120,
    -- İki kayıt arası bundan uzunsa gerçek yol değil (GPS sıçraması ya da simülatör başka bir yerden başladı).
    -- Ölçüldü: simülatör --speedup 10 ile bir adımda en fazla ~1,1 km gidiyor.
    @MaxJumpKm          FLOAT        = 2.0,
    -- İki kayıt arasında yakıt bundan fazla düştüyse tüketim sayılmaz (bir adımda en fazla ~%0,6 yakılabiliyor)
    @MaxFuelDropPercent DECIMAL(5,2) = 5.0
AS
BEGIN
    SET NOCOUNT ON;   -- "N satır etkilendi" mesajları gönderilmesin, çağıran sadece sonuç tablosunu alsın

    WITH DayRecords AS (
        -- Günün kayıtları ve her kaydın yanında aynı aracın bir önceki kaydı (LAG).
        -- İki kayıt arasındaki yolu ve süreyi hesaplamak için önceki kaydın konumu, yakıtı ve saati lazım.
        SELECT
            VehicleId,
            Latitude,
            Longitude,
            SpeedKmh,
            FuelPercent,
            RecordedAt,
            LAG(Latitude)    OVER (PARTITION BY VehicleId ORDER BY RecordedAt, LocationId) AS PrevLatitude,
            LAG(Longitude)   OVER (PARTITION BY VehicleId ORDER BY RecordedAt, LocationId) AS PrevLongitude,
            LAG(FuelPercent) OVER (PARTITION BY VehicleId ORDER BY RecordedAt, LocationId) AS PrevFuelPercent,
            LAG(RecordedAt)  OVER (PARTITION BY VehicleId ORDER BY RecordedAt, LocationId) AS PrevRecordedAt
        FROM dbo.LocationRecords
        WHERE RecordedAt >= @StartUtc AND RecordedAt < @EndUtc
    ),
    Segments AS (
        -- Ardışık iki kayıt arası bir "parça": kaç saniye sürdü, kaç km gidildi.
        -- geography: Dünya'nın şeklini hesaba katan mesafe (STDistance metre döner; 4326 = GPS'in koordinat sistemi)
        SELECT
            VehicleId,
            SpeedKmh,
            FuelPercent,
            PrevFuelPercent,
            DATEDIFF(SECOND, PrevRecordedAt, RecordedAt) AS Seconds,
            geography::Point(Latitude, Longitude, 4326)
                .STDistance(geography::Point(PrevLatitude, PrevLongitude, 4326)) / 1000.0 AS Km
        FROM DayRecords
        WHERE PrevRecordedAt IS NOT NULL   -- aracın günün ilk kaydının öncesi yok
    ),
    Totals AS (
        -- Uzun boşluklar ve ani sıçramalar yol sayılmaz
        SELECT
            VehicleId,
            SUM(Km) AS DistanceKm,
            SUM(CASE WHEN SpeedKmh > 0 THEN Seconds ELSE 0 END) AS MovingSeconds,
            -- Yakıt: sadece düşüşler toplanır. "İlk - son" hesabı depo doldurulunca (yakıt artınca) yanlış olurdu.
            -- Tek adımda çok büyük düşüş tüketim değildir (sensör sıçraması, cihaz yeniden başladı; gerçek
            -- sistemlerde ani düşüş ayrıca "yakıt hırsızlığı" uyarısı olur).
            SUM(CASE WHEN PrevFuelPercent > FuelPercent AND PrevFuelPercent - FuelPercent <= @MaxFuelDropPercent
                     THEN PrevFuelPercent - FuelPercent ELSE 0 END) AS FuelUsedPercent
        FROM Segments
        WHERE Seconds <= @MaxGapSeconds AND Km <= @MaxJumpKm
        GROUP BY VehicleId
    ),
    Stats AS (
        -- Kayıt sayısı ve hızlar bütün kayıtlardan (parçalardan değil)
        SELECT
            VehicleId,
            COUNT(*) AS Records,
            MAX(SpeedKmh) AS MaxSpeedKmh,
            AVG(CASE WHEN SpeedKmh > 0 THEN SpeedKmh END) AS AvgMovingSpeedKmh   -- dururkenki 0'lar ortalamayı düşürmesin
        FROM DayRecords
        GROUP BY VehicleId
    ),
    SpeedAlarms AS (
        SELECT VehicleId, COUNT(*) AS SpeedAlarms
        FROM dbo.Alarms
        WHERE AlarmType = 'SpeedLimit' AND CreatedAt >= @StartUtc AND CreatedAt < @EndUtc
        GROUP BY VehicleId
    )
    SELECT
        v.Plate,
        d.FirstName + N' ' + d.LastName AS Driver,
        ISNULL(s.Records, 0)            AS Records,
        ISNULL(t.DistanceKm, 0)         AS DistanceKm,
        ISNULL(t.MovingSeconds, 0)      AS MovingSeconds,
        s.AvgMovingSpeedKmh,
        s.MaxSpeedKmh,
        ISNULL(a.SpeedAlarms, 0)        AS SpeedAlarms,
        ISNULL(t.FuelUsedPercent, 0)    AS FuelUsedPercent
    FROM dbo.Vehicles AS v
    LEFT JOIN dbo.Drivers AS d ON d.DriverId = v.DriverId
    LEFT JOIN Stats       AS s ON s.VehicleId = v.VehicleId
    LEFT JOIN Totals      AS t ON t.VehicleId = v.VehicleId
    LEFT JOIN SpeedAlarms AS a ON a.VehicleId = v.VehicleId
    WHERE v.IsActive = 1 OR s.VehicleId IS NOT NULL   -- aktif araçların hepsi (o gün hiç çalışmadıysa da görünsün)
    ORDER BY v.Plate;
END
GO

PRINT N'Rapor yordamları oluşturuldu.';
GO
