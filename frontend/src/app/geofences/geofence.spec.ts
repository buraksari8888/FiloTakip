import { Geofence, geofenceAlarmText, roundCoordinate, roundRadius } from './geofence';

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

describe('roundRadius', () => {
  it('10 metreye yuvarlar', () => {
    expect(roundRadius(1237.4)).toBe(1240);
    expect(roundRadius(304)).toBe(300);
  });

  it('50 m ile 50 km arasında tutar', () => {
    expect(roundRadius(12)).toBe(50);
    expect(roundRadius(80_000)).toBe(50_000);
  });
});

describe('roundCoordinate', () => {
  it('6 ondalığa kısaltır (veritabanındaki gibi)', () => {
    expect(roundCoordinate(41.01234567891)).toBe(41.012346);
    expect(roundCoordinate(-28.9)).toBe(-28.9);
  });
});
