// API'deki GeofenceDto'nun TypeScript karşılığı (GET /api/geofences): haritada daire şeklinde bir bölge
export interface Geofence {
  geofenceId: number;
  name: string;
  latitude: number;
  longitude: number;
  radiusMeters: number;
  alarmOnEnter: boolean;
  alarmOnExit: boolean;
}

// Yeni bölge ya da düzenleme için API'ye gönderilen alanlar (API'deki SaveGeofenceDto)
export type GeofenceInput = Omit<Geofence, 'geofenceId'>;

// Yarıçap sınırları, veritabanındaki CK_Geofences_Radius kuralıyla aynı
export const MIN_RADIUS_METERS = 50;
export const MAX_RADIUS_METERS = 50_000;
export const DEFAULT_RADIUS_METERS = 300;

// Bölgenin hangi durumda alarm verdiği, kullanıcıya gösterilecek şekilde
export function geofenceAlarmText(zone: Pick<Geofence, 'alarmOnEnter' | 'alarmOnExit'>): string {
  if (zone.alarmOnEnter && zone.alarmOnExit) {
    return 'girişte ve çıkışta alarm';
  }
  return zone.alarmOnEnter ? 'girişte alarm' : 'çıkışta alarm';
}

// Haritada sürüklenerek bulunan yarıçap: 10 metreye yuvarlanır (1.237,4 m yerine 1.240 m) ve sınırların içinde kalır
export function roundRadius(meters: number): number {
  const rounded = Math.round(meters / 10) * 10;
  return Math.min(MAX_RADIUS_METERS, Math.max(MIN_RADIUS_METERS, rounded));
}

// Koordinat veritabanında 6 ondalık (~10 cm); haritanın verdiği 41.01234567891 gibi sayılar kısaltılır
export function roundCoordinate(value: number): number {
  return Math.round(value * 1e6) / 1e6;
}
