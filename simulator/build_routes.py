"""Rotaları gerçek yollara oturtur.

Her rota için başlangıçtan bitişe araba güzergâhını OSRM'den (OpenStreetMap verisiyle çalışan
ücretsiz yol tarifi servisi) alır ve routes.json dosyasına yazar. Simülatör rotaları bu dosyadan okur.

Bir kez çalıştırmak yeterli, rotaları değiştirirsen tekrar çalıştır:
    python build_routes.py
"""

import json
import time
from pathlib import Path

import requests

OSRM_URL = "https://router.project-osrm.org/route/v1/driving"
USER_AGENT = "FiloTakip-ogrenci-projesi"
OUTPUT_FILE = Path(__file__).with_name("routes.json")

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
    """Noktalardan geçen araba güzergâhını [(enlem, boylam), ...] ve km olarak döner."""
    # OSRM koordinatları "boylam,enlem" sırasıyla ister (bizim kullandığımız sıranın tersi)
    coordinates = ";".join(f"{lon},{lat}" for lat, lon in waypoints)
    response = requests.get(
        f"{OSRM_URL}/{coordinates}",
        params={
            "overview": "full",
            "geometries": "geojson",
            "continue_straight": "true",  # ara noktalarda U dönüşü yapma
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
    return points, route["distance"] / 1000


def nearest_index(points, target):
    """Güzergâhta hedefe en yakın noktanın sırası (kısa mesafede düz hesap yeterli)."""
    lat, lon = target
    return min(range(len(points)), key=lambda i: (points[i][0] - lat) ** 2 + (points[i][1] - lon) ** 2)


def build_route(name, stops, via):
    points, km = fetch_route([stops[0], *via, stops[-1]])
    stop_indexes = sorted({nearest_index(points, stop) for stop in stops})
    print(f"  {name:<38} {len(stop_indexes):>2} durak  {len(points):>4} nokta  {km:5.1f} km")
    return {"name": name, "points": points, "stops": stop_indexes}


def main():
    print("Rotalar OSRM'den alınıyor...")
    routes = []
    for name, stops, via in ROUTES:
        routes.append(build_route(name, stops, via))
        time.sleep(1)  # ücretsiz demo sunucusu: saniyede en fazla 1 istek

    # Her rota tek satırda: dosya küçük kalsın, Git'te hangi rotanın değiştiği görünsün
    lines = ",\n".join(json.dumps(route, ensure_ascii=False, separators=(",", ":")) for route in routes)
    OUTPUT_FILE.write_text(f"[\n{lines}\n]\n", encoding="utf-8")
    print(f"{OUTPUT_FILE.name} yazıldı.")


if __name__ == "__main__":
    main()
