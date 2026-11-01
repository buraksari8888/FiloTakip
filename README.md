# FiloTakip

Araç filosu takip sistemi. Staj öncesi pratik projesi.

Araçlardaki GPS cihazlarını taklit eden bir simülatör, konumları API'ye gönderir. API konumları
veritabanına kaydeder, aracın bulunduğu yolun ya da aracın kendi hız sınırı aşılınca, yakıt azalınca, araç uzun süre rölantide kalınca ya da bir bölgeye girip çıkınca alarm üretir. Web arayüzü araçları canlı olarak haritada
ve listede gösterir.

```text
 Simülatör (Python)  ──── POST konum ────▶  API (.NET)  ◀──▶  SQL Server
                                             │     ▲
                   canlı bildirim (SignalR)  │     │  HTTP: ilk yükleme, okundu işaretleme
                                             ▼     │
                                      Web arayüzü (Angular)
```

| Klasör | Teknoloji | Açıklama |
| --- | --- | --- |
| `database/` | MSSQL | Tablolar, view, örnek veriler |
| `backend/` | .NET 10 Web API, EF Core, xUnit | REST API ve birim testleri |
| `frontend/` | Angular 22, Leaflet | Harita, araç listesi, alarmlar |
| `simulator/` | Python | Araçlardan konum gönderen simülatör |
| `reports/` | Python, pandas, openpyxl | Günlük filo raporu (ekrana ve Excel'e) |

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
sqlcmd -S localhost -E -C -i 03_reports.sql
sqlcmd -S localhost -E -C -i 07_road_segments.sql
```

> `01_schema.sql` veritabanını silip baştan oluşturur. Çalıştırmadan önce API'yi durdur.
> Sonra diğerlerini de tekrar çalıştır (`03_reports.sql` rapor yordamını, `07_road_segments.sql` yolların hız sınırlarını ekler).
>
> Veritabanın daha eski bir sürümle oluşturulduysa ve verilerini silmek istemiyorsan `01_schema.sql` yerine
> güncelleme betiklerini sırayla çalıştır (birden fazla çalıştırmak zararsız):
>
> ```powershell
> sqlcmd -S localhost -E -C -i 04_fuel_drain.sql   # yakıt hırsızlığı alarmı
> sqlcmd -S localhost -E -C -i 05_geofences.sql    # bölgeler ve bölge alarmları
> sqlcmd -S localhost -E -C -i 06_road_speed_limits.sql   # yola göre hız sınırı (tablo ve sütun)
> sqlcmd -S localhost -E -C -i 07_road_segments.sql       # yol parçaları ve sınırları
> ```

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

| Sayfa | Adres | İçerik |
| --- | --- | --- |
| Harita | `/harita` | Bütün araçlar canlı olarak haritada. Bir araca tıklanınca son 30 dakikada geçtiği yol mavi çizgiyle gösterilir: iz yeni konum geldikçe uzar, eski kısmı soluk çizilir, 2 dakikadan uzun boşluk ya da 2 km'den büyük sıçramada kopar. Bölgeler kesik çizgili lacivert daire olarak çizilir; ortasında adı ve hangi geçişte alarm verdiği yazar |
| | `/harita?arac=3` | O araca yakınlaşır, bilgi balonunu ve izini açar |
| | `/harita?alarm=46` | Alarmın olduğu yeri kırmızı halkayla gösterir (aracın şu anki yeri değil) |
| Araçlar | `/araclar` | Araçların son durumu; her satırda "Haritada göster" |
| Alarmlar | `/alarmlar` | Alarmlar, okundu işaretleme; her satırda "Haritada göster" |

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
| `--log-level ERROR` | Sadece sorunları yazar (bkz. [Loglar](#loglar)) |
| `python simulator.py --help` | Bütün seçenekleri listeler |

Araçlar duraklarda ara sıra durur. Bazı duruşlarda bir senaryo oynanır:

| Senaryo | Sıklık | Ne olur | Terminalde |
| --- | --- | --- | --- |
| Hırsızlık | Duruşların %3'ü | Kontak kapanır, 2-3 adım boyunca depodan her adımda %6-10 yakıt çekilir; API yakıt kaybı alarmı üretir | `⛽ DEPODAN YAKIT ÇEKİLİYOR` |
| Uzun rölanti | Duruşların %3'ü | Motor çalışır halde 110-160 adım beklenir (3 saniyelik aralıkla 5,5-8 dakika); 5. dakikada API rölanti alarmı üretir | `🕒 MOTOR ÇALIŞIR HALDE UZUN BEKLEME BAŞLADI` |

Rölanti süresi gerçek saatle ölçüldüğü için `--speedup` bu beklemeyi kısaltmaz.

Örnek bölgeler (`02_seed.sql`) üç rotanın ucunda: Yeşilköy Depo (sahil yolu), Sarıyer Teslimat Noktası
(Boğaz Avrupa), Beykoz Şantiye (Boğaz Anadolu). Araçlar rotanın ucunda dönüp geri geldiği için bu bölgelere
girip çıkarlar. `--speedup 10` ile araçlar rotalarını birkaç dakikada bir baştan sona gittiğinden bölge
alarmları sık gelir (filoda saatte ~30); gerçek zamanda bunun onda biri olur.

Araçlar `routes.json` dosyasındaki gerçek yollar üzerinde, yolun hız sınırına göre gider: sınırın biraz
altında, önünde (250 m içinde) sınır düşüyorsa şimdiden yavaşlayarak. Ara sıra (~5 dakikada bir) sınırı
12-30 km/s aşarlar (hız ihlali senaryosu). Rotaları değiştirirsen `python build_routes.py` ile yeniden
oluştur, sonra `database/07_road_segments.sql`'i çalıştır. Bu komut internetten OSRM (yol tarifi) ve
Overpass (OpenStreetMap sorgusu) servislerine istek atar.

### 5. Raporlar

İlk seferde sanal ortamı kur:

```powershell
cd reports
python -m venv .venv
.\.venv\Scripts\activate
pip install -r requirements.txt
python db.py     # veritabanı bağlantısını dener
```

Günlük filo raporu: her aracın o günkü mesafesi, hareket süresi, ortalama ve en yüksek hızı, hız alarmı
sayısı ve yakıt tüketimi.

| Komut | Ne yapar |
| --- | --- |
| `python daily_report.py` | Bugünün raporunu ekrana yazar (gün İstanbul saatine göre) |
| `python daily_report.py --date 2026-10-30` | Belirli bir günün raporu |
| `--excel` | Ayrıca `filo-raporu-<gün>.xlsx` dosyasına kaydeder (başlıklar, filtre, toplam satırı) |
| `--excel C:\Raporlar\rapor.xlsx` | Ayrıca bu Excel dosyasına kaydeder |

Hesabın asıl kısmı veritabanında: `dbo.usp_DailyFleetReport` saklı yordamı (`database/03_reports.sql`).
SSMS'ten de denenebilir (İstanbul saatiyle 30.10.2026 = UTC 29.10 21:00 – 30.10 21:00):

```sql
EXEC dbo.usp_DailyFleetReport @StartUtc = '2026-10-29 21:00', @EndUtc = '2026-10-30 21:00';
```

Mesafe ardışık konumlar arasındaki gerçek uzaklıkların toplamıdır (`geography`). Şunlar sayılmaz:

- 2 dakikadan uzun aralar (cihaz kapalıydı, aradaki yol bilinmiyor)
- 2 km'den uzun tek adımlar (GPS sıçraması ya da simülatör başka bir yerden başladı)
- Tek adımda %5'ten büyük yakıt düşüşleri (tüketim değil; depo dolumu da sayılmaz)

> Simülatör `--speedup` ile çalıştıysa araçlar gerçek zamandan hızlı ilerler: rapordaki mesafe, hareket
> süresine göre çok yüksek görünür.

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
| Arayüzün kullandığı API adresleri | `frontend/src/environments/environment.development.ts` → `apiUrl`, `hubUrl` (canlı bildirim) |
| Simülatörün kullandığı API adresi | `--api-url` ya da `FILOTAKIP_API_URL` ortam değişkeni (varsayılan `http://localhost:5275`) |
| API'nin log seviyeleri | `backend/FiloTakip.Api/appsettings.json` → `Logging:LogLevel` |
| Simülatörün log seviyesi | `--log-level` (varsayılan `INFO`) |

## Loglar

API'nin terminalindeki her satır saat, seviye ve kaynakla (logu yazan sınıf) başlar:

```text
14:03:12 info: FiloTakip.Api.Services.LocationService[0] Alarm #47 (SpeedLimit): 34 ABC 123 Hız sınırı aşıldı: 98,5 km/s (araç sınırı 90)
14:03:15 warn: FiloTakip.Api.Services.LocationService[0] Konum reddedildi: 999 numaralı araç yok
```

| Seviye | API ne zaman yazar |
| --- | --- |
| Debug (normalde gizli) | Her kaydedilen konum |
| Information | Alarm üretildi (hız, düşük yakıt, yakıt kaybı, rölanti, bölge giriş/çıkış), alarm(lar) okundu işaretlendi |
| Warning | Olmayan bir araç için konum geldi |

Seviyeler `appsettings.json` → `Logging:LogLevel` içinde, kaynağa göre ayarlanır. EF Core'un SQL sorguları
gürültü olmasın diye `Warning`'e kısıldı; sorguları görmek için `Microsoft.EntityFrameworkCore.Database.Command`
satırını `Information` yap.

Kendi kodumuzun Debug loglarını dosyayı değiştirmeden açmak için (terminal kapanınca ayar da gider):

```powershell
$env:Logging__LogLevel__FiloTakip = "Debug"
dotnet run
```

Simülatör de aynı biçimde yazar: `14:03:12 INFO    34 ABC 123   #8040  41.01595, 28.98611   67.3 km/s ...`.
Hız ihlali simülatör için normal bir olay olduğundan INFO seviyesinde, `⚠️ HIZ SINIRI AŞILDI` işaretiyle yazılır.
Konum gönderilemezse ERROR yazılır. `--log-level ERROR` sadece sorunları, `--log-level DEBUG` HTTP isteklerini de gösterir.

## Hatalar

API bütün hataları aynı biçimde döner (ProblemDetails, RFC 9457):

```json
{ "title": "Sunucuda beklenmeyen bir hata oluştu.", "status": 500, "traceId": "00-a14b7ac8dd3d5adc...-00" }
```

| Kod | Ne zaman |
| --- | --- |
| 400 | Gönderilen veri geçersiz; `errors` alanı hangi alanın neden reddedildiğini yazar |
| 404 | Kayıt ya da adres yok |
| 500 | Beklenmeyen hata (ör. veritabanı kapalı). Kullanıcıya ayrıntı gösterilmez, ayrıntı API'nin logundadır |

**Hata kodu (traceId) ile hatayı bulmak:** Arayüz sunucu hatalarında "Hata kodu: 00-..." gösterir, simülatör de
aynı numarayı yazar. Bu numara API'nin logunda aratılırsa hatanın tamamı (yığın izi dahil) bulunur.

- **Arayüz:** API'ye hiç ulaşılamıyorsa "API'ye ulaşılamıyor", API cevap verip hata döndüyse "Sunucuda bir hata
  oluştu" ve hata kodu gösterilir. Alarmı okundu işaretleme başarısız olursa hangi işlemin başarısız olduğu yazar.
- **Simülatör:** Bir konum kaydedilemezse hatayı yazar ve sonraki konumla devam eder. API 5 saniyede cevap
  vermezse vazgeçer. Açılışta bir hata olursa çıkış kodu 1 ile biter, başarılıysa 0 (`$LASTEXITCODE`).

## API

| İstek | Açıklama |
| --- | --- |
| `GET /api/vehicles` | Bütün araçlar |
| `GET /api/vehicles/{id}` | Tek araç |
| `GET /api/vehicles/last-locations` | Her aracın son konumu (harita ve liste bunu kullanır) |
| `GET /api/vehicles/{id}/locations?minutes=30` | Aracın son X dakikadaki konumları, eskiden yeniye (haritadaki iz; `minutes` 1–1440) |
| `POST /api/locations` | Yeni konum kaydı (simülatör bunu kullanır), gerekirse alarm üretir |
| `GET /api/locations/{id}` | Tek konum kaydı |
| `GET /api/alarms?unreadOnly=true&take=50` | En yeni alarmlar |
| `GET /api/alarms/{id}` | Tek alarm (konumuyla birlikte) |
| `GET /api/alarms/unread-count` | Okunmamış alarm sayısı |
| `POST /api/alarms/{id}/read` | Alarmı okundu işaretler |
| `POST /api/alarms/read-all` | Bütün alarmları okundu işaretler |
| `GET /api/geofences` | Aktif bölgeler (merkez, yarıçap, girişte/çıkışta alarm) |
| `/hubs/live` | Canlı bildirim kanalı (SignalR, WebSocket), bkz. [Canlı güncelleme](#canlı-güncelleme-signalr) |

## Canlı güncelleme (SignalR)

Arayüz API'ye tekrar tekrar sormaz. Açılışta verileri bir kez HTTP ile çeker, sonra API'nin canlı bildirim
kanalına bağlanır; API yeni bir şey olunca bağlı bütün ekranlara kendisi haber verir:

| Mesaj | Ne zaman | Arayüz ne yapar |
| --- | --- | --- |
| `LocationAdded` | Yeni konum kaydedildi | Liste ve haritada sadece o aracı günceller |
| `AlarmsChanged` | Alarm üretildi ya da okundu işaretlendi | Rozeti ve alarm listesini yeniden çeker |

- Üst menünün sağındaki nokta bağlantıyı gösterir: yeşil **Canlı**, sarı **Bağlanıyor…**, kırmızı **Bağlantı yok**.
- Bağlantı koparsa arayüz 5 saniyede bir yeniden dener. Bağlantı geri gelince kopukken kaçan değişiklikler için
  listeleri bir kez baştan çeker.
- Gelen konum ekrandakinden eskiyse (cihazın bağlantısızken biriktirip sonradan gönderdiği konum) yok sayılır.

## Alarm kuralları

- **Hız alarmı:** Hız, geçerli sınırı 5 km/s'ten fazla aşınca üretilir (tolerans). Araç sınırın üstünde
  kalmaya devam ettikçe yeni alarm üretilmez, sadece sınırın altından üstüne geçtiği an sayılır
  (aynı hızla 90'lık yoldan 50'lik yola girmek de yeni bir ihlaldir). Aynı araç için 5 dakikada en fazla
  bir hız alarmı üretilir.
- **Geçerli hız sınırı:** Aracın bulunduğu yolun sınırı ile aracın kendi sınırından (ör. kamyon 80) küçük
  olanı. GPS cihazı yolun sınırını bilmez; API konuma en yakın yol parçasını (`RoadSegments`) bulur ve
  sınırını konum kaydına (`RoadSpeedLimitKmh`) yazar. Konum 30 metre içinde bilinen bir yolda değilse aracın
  kendi sınırı geçerlidir. Yol parçaları ve sınırları OpenStreetMap'ten gelir (`maxspeed` etiketi; etiket
  yoksa yol türüne göre Türkiye'deki genel sınır: otoyol 120, ana yol 90, şehir içi 50). Alarm mesajı hangi
  sınırın aşıldığını söyler: "(yol sınırı 50)" ya da "(araç sınırı 80)".
- **Düşük yakıt alarmı:** Yakıt %15'in altına indiği an üretilir. Yakıt düşük kaldıkça (depo dolana
  kadar) yeni alarm üretilmez. Yakıt sensörü dalgalanabildiği için (ör. %14,9 → %15,2 → %14,8) aynı
  araç için 10 dakikada en fazla bir düşük yakıt alarmı üretilir. Yakıt bilgisi göndermeyen cihazlarda bu alarm yoktur.
- **Yakıt kaybı (hırsızlık şüphesi):** Araç dururken (hız 3 km/s'in altında; GPS duran araçta bile 1-2 km/s
  gösterebilir) yakıt, durduğu andan beri 5 puan ya da daha fazla düşerse üretilir. Duran araçta yakıt
  neredeyse hiç harcanmaz, sensör dalgalanması da ±1-2 puandır. Araç giderken bakılmaz (yakıt zaten harcanır,
  depo çalkalanır). Karşılaştırma duruştaki en yüksek yakıtla yapılır: araç dururken depo doldurulup
  sonra boşaltılırsa da yakalanır. Duruş başına tek alarm üretilir; alarm, düşüş 5 puanı geçtiği an üretildiği
  için mesajdaki miktar o anki düşüştür (çekme devam ettiyse toplam kayıp daha fazla olabilir).
- **Rölanti alarmı:** Kontak açık ve araç duruyorsa (hız 3 km/s'in altında) motor boşa yakıt yakıyor demektir.
  Kırmızı ışık ya da kısa bekleme normaldir; bu durum 5 dakika sürerse alarm üretilir. Süre konum kayıtlarının
  saatine göre ölçülür. Araç hareket edince ya da kontak kapanınca rölanti biter, sonraki rölanti yeniden
  sayılır. Rölanti başına tek alarm üretilir. Kontak kapalı park halinde alarm yoktur.
- **Bölge alarmı (geofence):** Bölge, merkezi ve yarıçapı olan bir dairedir; bütün araçlar için geçerlidir.
  Aracın önceki konumu bölgenin dışında, yeni konumu içindeyse "bölgeye giriş", tersiyse "bölgeden çıkış"
  alarmı üretilir. Araç bölgenin içinde ya da dışında kaldıkça yeni alarm üretilmez. Her bölge için hangi
  geçişte alarm istendiği ayrı seçilir (ör. depo için sadece çıkış, müşteri için sadece varış). Aracın ilk
  konumu bölgenin içindeyse alarm yoktur (karşılaştıracak önceki konum yok). Konumlar arasında araç
  bölgenin içinden geçip çıkmışsa (iki konum da dışarıda) bu fark edilmez; bölge yarıçapı, cihazın iki konum
  arasında gittiği yoldan büyük seçilmelidir.
- **Liste ve harita:** Hız geçerli sınırın üstündeyse araç toleranssız olarak kırmızı, yakıt %15'in altındaysa turuncu gösterilir.

## Proje yapısı

### backend/FiloTakip.Api

| Klasör | Görevi |
| --- | --- |
| `Controllers/` | HTTP isteğini alır, servise verir, sonucu HTTP koduna çevirir |
| `Services/` | İş mantığı: konum kaydetme, alarm kuralları, sorgular |
| `Data/` | EF Core veritabanı bağlantısı (`FiloTakipDbContext`) |
| `Models/` | Veritabanı tablolarının C# karşılıkları |
| `Dtos/` | API'nin aldığı ve döndüğü veri biçimleri |
| `Errors/` | Yakalanmamış hataları loglayıp standart 500 cevabı dönen `GlobalExceptionHandler` |
| `Hubs/` | SignalR canlı bildirim kanalı (`LiveHub`) ve gönderilebilecek mesajlar (`ILiveClient`) |

Birim testleri `backend/FiloTakip.Api.Tests` içinde.

### frontend/src/app

| Klasör | Görevi |
| --- | --- |
| `vehicles/` | Araç modeli, API servisi, canlı veri, araç listesi sayfası |
| `alarms/` | Alarm modeli, API servisi, okunmamış sayısı, alarm listesi sayfası |
| `geofences/` | Bölge modeli ve API servisi (bölgeler haritada çizilir) |
| `map/` | Leaflet haritası sayfası (ayrı dosyada, sadece bu sayfa açılınca yüklenir) |
| `core/` | Bütün uygulamanın paylaştığı tekil servisler (API'ye canlı bağlantı: `LiveHub`) |
| `shared/` | Birden fazla sayfanın kullandığı parçalar (hataya göre mesaj gösteren API hata kutusu) |

### simulator

| Dosya | Görevi |
| --- | --- |
| `simulator.py` | Komut satırı ve ana döngü |
| `vehicle.py` | Aracın hareketi (konum, hız, yakıt) |
| `api_client.py` | API ile konuşma (HTTP istekleri); hata cevaplarını `ApiError`'a çevirir |
| `build_routes.py` | Rotaları OSRM ile gerçek yollara oturtur, yolların hız sınırlarını Overpass'tan alır; `routes.json` ve `database/07_road_segments.sql` dosyalarını yazar |

### reports

| Dosya | Görevi |
| --- | --- |
| `db.py` | Veritabanı bağlantısı (SQLAlchemy + ODBC); tek başına çalışınca bağlantıyı dener |
| `daily_report.py` | Günlük filo raporu: günün UTC aralığını hesaplar, yordamı çağırır, ekrana ve Excel'e yazar |

### database

| Dosya | Görevi |
| --- | --- |
| `01_schema.sql` | Veritabanı, tablolar, index'ler, view (veritabanını silip baştan oluşturur) |
| `02_seed.sql` | Örnek sürücüler, araçlar ve bölgeler |
| `03_reports.sql` | Rapor saklı yordamı (`usp_DailyFleetReport`) |
| `04_fuel_drain.sql` | Var olan veritabanına yakıt kaybı alarm türünü ekler (veriyi silmeden) |
| `05_geofences.sql` | Var olan veritabanına bölgeler tablosunu, örnek bölgeleri ve bölge alarm türlerini ekler (veriyi silmeden) |
| `06_road_speed_limits.sql` | Var olan veritabanına yol parçaları tablosunu ve konumlara yol sınırı sütununu ekler (veriyi silmeden) |
| `07_road_segments.sql` | Yol parçaları ve hız sınırları (`build_routes.py` oluşturur, elle değiştirilmez) |

## Tablolar

- **Drivers**: sürücüler
- **Vehicles**: araçlar (plaka, hız sınırı, atanmış sürücü)
- **LocationRecords**: araçlardan gelen konum / hız / yakıt kayıtları (ve API'nin bulduğu yol sınırı)
- **Alarms**: hız ihlali, düşük yakıt gibi uyarılar
- **Geofences**: bölgeler (merkez, yarıçap, girişte/çıkışta alarm)
- **RoadSegments**: yol parçaları (iki nokta arası) ve hız sınırları
- **vw_VehicleLastLocation**: her aracın son konumu (view)
- **usp_DailyFleetReport**: günlük filo raporu (saklı yordam)
