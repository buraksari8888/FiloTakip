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

// Bölgenin hangi durumda alarm verdiği, kullanıcıya gösterilecek şekilde
export function geofenceAlarmText(zone: Geofence): string {
  if (zone.alarmOnEnter && zone.alarmOnExit) {
    return 'girişte ve çıkışta alarm';
  }
  return zone.alarmOnEnter ? 'girişte alarm' : 'çıkışta alarm';
}
