"""FiloTakip araç simülatörü: araçlardaki GPS cihazlarını taklit eder.

Kullanım:
    python simulator.py                      # araç listesini göster
    python simulator.py --all                # bütün aktif araçları sür
    python simulator.py --vehicle 2          # sadece 2 numaralı aracı sür
    python simulator.py --all --interval 1 --steps 10
    python simulator.py --all --speedup 10   # zamanı 10 kat hızlandır (haritada hareket belirgin olsun)
"""

import argparse
import json
import math
import random
import time
from datetime import datetime, timezone
from pathlib import Path

import requests

API_URL = "http://localhost:5275"

# Gerçek yollara oturtulmuş rotalar (build_routes.py ile oluşturulur)
ROUTES_FILE = Path(__file__).with_name("routes.json")

FUEL_PERCENT_PER_KM = 0.5   # gösterim için abartılı tüketim (gerçekte ~0.15)
STOP_CHANCE = 0.3           # durağa varınca durma ihtimali (%30)


def distance_km(lat1, lon1, lat2, lon2):
    """İki koordinat arasındaki mesafe (Haversine formülü, Dünya'yı küre kabul eder)."""
    earth_radius_km = 6371
    phi1, phi2 = math.radians(lat1), math.radians(lat2)
    d_phi = math.radians(lat2 - lat1)
    d_lambda = math.radians(lon2 - lon1)
    a = math.sin(d_phi / 2) ** 2 + math.cos(phi1) * math.cos(phi2) * math.sin(d_lambda / 2) ** 2
    return 2 * earth_radius_km * math.asin(math.sqrt(a))


class VehicleSimulator:
    """Tek bir aracın durumunu (konum, hız, yakıt) tutar ve her adımda günceller."""

    def __init__(self, vehicle, route):
        self.vehicle_id = vehicle["vehicleId"]
        self.plate = vehicle["plate"]
        self.speed_limit = vehicle["speedLimitKmh"]
        self.max_speed = self.speed_limit + 25  # ara sıra sınırı aşsın
        self.route = route["points"]            # yol üzerindeki noktalar
        self.stops = set(route["stops"])        # durak olan noktaların sırası

        # Rotanın rastgele bir noktasından, rastgele bir yönde yola çık
        # (her çalıştırmada araçlar farklı yerlerde olsun)
        self.target_index = random.randrange(len(self.route))
        self.lat, self.lon = self.route[self.target_index]
        self.direction = random.choice((1, -1))  # 1: ileri, -1: geri
        self._next_target()                      # gidilen nokta: bir sonraki (uçtaysak yön kendiliğinden döner)

        self.speed = 0.0
        self.fuel = random.uniform(40, 95)
        self.stop_ticks = 0                     # kaç adım daha duracak

    def step(self, seconds):
        """Aracı 'seconds' saniye kadar ilerletir."""
        if self.stop_ticks > 0:
            self.stop_ticks -= 1
            self.speed = 0.0
            return

        # Hız: biraz rastgele değişsin, 5 ile max_speed arasında kalsın
        self.speed = min(max(self.speed + random.uniform(-8, 12), 5.0), self.max_speed)

        # Mesafe = hız x süre  (km/s x saat)
        remaining_km = self.speed * seconds / 3600
        self.fuel -= remaining_km * FUEL_PERCENT_PER_KM
        if self.fuel < 5:
            self.fuel = 100.0                   # benzinlikte doldu

        while remaining_km > 0:
            target_lat, target_lon = self.route[self.target_index]
            to_target_km = distance_km(self.lat, self.lon, target_lat, target_lon)

            if to_target_km > remaining_km:
                # Noktaya varmadan yolun bir kısmını git (aradaki doğru üzerinde ilerle)
                ratio = remaining_km / to_target_km
                self.lat += (target_lat - self.lat) * ratio
                self.lon += (target_lon - self.lon) * ratio
                break

            # Noktaya vardık, sıradakine geç
            self.lat, self.lon = target_lat, target_lon
            remaining_km -= to_target_km
            arrived_index = self.target_index
            self._next_target()
            if arrived_index in self.stops and random.random() < STOP_CHANCE:
                self.stop_ticks = random.randint(1, 3)
                self.speed = 0.0
                break

    def _next_target(self):
        """Sıradaki noktayı seç; rotanın sonuna gelince geri dön."""
        next_index = self.target_index + self.direction
        if not 0 <= next_index < len(self.route):
            self.direction *= -1
            next_index = self.target_index + self.direction
        self.target_index = next_index

    def to_payload(self):
        """API'nin beklediği JSON (CreateLocationDto ile aynı alanlar)."""
        return {
            "vehicleId": self.vehicle_id,
            "latitude": round(self.lat, 6),
            "longitude": round(self.lon, 6),
            "speedKmh": round(self.speed, 1),
            "fuelPercent": round(self.fuel, 2),
            "ignitionOn": True,
            "recordedAt": datetime.now(timezone.utc).isoformat(),
        }


def load_routes():
    """routes.json'daki rotaları okur: [{"name", "points": [[enlem, boylam], ...], "stops": [...]}, ...]"""
    return json.loads(ROUTES_FILE.read_text(encoding="utf-8"))


def get_vehicles():
    """API'den araç listesini çeker."""
    response = requests.get(f"{API_URL}/api/vehicles", timeout=5)
    response.raise_for_status()  # 200 dışında bir kod gelirse hata fırlat
    return response.json()       # JSON metnini Python listesine çevir


def get_vehicle(vehicle_id):
    """Tek bir aracı çeker, yoksa None döner."""
    response = requests.get(f"{API_URL}/api/vehicles/{vehicle_id}", timeout=5)
    if response.status_code == 404:
        return None
    response.raise_for_status()
    return response.json()


def send_location(payload):
    """Konumu API'ye gönderir, oluşan kaydın numarasını döner."""
    response = requests.post(f"{API_URL}/api/locations", json=payload, timeout=5)
    if response.status_code == 400:
        print(f"  API veriyi reddetti: {response.json().get('errors')}")
        return None
    response.raise_for_status()
    return response.json()["locationId"]


def print_vehicles():
    vehicles = get_vehicles()
    print(f"{len(vehicles)} araç bulundu:")
    for v in vehicles:
        driver = v["driverName"] or "(sürücü yok)"
        car = f"{v['brand']} {v['model']}"
        print(f"  {v['vehicleId']:>2}  {v['plate']:<12} {car:<18} {driver}")
    print("\nBütün araçları sürmek için:  python simulator.py --all")
    print("Tek bir aracı sürmek için:   python simulator.py --vehicle <numara>")


def run(sims, interval, steps, speedup):
    """Her turda bütün araçları bir adım ilerletir ve konumlarını gönderir."""
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
                location_id = send_location(payload)
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
    args = parser.parse_args()

    try:
        if args.all:
            vehicles = [v for v in get_vehicles() if v["isActive"]]
        elif args.vehicle is not None:
            vehicle = get_vehicle(args.vehicle)
            if vehicle is None:
                print(f"{args.vehicle} numaralı araç bulunamadı.")
                return
            vehicles = [vehicle]
        else:
            print_vehicles()
            return

        # Her araca sırayla bir rota ver (5'ten fazla araç olursa rotalar tekrar kullanılır)
        routes = load_routes()
        sims = [VehicleSimulator(v, routes[i % len(routes)]) for i, v in enumerate(vehicles)]
        run(sims, args.interval, args.steps, args.speedup)
    except FileNotFoundError:
        print(f"{ROUTES_FILE.name} bulunamadı. Önce rotaları oluştur: python build_routes.py")
    except requests.ConnectionError:
        print("API'ye bağlanılamadı. API çalışıyor mu? (backend/FiloTakip.Api klasöründe: dotnet run)")
    except KeyboardInterrupt:
        print("\nSimülatör durduruldu.")


if __name__ == "__main__":
    main()
