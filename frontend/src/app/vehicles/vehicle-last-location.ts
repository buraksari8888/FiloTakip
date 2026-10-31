import { LocationUpdate } from '../core/live-hub';

// API'deki VehicleLastLocationDto'nun TypeScript karşılığı (GET /api/vehicles/last-locations).
// Henüz hiç konum göndermemiş araçlarda konum alanları null gelir.
export interface VehicleLastLocation {
  vehicleId: number;
  plate: string;
  brand: string;
  model: string;
  speedLimitKmh: number;
  driverName: string | null;
  latitude: number | null;
  longitude: number | null;
  speedKmh: number | null;
  fuelPercent: number | null;
  ignitionOn: boolean | null;
  recordedAt: string | null; // UTC, ör. "2026-10-28T17:55:49Z"
}

const LOW_FUEL_PERCENT = 15;

export function isSpeeding(v: VehicleLastLocation): boolean {
  return v.speedKmh !== null && v.speedKmh > v.speedLimitKmh;
}

export function isLowFuel(v: VehicleLastLocation): boolean {
  return v.fuelPercent !== null && v.fuelPercent < LOW_FUEL_PERCENT;
}

// Canlı gelen konumu listedeki araca uygular ve yeni bir liste döner (signal değiştiğini böyle anlar).
// Araç listede yoksa (ör. pasif araç) ya da gelen konum eldekinden eskiyse o araç olduğu gibi kalır:
// gerçek cihazlar bağlantısız kaldıkları sürede biriken eski konumları sonradan gönderebilir.
export function applyLocation(list: VehicleLastLocation[], location: LocationUpdate): VehicleLastLocation[] {
  return list.map((v) =>
    v.vehicleId === location.vehicleId && !isOlder(location.recordedAt, v.recordedAt)
      ? {
          ...v,
          latitude: location.latitude,
          longitude: location.longitude,
          speedKmh: location.speedKmh,
          fuelPercent: location.fuelPercent,
          ignitionOn: location.ignitionOn,
          recordedAt: location.recordedAt,
        }
      : v,
  );
}

function isOlder(incoming: string, current: string | null): boolean {
  return current !== null && Date.parse(incoming) < Date.parse(current);
}
