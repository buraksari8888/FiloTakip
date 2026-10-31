"""FiloTakip araç simülatörü: araçlardaki GPS cihazlarını taklit eder.

Kullanım:
    python simulator.py                      # araç listesini göster
    python simulator.py --all                # bütün aktif araçları sür
    python simulator.py --vehicle 2          # sadece 2 numaralı aracı sür
    python simulator.py --all --interval 1 --steps 10
    python simulator.py --all --speedup 10   # zamanı 10 kat hızlandır (haritada hareket belirgin olsun)
    python simulator.py --all --api-url http://localhost:5299   # başka adresteki API'ye gönder

Dosyalar:
    simulator.py   komut satırı ve ana döngü (bu dosya)
    vehicle.py     aracın hareketi (konum, hız, yakıt)
    api_client.py  API ile konuşma (HTTP istekleri)
"""

import argparse
import json
import os
import time
from pathlib import Path

import requests

from api_client import FiloTakipApi, LocationRejectedError
from vehicle import VehicleSimulator

# API adresi: --api-url verilirse o, yoksa FILOTAKIP_API_URL ortam değişkeni, o da yoksa bilgisayardaki API
DEFAULT_API_URL = os.environ.get("FILOTAKIP_API_URL", "http://localhost:5275")

# Gerçek yollara oturtulmuş rotalar (build_routes.py ile oluşturulur)
ROUTES_FILE = Path(__file__).with_name("routes.json")


def load_routes():
    """routes.json'daki rotaları okur: [{"name", "points": [[enlem, boylam], ...], "stops": [...]}, ...]"""
    return json.loads(ROUTES_FILE.read_text(encoding="utf-8"))


def print_vehicles(api):
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
    print(f"API: {api.base_url}")
    if speedup != 1:
        print(f"Zaman {speedup:g} kat hızlı: her {interval:g} saniyede {interval * speedup:g} saniyelik yol gidilir")
    for sim in sims:
        print(f"{sim.plate:<12} yola çıktı (hız sınırı {sim.speed_limit} km/s)")
    print("Durdurmak için Ctrl+C\n")

    step = 0
    while steps is None or step < steps:
        for sim in sims:
            sim.step(interval * speedup)
            payload = sim.to_payload()
            try:
                location_id = api.send_location(payload)
            except LocationRejectedError as error:
                print(f"  {sim.plate:<12} API konumu reddetti: {error}")
                continue
            except requests.RequestException as error:
                # Bir konum gönderilemedi diye simülatör durmasın (gerçek cihazlar da denemeye devam eder)
                print(f"  {sim.plate:<12} konum gönderilemedi: {error.__class__.__name__}")
                continue

            warning = "  ⚠️ HIZ SINIRI AŞILDI" if payload["speedKmh"] > sim.speed_limit else ""
            print(f"  {sim.plate:<12} #{location_id}  {payload['latitude']:.5f}, {payload['longitude']:.5f}  "
                  f"{payload['speedKmh']:>5.1f} km/s  yakıt %{payload['fuelPercent']:.1f}{warning}")

        step += 1
        time.sleep(interval)


def main():
    parser = argparse.ArgumentParser(description="FiloTakip araç simülatörü")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--all", action="store_true", help="bütün aktif araçları sür")
    group.add_argument("--vehicle", type=int, help="sadece bu numaralı aracı sür")
    parser.add_argument("--interval", type=float, default=3, help="kaç saniyede bir konum gönderilsin (varsayılan 3)")
    parser.add_argument("--steps", type=int, help="kaç tur sürülsün (verilmezse Ctrl+C'ye kadar)")
    parser.add_argument("--speedup", type=float, default=1, help="zaman kaç kat hızlı aksın (varsayılan 1 = gerçek zaman)")
    parser.add_argument("--api-url", default=DEFAULT_API_URL,
                        help="API'nin adresi (varsayılan: %(default)s, FILOTAKIP_API_URL ortam değişkeniyle de verilebilir)")
    args = parser.parse_args()

    api = FiloTakipApi(args.api_url)
    try:
        if args.all:
            vehicles = [v for v in api.get_vehicles() if v["isActive"]]
        elif args.vehicle is not None:
            vehicle = api.get_vehicle(args.vehicle)
            if vehicle is None:
                print(f"{args.vehicle} numaralı araç bulunamadı.")
                return
            vehicles = [vehicle]
        else:
            print_vehicles(api)
            return

        # Her araca sırayla bir rota ver (5'ten fazla araç olursa rotalar tekrar kullanılır)
        routes = load_routes()
        sims = [VehicleSimulator(v, routes[i % len(routes)]) for i, v in enumerate(vehicles)]
        run(api, sims, args.interval, args.steps, args.speedup)
    except FileNotFoundError:
        print(f"{ROUTES_FILE.name} bulunamadı. Önce rotaları oluştur: python build_routes.py")
    except requests.ConnectionError:
        print(f"API'ye bağlanılamadı ({api.base_url}). API çalışıyor mu? (backend/FiloTakip.Api klasöründe: dotnet run)")
    except KeyboardInterrupt:
        print("\nSimülatör durduruldu.")


if __name__ == "__main__":
    main()
