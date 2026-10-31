# FiloTakip

Araç filosu takip sistemi. Staj öncesi pratik projesi.

Araçlardaki GPS cihazlarını taklit eden bir simülatör, konumları API'ye gönderir. API konumları
veritabanına kaydeder, hız sınırı aşılınca alarm üretir. Web arayüzü araçları canlı olarak haritada
ve listede gösterir.

```text
 Simülatör (Python)  ──POST konum──▶  API (.NET)  ◀──▶  SQL Server
                                        ▲
 Web arayüzü (Angular)  ──3 sn'de bir──┘
```

| Klasör | Teknoloji | Açıklama |
| --- | --- | --- |
| `database/` | MSSQL | Tablolar, view, örnek veriler |
| `backend/` | .NET 10 Web API, EF Core, xUnit | REST API ve birim testleri |
| `frontend/` | Angular 22, Leaflet | Harita, araç listesi, alarmlar |
| `simulator/` | Python | Araçlardan konum gönderen simülatör |
| `reports/` | Python, pandas | Raporlar (yapım aşamasında) |

## Gerekenler

- .NET 10 SDK
- SQL Server (`localhost`, Windows kimlik doğrulaması)
- Node.js 24 ve Angular CLI 22 (`npm install -g @angular/cli`)
- Python 3.14
- ODBC Driver 18 for SQL Server (sadece raporlar için)

## Kurulum ve çalıştırma

Her parçayı ayrı bir terminalde çalıştır. Sıra: veritabanı → API → arayüz → simülatör.

### 1. Veritabanı

```powershell
cd database
sqlcmd -S localhost -E -C -i 01_schema.sql
sqlcmd -S localhost -E -C -i 02_seed.sql
```

> `01_schema.sql` veritabanını silip baştan oluşturur. Çalıştırmadan önce API'yi durdur.

### 2. API

```powershell
cd backend/FiloTakip.Api
dotnet run
```

- API: <http://localhost:5275>
- Swagger (tarayıcıdan deneme): <http://localhost:5275/swagger>

### 3. Web arayüzü

```powershell
cd frontend
npm install      # sadece ilk seferde: paketleri indirir
ng serve
```

Arayüz: <http://localhost:4200>

### 4. Simülatör

İlk seferde sanal ortamı kur:

```powershell
cd simulator
python -m venv .venv
.\.venv\Scripts\activate
pip install -r requirements.txt
```

Sonra çalıştır (durdurmak için Ctrl+C):

```powershell
cd simulator
.\.venv\Scripts\activate
python simulator.py --all --speedup 10
```

| Komut | Ne yapar |
| --- | --- |
| `python simulator.py` | Araç listesini gösterir |
| `python simulator.py --all` | Bütün aktif araçları sürer |
| `python simulator.py --vehicle 3` | Sadece 3 numaralı aracı sürer |
| `--interval 1` | Her 1 saniyede konum gönderir (varsayılan 3) |
| `--steps 20` | 20 tur sonra durur (varsayılan: Ctrl+C'ye kadar) |
| `--speedup 10` | Zaman 10 kat hızlı akar, haritada hareket belirgin olur |
| `--api-url http://...` | Başka adresteki API'ye gönderir |
| `python simulator.py --help` | Bütün seçenekleri listeler |

Araçlar `routes.json` dosyasındaki gerçek yollar üzerinde gider. Rotaları değiştirirsen
`python build_routes.py` ile yeniden oluştur. Bu komut OSRM yol tarifi servisine internetten istek atar.

### 5. Raporlar (yapım aşamasında)

```powershell
cd reports
python -m venv .venv
.\.venv\Scripts\activate
pip install -r requirements.txt
python db.py     # veritabanı bağlantısını dener
```

## Testler

```powershell
dotnet test backend/FiloTakip.slnx          # .NET birim testleri (alarm kuralları)
cd frontend; ng test --watch=false          # Angular testleri
```

> .NET testlerini çalıştırmadan önce API'yi durdur, yoksa derleme dosyaları kilitli olur.

## Ayarlar

| Ayar | Nerede |
| --- | --- |
| Veritabanı bağlantısı | `backend/FiloTakip.Api/appsettings.json` → `ConnectionStrings:FiloTakip` |
| API'ye istek atabilecek siteler (CORS) | `backend/FiloTakip.Api/appsettings.Development.json` → `Cors:AllowedOrigins` |
| Arayüzün kullandığı API adresi | `frontend/src/environments/environment.development.ts` → `apiUrl` |
| Simülatörün kullandığı API adresi | `--api-url` ya da `FILOTAKIP_API_URL` ortam değişkeni (varsayılan `http://localhost:5275`) |

## API

| İstek | Açıklama |
| --- | --- |
| `GET /api/vehicles` | Bütün araçlar |
| `GET /api/vehicles/{id}` | Tek araç |
| `GET /api/vehicles/last-locations` | Her aracın son konumu (harita ve liste bunu kullanır) |
| `POST /api/locations` | Yeni konum kaydı (simülatör bunu kullanır), gerekirse alarm üretir |
| `GET /api/locations/{id}` | Tek konum kaydı |
| `GET /api/alarms?unreadOnly=true&take=50` | En yeni alarmlar |
| `GET /api/alarms/unread-count` | Okunmamış alarm sayısı |
| `POST /api/alarms/{id}/read` | Alarmı okundu işaretler |
| `POST /api/alarms/read-all` | Bütün alarmları okundu işaretler |

## Alarm kuralları

- **Hız alarmı:** Hız, sınırı 5 km/s'ten fazla aşınca üretilir (tolerans). Araç sınırın üstünde
  kalmaya devam ettikçe yeni alarm üretilmez, sadece sınırın altından üstüne geçtiği an sayılır.
  Aynı araç için 5 dakikada en fazla bir hız alarmı üretilir.
- **Liste ve harita:** Hız sınırın üstündeyse araç toleranssız olarak kırmızı gösterilir.

## Proje yapısı

### backend/FiloTakip.Api

| Klasör | Görevi |
| --- | --- |
| `Controllers/` | HTTP isteğini alır, servise verir, sonucu HTTP koduna çevirir |
| `Services/` | İş mantığı: konum kaydetme, alarm kuralları, sorgular |
| `Data/` | EF Core veritabanı bağlantısı (`FiloTakipDbContext`) |
| `Models/` | Veritabanı tablolarının C# karşılıkları |
| `Dtos/` | API'nin aldığı ve döndüğü veri biçimleri |

Birim testleri `backend/FiloTakip.Api.Tests` içinde.

### frontend/src/app

| Klasör | Görevi |
| --- | --- |
| `vehicles/` | Araç modeli, API servisi, canlı veri, araç listesi sayfası |
| `alarms/` | Alarm modeli, API servisi, okunmamış sayısı, alarm listesi sayfası |
| `map/` | Leaflet haritası sayfası |
| `shared/` | Birden fazla sayfanın kullandığı parçalar (API hata kutusu) |

### simulator

| Dosya | Görevi |
| --- | --- |
| `simulator.py` | Komut satırı ve ana döngü |
| `vehicle.py` | Aracın hareketi (konum, hız, yakıt) |
| `api_client.py` | API ile konuşma (HTTP istekleri) |
| `build_routes.py` | Rotaları OSRM ile gerçek yollara oturtur, `routes.json` dosyasını yazar |

## Tablolar

- **Drivers**: sürücüler
- **Vehicles**: araçlar (plaka, hız sınırı, atanmış sürücü)
- **LocationRecords**: araçlardan gelen konum / hız / yakıt kayıtları
- **Alarms**: hız ihlali, düşük yakıt gibi uyarılar
- **vw_VehicleLastLocation**: her aracın son konumu (view)
