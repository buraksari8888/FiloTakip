"""FiloTakip araç simülatörü: araçlardaki GPS cihazlarını taklit eder.

Kullanım:
    python simulator.py                      # araç listesini göster
    python simulator.py --all                # bütün aktif araçları sür
    python simulator.py --vehicle 2          # sadece 2 numaralı aracı sür
    python simulator.py --all --interval 1 --steps 10
    python simulator.py --all --speedup 10   # zamanı 10 kat hızlandır (haritada hareket belirgin olsun)
    python simulator.py --all --api-url http://localhost:5299   # başka adresteki API'ye gönder
    python simulator.py --all --log-level ERROR   # sadece hataları yaz

Dosyalar:
    simulator.py   komut satırı ve ana döngü (bu dosya)
    vehicle.py     aracın hareketi (konum, hız, yakıt)
    api_client.py  API ile konuşma (HTTP istekleri)
"""

import argparse
import json
import logging
import os
import sys
import time
from pathlib import Path

import requests

from api_client import ApiError, FiloTakipApi
from vehicle import VehicleSimulator

log = logging.getLogger(__name__)

# API adresi: --api-url verilirse o, yoksa FILOTAKIP_API_URL ortam değişkeni, o da yoksa bilgisayardaki API
DEFAULT_API_URL = os.environ.get("FILOTAKIP_API_URL", "http://localhost:5275")

# Gerçek yollara oturtulmuş rotalar (build_routes.py ile oluşturulur)
ROUTES_FILE = Path(__file__).with_name("routes.json")


def load_routes():
    """routes.json'daki rotaları okur: [{"name", "points": [[enlem, boylam], ...], "stops": [...]}, ...]"""
    return json.loads(ROUTES_FILE.read_text(encoding="utf-8"))


def print_vehicles(api):
    # Burada print kalıyor: bu liste programın kullanıcıya verdiği cevap (çıktısı), çalışma günlüğü değil
    vehicles = api.get_vehicles()
    print(f"{len(vehicles)} araç bulundu:")
    for v in vehicles:
        driver = v["driverName"] or "(sürücü yok)"
        car = f"{v['brand']} {v['model']}"
        print(f"  {v['vehicleId']:>2}  {v['plate']:<12} {car:<18} {driver}")
    print("\nBütün araçları sürmek için:  python simulator.py --all")
    print("Tek bir aracı sürmek için:   python simulator.py --vehicle <numara>")


def run(api, sims, interval, steps, speedup):
    """Her turda bütün araçları bir adım ilerletir ve konumlarını gönderir."""
    # f-string yerine %s yer tutucuları: değerler ayrı verilir, satır ancak yazılacaksa birleştirilir
    # (.NET'teki {Plate} şablonlarıyla aynı fikir)
    log.info("API: %s", api.base_url)
    if speedup != 1:
        log.info("Zaman %g kat hızlı: her %g saniyede %g saniyelik yol gidilir", speedup, interval, interval * speedup)
    for sim in sims:
        log.info("%-12s yola çıktı (hız sınırı %s km/s)", sim.plate, sim.speed_limit)
    log.info("Durdurmak için Ctrl+C")

    step = 0
    while steps is None or step < steps:
        for sim in sims:
            sim.step(interval * speedup)
            payload = sim.to_payload()
            try:
                location_id = api.send_location(payload)
            except ApiError as error:
                # API cevap verdi ama konumu kaydetmedi (400: veri geçersiz, 500: sunucuda hata...)
                log.error("%-12s API konumu kaydetmedi: %s", sim.plate, error)
                continue
            except requests.RequestException as error:
                # Bir konum gönderilemedi diye simülatör durmasın (gerçek cihazlar da denemeye devam eder)
                log.error("%-12s konum gönderilemedi: %s", sim.plate, error.__class__.__name__)
                continue

            # Hız ihlali simülatör için bir hata değil, beklenen bir olay: seviyesi INFO, sadece işaretlenir
            warning = "  ⚠️ HIZ SINIRI AŞILDI" if payload["speedKmh"] > sim.speed_limit else ""
            log.info("%-12s #%s  %.5f, %.5f  %5.1f km/s  yakıt %%%.1f%s",   # %% = düz yüzde işareti
                     sim.plate, location_id, payload["latitude"], payload["longitude"],
                     payload["speedKmh"], payload["fuelPercent"], warning)

        step += 1
        time.sleep(interval)


def main():
    """Çıkış kodunu döner: 0 = başarılı, 1 = hata (onu çalıştıran betikler başarısız olduğunu anlayabilsin)."""
    parser = argparse.ArgumentParser(description="FiloTakip araç simülatörü")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--all", action="store_true", help="bütün aktif araçları sür")
    group.add_argument("--vehicle", type=int, help="sadece bu numaralı aracı sür")
    parser.add_argument("--interval", type=float, default=3, help="kaç saniyede bir konum gönderilsin (varsayılan 3)")
    parser.add_argument("--steps", type=int, help="kaç tur sürülsün (verilmezse Ctrl+C'ye kadar)")
    parser.add_argument("--speedup", type=float, default=1, help="zaman kaç kat hızlı aksın (varsayılan 1 = gerçek zaman)")
    parser.add_argument("--api-url", default=DEFAULT_API_URL,
                        help="API'nin adresi (varsayılan: %(default)s, FILOTAKIP_API_URL ortam değişkeniyle de verilebilir)")
    parser.add_argument("--log-level", default="INFO", type=str.upper, choices=["DEBUG", "INFO", "WARNING", "ERROR"],
                        help="bu seviye ve üstündeki loglar yazılır (varsayılan: %(default)s, DEBUG HTTP isteklerini de gösterir)")
    args = parser.parse_args()

    # Her log satırı: saat, seviye, mesaj. Örnek: 14:03:12 INFO    34 ABC 123   #8040  41.01595, ...
    # Python logları varsayılan olarak stderr'e yazar; bu programın asıl çıktısı loglar olduğu için stdout'a yazıyoruz
    # (yoksa "python simulator.py --all > kayit.txt" dosyaya hiçbir şey yazmazdı)
    logging.basicConfig(level=args.log_level, format="%(asctime)s %(levelname)-7s %(message)s", datefmt="%H:%M:%S",
                        stream=sys.stdout)

    api = FiloTakipApi(args.api_url)
    try:
        if args.all:
            vehicles = [v for v in api.get_vehicles() if v["isActive"]]
        elif args.vehicle is not None:
            vehicle = api.get_vehicle(args.vehicle)
            if vehicle is None:
                log.error("%s numaralı araç bulunamadı.", args.vehicle)
                return 1
            vehicles = [vehicle]
        else:
            print_vehicles(api)
            return 0

        # Her araca sırayla bir rota ver (5'ten fazla araç olursa rotalar tekrar kullanılır)
        routes = load_routes()
        sims = [VehicleSimulator(v, routes[i % len(routes)]) for i, v in enumerate(vehicles)]
        run(api, sims, args.interval, args.steps, args.speedup)
    except FileNotFoundError:
        log.error("%s bulunamadı. Önce rotaları oluştur: python build_routes.py", ROUTES_FILE.name)
        return 1
    except requests.ConnectionError:
        log.error("API'ye bağlanılamadı (%s). API çalışıyor mu? (backend/FiloTakip.Api klasöründe: dotnet run)",
                  api.base_url)
        return 1
    except requests.RequestException as error:
        # Bağlantı kuruldu ama istek tamamlanamadı (ör. API 5 saniyede cevap vermedi: Timeout)
        log.error("API'ye yapılan istek başarısız oldu: %s", error.__class__.__name__)
        return 1
    except ApiError as error:
        log.error("API hata döndürdü: %s", error)
        return 1
    except KeyboardInterrupt:
        log.info("Simülatör durduruldu.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
