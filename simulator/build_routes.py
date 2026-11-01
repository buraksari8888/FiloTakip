"""Rotaları gerçek yollara oturtur ve yolların hız sınırlarını bulur.

Her rota için başlangıçtan bitişe araba güzergâhını OSRM'den (OpenStreetMap verisiyle çalışan
ücretsiz yol tarifi servisi) alır. Güzergâhtaki her yol parçasının hız sınırını da Overpass'tan
(OpenStreetMap verisini sorgulama servisi) öğrenir. İki dosya yazar:
    routes.json                          simülatör rotaları ve hız sınırlarını buradan okur
    ../database/07_road_segments.sql     API'nin hız alarmı için kullandığı yol parçaları (RoadSegments tablosu)

Bir kez çalıştırmak yeterli, rotaları değiştirirsen tekrar çalıştır (sonra 07_road_segments.sql'i de çalıştır):
    python build_routes.py
"""

import json
import time
from pathlib import Path

import requests

OSRM_URL = "https://router.project-osrm.org/route/v1/driving"
OVERPASS_URL = "https://overpass-api.de/api/interpreter"
USER_AGENT = "FiloTakip-ogrenci-projesi"
OUTPUT_FILE = Path(__file__).with_name("routes.json")
SQL_FILE = Path(__file__).parent.parent / "database" / "07_road_segments.sql"

# OpenStreetMap'te yolun maxspeed etiketi yoksa yol türüne göre Türkiye'deki genel sınır kullanılır
# (Karayolları Trafik Yönetmeliği, otomobil): otoyol 120, ana yol (bölünmüş yol) 90, şehir içi 50.
DEFAULT_LIMITS = {"motorway": 120, "trunk": 90}
URBAN_LIMIT = 50
# maxspeed bazen sayı yerine bölge kodu olarak yazılır
ZONE_LIMITS = {"TR:urban": 50, "TR:rural": 90, "TR:trunk": 110, "TR:motorway": 120}

# Her rota: ad, duraklar, ara noktalar (koordinatlar: enlem, boylam).
#
# - Güzergâh ilk duraktan son durağa çizilir. OSRM en hızlı yolu seçtiği için istediğimiz caddeden
#   geçmesi gereken yerlerde "ara nokta" veriyoruz. Ara noktalar, OSRM'in "nearest" servisiyle
#   doğru caddenin üstünde olduğu kontrol edilmiş koordinatlar.
# - Duraklar (araçların ara sıra durduğu yerler) güzergâhtaki en yakın noktaya yerleştirilir,
#   tam yolun üstünde olmaları gerekmez.
ROUTES = [
    ("Sahil yolu: Sirkeci - Yeşilköy", [
        (41.0150, 28.9800),  # Sirkeci
        (41.0040, 28.9800),  # Ahırkapı
        (41.0025, 28.9650),  # Kumkapı
        (41.0030, 28.9500),  # Yenikapı
        (40.9990, 28.9350),  # Samatya
        (40.9930, 28.9230),  # Yedikule
        (40.9860, 28.9050),  # Zeytinburnu
        (40.9750, 28.8750),  # Bakırköy
        (40.9620, 28.8300),  # Yeşilköy
    ], []),
    # OpenStreetMap'te Bağdat Caddesi'nin ortası Bostancı'dan Kadıköy'e tek yön çizildiği için
    # rotayı bu yönde oluşturuyoruz (araç rotanın sonunda zaten geri dönüyor).
    ("Bağdat Caddesi: Bostancı - Kadıköy", [
        (40.9530, 29.0960),  # Bostancı
        (40.9610, 29.0890),  # Suadiye
        (40.9690, 29.0780),  # Erenköy
        (40.9750, 29.0640),  # Göztepe
        (40.9800, 29.0480),  # Kızıltoprak
        (40.9905, 29.0250),  # Kadıköy
    ], [
        # Bağdat Caddesi çift şeritli; karşı şeride düşen ara nokta döngü yaratıyor.
        # Bu ikisi en kısa (döngüsüz) güzergâhı veriyor.
        (40.96351, 29.07481),  # Bağdat Caddesi, Caddebostan
        (40.97404, 29.05495),  # Bağdat Caddesi, Göztepe
    ]),
    ("E-5: Topkapı - Avcılar", [
        (41.0180, 28.9220),  # Topkapı
        (41.0070, 28.8950),  # Merter
        (40.9960, 28.8370),  # Yenibosna
        (40.9930, 28.7800),  # Küçükçekmece
        (40.9800, 28.7200),  # Avcılar
    ], []),
    ("Boğaz (Avrupa): Beşiktaş - Sarıyer", [
        (41.0420, 29.0060),  # Beşiktaş
        (41.0480, 29.0270),  # Ortaköy
        (41.0680, 29.0430),  # Arnavutköy
        (41.0770, 29.0430),  # Bebek
        (41.0850, 29.0560),  # Rumelihisarı
        (41.1050, 29.0560),  # Emirgan
        (41.1150, 29.0580),  # İstinye
        (41.1240, 29.0700),  # Yeniköy
        (41.1380, 29.0580),  # Tarabya
        (41.1660, 29.0500),  # Sarıyer
    ], [
        (41.0600, 29.0370),    # Arnavutköy Kuruçeşme Caddesi
        (41.07696, 29.04315),  # Cevdetpaşa Caddesi, Bebek
        (41.08399, 29.05685),  # Yahya Kemal Caddesi, Rumelihisarı
        (41.08984, 29.05656),  # Baltalimanı Hisar Caddesi
        (41.1060, 29.0570),    # Sakıp Sabancı Caddesi, Emirgan
        (41.1230, 29.0710),    # Yeniköy Tarabya Caddesi, Yeniköy
        (41.13719, 29.05743),  # Yeniköy Tarabya Caddesi, Tarabya
    ]),
    ("Boğaz (Anadolu): Üsküdar - Beykoz", [
        (41.0260, 29.0150),  # Üsküdar
        (41.0360, 29.0300),  # Kuzguncuk
        (41.0450, 29.0440),  # Beylerbeyi
        (41.0510, 29.0530),  # Çengelköy
        (41.0750, 29.0600),  # Kandilli
        (41.0830, 29.0670),  # Anadoluhisarı
        (41.0990, 29.0660),  # Kanlıca
        (41.1060, 29.0790),  # Çubuklu
        (41.1170, 29.0930),  # Paşabahçe
        (41.1330, 29.0970),  # Beykoz
    ], []),
]


def fetch_route(waypoints):
    """Noktalardan geçen araba güzergâhını döner: noktalar [(enlem, boylam), ...], her noktanın
    OpenStreetMap düğüm (node) numarası ve km."""
    # OSRM koordinatları "boylam,enlem" sırasıyla ister (bizim kullandığımız sıranın tersi)
    coordinates = ";".join(f"{lon},{lat}" for lat, lon in waypoints)
    response = requests.get(
        f"{OSRM_URL}/{coordinates}",
        params={
            "overview": "full",
            "geometries": "geojson",
            "continue_straight": "true",  # ara noktalarda U dönüşü yapma
            "annotations": "nodes",       # her noktanın OSM düğüm numarası (hangi yolda olduğunu bulmak için)
        },
        headers={"User-Agent": USER_AGENT},
        timeout=30,
    )
    response.raise_for_status()
    data = response.json()
    if data["code"] != "Ok":
        raise RuntimeError(f"OSRM rota bulamadı: {data.get('message')}")

    route = data["routes"][0]
    points = [(round(lat, 6), round(lon, 6)) for lon, lat in route["geometry"]["coordinates"]]

    # Düğümler ara noktalarla bölünmüş parçalar (leg) halinde gelir. Her leg'de ardışık iki düğüm,
    # güzergâhta ardışık iki noktaya (bir yol parçasına) karşılık gelir.
    node_pairs = []
    for leg in route["legs"]:
        leg_nodes = leg["annotation"]["nodes"]
        node_pairs += zip(leg_nodes, leg_nodes[1:])
    if len(node_pairs) != len(points) - 1:
        raise RuntimeError(f"OSRM {len(points)} nokta ama {len(node_pairs)} düğüm çifti döndü, eşleştirilemedi")
    return points, node_pairs, route["distance"] / 1000


def fetch_ways(node_pairs):
    """Bu düğümlerden geçen yolları (way) Overpass'tan alır: [{"nodes": [...], "tags": {...}}, ...]"""
    nodes = {node for pair in node_pairs for node in pair}
    query = f"[out:json][timeout:90];node(id:{','.join(map(str, nodes))});way(bn)[highway];out body;"
    for attempt in range(3):
        response = requests.post(OVERPASS_URL, data={"data": query}, headers={"User-Agent": USER_AGENT}, timeout=120)
        if response.status_code not in (429, 502, 504):   # 429: çok sık istek, 50x: sunucu meşgul; bekleyip tekrar dene
            break
        time.sleep(10 * (attempt + 1))
    response.raise_for_status()
    return response.json()["elements"]


def parse_maxspeed(value):
    """maxspeed etiketini km/s'e çevirir ("50" -> 50, "TR:urban" -> 50); anlaşılmıyorsa None."""
    if value is None:
        return None
    if value.isdigit():
        return int(value)
    return ZONE_LIMITS.get(value)


def speed_limit(tags):
    """Yolun hız sınırı ve bu sınırın OSM'de yazılı olup olmadığı (yazılı değilse yol türüne göre varsayılan)."""
    for key in ("maxspeed", "maxspeed:forward", "maxspeed:backward"):
        if limit := parse_maxspeed(tags.get(key)):
            return limit, True
    return DEFAULT_LIMITS.get(tags.get("highway"), URBAN_LIMIT), False


def segment_roads(node_pairs, ways):
    """Ardışık iki nokta arasındaki her parçanın hangi yolda olduğunu bulur: [yolun etiketleri, ...]
    Bir yol (way) sıralı düğümlerden oluşur; parçanın iki düğümünü yan yana içeren yol, parçanın yoludur."""
    road_of_pair = {}
    for way in ways:
        for a, b in zip(way["nodes"], way["nodes"][1:]):
            road_of_pair[a, b] = road_of_pair[b, a] = way["tags"]

    roads = [road_of_pair.get(pair) for pair in node_pairs]
    # Yolu bulunamayan parça (rotanın başı/sonu yolun ortasına denk gelebilir) komşu parçanın yolunu alır:
    # önce soldan sağa (öncekinden), kalanlar sağdan sola (sonrakinden)
    for i in range(1, len(roads)):
        roads[i] = roads[i] or roads[i - 1]
    for i in range(len(roads) - 2, -1, -1):
        roads[i] = roads[i] or roads[i + 1]
    return [road or {} for road in roads]


def nearest_index(points, target):
    """Güzergâhta hedefe en yakın noktanın sırası (kısa mesafede düz hesap yeterli)."""
    lat, lon = target
    return min(range(len(points)), key=lambda i: (points[i][0] - lat) ** 2 + (points[i][1] - lon) ** 2)


def build_route(name, stops, via):
    """Rotayı (routes.json için) ve yol parçalarını (SQL için) döner."""
    points, node_pairs, km = fetch_route([stops[0], *via, stops[-1]])
    stop_indexes = sorted({nearest_index(points, stop) for stop in stops})
    roads = segment_roads(node_pairs, fetch_ways(node_pairs))
    limits = [speed_limit(tags) for tags in roads]
    tagged = sum(has_tag for _, has_tag in limits) * 100 // len(limits)
    print(f"  {name:<38} {len(stop_indexes):>2} durak  {len(points):>4} nokta  {km:5.1f} km"
          f"  sınırlar {sorted({limit for limit, _ in limits})}, %{tagged} OSM'de yazılı")

    segments = [
        {"road": tags.get("name"), "highway": tags.get("highway", "unknown"), "limit": limit,
         "start": start, "end": end}
        for tags, (limit, _), start, end in zip(roads, limits, points, points[1:])
    ]
    # limits[i]: points[i] ile points[i + 1] arasındaki yolun hız sınırı
    route = {"name": name, "points": points, "stops": stop_indexes, "limits": [limit for limit, _ in limits]}
    return route, segments


def sql_text(value):
    return "NULL" if value is None else "N'" + value.replace("'", "''") + "'"


def write_sql(segments):
    """Yol parçalarını RoadSegments tablosuna yazan SQL betiğini oluşturur."""
    rows = [
        f"({sql_text(s['road'])}, '{s['highway']}', {s['limit']}, "
        f"{s['start'][0]}, {s['start'][1]}, {s['end'][0]}, {s['end'][1]})"
        for s in segments
    ]
    # SQL Server tek INSERT ... VALUES'ta en fazla 1000 satır kabul eder
    inserts = "\n".join(
        "INSERT INTO dbo.RoadSegments (RoadName, HighwayType, SpeedLimitKmh, "
        "StartLatitude, StartLongitude, EndLatitude, EndLongitude) VALUES\n" + ",\n".join(rows[i:i + 1000]) + ";"
        for i in range(0, len(rows), 1000)
    )
    SQL_FILE.write_text(f"""/* =========================================================
   FiloTakip - Yol parçaları ve hız sınırları (RoadSegments)
   BU DOSYA OTOMATİK OLUŞTURULDU, elle değiştirme: simulator/build_routes.py yazar.
   Çalıştırma: sqlcmd -S localhost -E -C -i 07_road_segments.sql

   Simülatör rotalarındaki her yol parçası (iki nokta arası) ve OpenStreetMap'teki hız sınırı.
   API, gelen konuma en yakın parçanın sınırını hız alarmında kullanır.
   Tablodaki eski parçaları silip yenilerini ekler (birden fazla çalıştırmak zararsız).
   ========================================================= */

USE FiloTakip;
GO

SET NOCOUNT ON;
DELETE FROM dbo.RoadSegments;
{inserts}
GO

PRINT N'RoadSegments tablosuna {len(rows)} yol parçası yazıldı.';
GO
""", encoding="utf-8")


def main():
    print("Rotalar OSRM'den, hız sınırları Overpass'tan alınıyor...")
    routes, segments = [], []
    for name, stops, via in ROUTES:
        route, route_segments = build_route(name, stops, via)
        routes.append(route)
        segments += route_segments
        time.sleep(5)  # ücretsiz sunucular: çok sık istek atınca reddediyorlar

    # Her rota tek satırda: dosya küçük kalsın, Git'te hangi rotanın değiştiği görünsün
    lines = ",\n".join(json.dumps(route, ensure_ascii=False, separators=(",", ":")) for route in routes)
    OUTPUT_FILE.write_text(f"[\n{lines}\n]\n", encoding="utf-8")
    print(f"{OUTPUT_FILE.name} yazıldı.")
    write_sql(segments)
    print(f"{SQL_FILE.name} yazıldı ({len(segments)} yol parçası).")


if __name__ == "__main__":
    main()
