"""Tek bir aracın hareketini taklit eder: rota üzerinde konum, hız ve yakıt.

Bu dosya API'yi ya da ekrana yazmayı bilmez; sadece aracın bir sonraki durumunu hesaplar.
"""

import math
import random
from datetime import datetime, timezone

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
