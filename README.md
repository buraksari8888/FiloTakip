# FiloTakip

Araç filosu takip sistemi. Staj öncesi pratik projesi.

| Klasör | Teknoloji | Açıklama |
|---|---|---|
| `database/` | MSSQL | Tablolar, view'lar, örnek veriler |
| `backend/` | .NET 10 Web API | REST API (EF Core) |
| `frontend/` | Angular | Harita ve araç listesi arayüzü |
| `simulator/` | Python | Araçlardan konum verisi gönderen simülatör |

## Veritabanını kurma

```powershell
cd database
sqlcmd -S localhost -E -C -i 01_schema.sql
sqlcmd -S localhost -E -C -i 02_seed.sql
```

> `01_schema.sql` veritabanını silip baştan oluşturur.

## Tablolar

- **Drivers**: sürücüler
- **Vehicles**: araçlar (plaka, hız sınırı, atanmış sürücü)
- **LocationRecords**: araçlardan gelen konum / hız / yakıt kayıtları
- **Alarms**: hız ihlali, düşük yakıt gibi uyarılar
- **vw_VehicleLastLocation**: her aracın son konumu (view)
