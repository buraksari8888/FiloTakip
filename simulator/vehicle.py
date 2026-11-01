"""Tek bir aracın hareketini taklit eder: rota üzerinde konum, hız ve yakıt.

Bu dosya API'yi ya da ekrana yazmayı bilmez; sadece aracın bir sonraki durumunu hesaplar.
"""

import math
import random
from datetime import datetime, timezone

FUEL_PERCENT_PER_KM = 0.5   # gösterim için abartılı tüketim (gerçekte ~0.15)
STOP_CHANCE = 0.3           # durağa varınca durma ihtimali (%30)
THEFT_CHANCE = 0.03         # durakta dururken depodan yakıt çekilme ihtimali (%3; gösterim için sık, gerçekte çok nadir)
IDLE_CHANCE = 0.03          # durakta motoru çalıştırıp uzun bekleme ihtimali (%3, rölanti senaryosu)
# Uzun bekleme kaç adım sürsün: varsayılan 3 saniyelik aralıkla 5,5-8 dakika (API 5 dakikada alarm üretir).
# Adım sayısıyla veriliyor çünkü API süreyi kayıtların saatine (gerçek saat) göre ölçer, --speedup bunu kısaltmaz.
IDLE_TICKS = (110, 160)

# Hız: şoför sınıra yakın gider (sınırın %85-100'ü). Ara sıra sınırı aşar (hız ihlali senaryosu).
CRUISE_FACTOR = (0.85, 1.0)
SPEEDING_CHANCE = 0.01      # her adımda hız yapmaya başlama ihtimali (%1: 3 saniyelik aralıkla ~5 dakikada bir)
SPEEDING_TICKS = (5, 15)    # hız yapma kaç adım sürsün
SPEEDING_OVER_KMH = (12, 30)  # sınırın kaç km/s üstüne çıksın (API 5 km/s'e kadar tolerans tanır)
ACCELERATION_KMH = 10       # bir adımda en fazla bu kadar hızlanır
BRAKING_KMH = 20            # bir adımda en fazla bu kadar yavaşlar
LOOKAHEAD_KM = 0.25         # şoför ileriyi görür: 250 metre içinde sınır düşüyorsa şimdiden yavaşlar


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
        self.vehicle_limit = vehicle["speedLimitKmh"]  # aracın kendi sınırı (ör. kamyon 80)
        self.route = route["points"]            # yol üzerindeki noktalar
        self.stops = set(route["stops"])        # durak olan noktaların sırası
        self.road_limits = route["limits"]      # road_limits[i]: route[i] ile route[i + 1] arasındaki yolun sınırı

        # Rotanın rastgele bir noktasından, rastgele bir yönde yola çık
        # (her çalıştırmada araçlar farklı yerlerde olsun)
        self.target_index = random.randrange(len(self.route))
        self.lat, self.lon = self.route[self.target_index]
        self.direction = random.choice((1, -1))  # 1: ileri, -1: geri
        self._next_target()                      # gidilen nokta: bir sonraki (uçtaysak yön kendiliğinden döner)

        self.speed = 0.0
        self.cruise_factor = random.uniform(*CRUISE_FACTOR)
        self.speeding_ticks = 0                 # hız ihlali senaryosu: kaç adım daha sınırın üstünde gidecek
        self.speeding_over = 0.0                # o sırada sınırın kaç km/s üstüne çıkıyor
        self.fuel = random.uniform(40, 95)
        self.stop_ticks = 0                     # kaç adım daha duracak
        self.theft_ticks = 0                    # hırsızlık senaryosu: kaç adım daha depodan yakıt çekilecek
        self.draining = False                   # bu adımda depodan yakıt çekildi mi (ekrana yazmak için)
        self.idle_started = False               # bu adımda uzun rölanti başladı mı (ekrana yazmak için)
        self.ignition_on = True

    def step(self, seconds):
        """Aracı 'seconds' saniye kadar ilerletir."""
        self.draining = False
        self.idle_started = False
        if self.stop_ticks > 0:
            self.stop_ticks -= 1
            self.speed = 0.0
            if self.theft_ticks > 0:
                # Her adımda %6-10. API'nin hırsızlık alarmı için durduğundan beri toplam düşüş önemli;
                # günlük rapor da tek adımda %5'ten büyük düşüşü tüketim saymaz.
                self.theft_ticks -= 1
                self.fuel = max(self.fuel - random.uniform(6, 10), 0.0)
                self.draining = True
            return

        self.ignition_on = True                 # yola çıkarken kontak açılır
        self._update_speed()

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
                scenario = random.random()
                if scenario < THEFT_CHANCE:
                    self._start_theft()
                elif scenario < THEFT_CHANCE + IDLE_CHANCE:
                    self._start_long_idle()
                break

    @property
    def speed_limit(self):
        """Şu an geçerli sınır: bulunduğu yolun sınırı ile aracın kendi sınırından küçük olanı (API'deki gibi)."""
        return min(self.road_limits[self._segment_index()], self.vehicle_limit)

    def _segment_index(self):
        """Araç hangi yol parçasında: ileri giderken hedef noktadan bir önceki, geri giderken hedef noktanın parçası."""
        return self.target_index - 1 if self.direction == 1 else self.target_index

    def _upcoming_limit(self):
        """Önümüzdeki LOOKAHEAD_KM içindeki en düşük sınır (şoför tabelayı görüp şimdiden yavaşlasın)."""
        limit = self.speed_limit
        lat, lon = self.lat, self.lon
        index, seen_km = self.target_index, 0.0
        while seen_km < LOOKAHEAD_KM:
            next_index = index + self.direction
            if not 0 <= next_index < len(self.route):
                break                           # rotanın sonu
            seen_km += distance_km(lat, lon, *self.route[index])
            lat, lon = self.route[index]
            limit = min(limit, self.road_limits[min(index, next_index)])
            index = next_index
        return min(limit, self.vehicle_limit)

    def _update_speed(self):
        """Hızı hedef hıza doğru değiştirir: hedef, sınıra göre (ara sıra sınırın üstü)."""
        if self.speeding_ticks > 0:
            self.speeding_ticks -= 1
        elif random.random() < SPEEDING_CHANCE:
            self.speeding_ticks = random.randint(*SPEEDING_TICKS)
            self.speeding_over = random.uniform(*SPEEDING_OVER_KMH)

        if self.speeding_ticks > 0:
            target = self.speed_limit + self.speeding_over
        else:
            target = self._upcoming_limit() * self.cruise_factor
        change = max(min(target - self.speed, ACCELERATION_KMH), -BRAKING_KMH)
        self.speed = max(self.speed + change + random.uniform(-2, 2), 5.0)

    def _start_theft(self):
        """Hırsızlık senaryosu: araç park halinde, kontak kapalı; 2-3 adım boyunca depodan yakıt çekilir.
        Duruş uzatılır ki çekme bittikten sonra da araç bir süre yerinde kalsın."""
        self.theft_ticks = random.randint(2, 3)
        self.stop_ticks = self.theft_ticks + 2
        self.ignition_on = False

    def _start_long_idle(self):
        """Rölanti senaryosu: şoför motoru kapatmadan uzun süre bekliyor (kontak açık, hız 0)."""
        self.stop_ticks = random.randint(*IDLE_TICKS)
        self.idle_started = True

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
            "ignitionOn": self.ignition_on,
            "recordedAt": datetime.now(timezone.utc).isoformat(),
        }
