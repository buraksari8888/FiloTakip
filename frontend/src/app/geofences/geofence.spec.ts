import { Geofence, geofenceAlarmText } from './geofence';

describe('geofenceAlarmText', () => {
  const zone = (alarmOnEnter: boolean, alarmOnExit: boolean): Geofence => ({
    geofenceId: 1,
    name: 'Depo',
    latitude: 41,
    longitude: 29,
    radiusMeters: 400,
    alarmOnEnter,
    alarmOnExit,
  });

  it('bölgenin hangi geçişte alarm verdiğini yazar', () => {
    expect(geofenceAlarmText(zone(true, true))).toBe('girişte ve çıkışta alarm');
    expect(geofenceAlarmText(zone(true, false))).toBe('girişte alarm');
    expect(geofenceAlarmText(zone(false, true))).toBe('çıkışta alarm');
  });
});
