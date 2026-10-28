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
