import { LocationUpdate } from '../core/live-hub';
import { applyLocation, VehicleLastLocation } from './vehicle-last-location';

const vehicle = (vehicleId: number, recordedAt: string | null): VehicleLastLocation => ({
  vehicleId,
  plate: `34 TEST ${vehicleId}`,
  brand: 'Ford',
  model: 'Transit',
  speedLimitKmh: 90,
  driverName: null,
  latitude: recordedAt ? 41 : null,
  longitude: recordedAt ? 29 : null,
  speedKmh: recordedAt ? 50 : null,
  fuelPercent: recordedAt ? 60 : null,
  ignitionOn: recordedAt ? true : null,
  recordedAt,
});

const update = (vehicleId: number, recordedAt: string): LocationUpdate => ({
  locationId: 1,
  vehicleId,
  latitude: 41.5,
  longitude: 29.5,
  speedKmh: 95,
  fuelPercent: 55,
  ignitionOn: true,
  recordedAt,
});

describe('applyLocation', () => {
  it('updates only the vehicle the location belongs to', () => {
    const list = [vehicle(1, '2026-10-31T10:00:00Z'), vehicle(2, '2026-10-31T10:00:00Z')];
    const result = applyLocation(list, update(2, '2026-10-31T10:00:03Z'));

    expect(result[0]).toBe(list[0]); // diğer araç aynı nesne, hiç değişmedi
    expect(result[1]).toMatchObject({ plate: '34 TEST 2', latitude: 41.5, speedKmh: 95, recordedAt: '2026-10-31T10:00:03Z' });
  });

  it('fills a vehicle that had no location yet', () => {
    const result = applyLocation([vehicle(1, null)], update(1, '2026-10-31T10:00:03Z'));
    expect(result[0]).toMatchObject({ latitude: 41.5, longitude: 29.5, recordedAt: '2026-10-31T10:00:03Z' });
  });

  it('ignores a location older than the one on screen', () => {
    const list = [vehicle(1, '2026-10-31T10:00:05Z')];
    const result = applyLocation(list, update(1, '2026-10-31T10:00:03.1234567Z'));
    expect(result[0]).toBe(list[0]);
  });

  it('ignores a vehicle that is not in the list', () => {
    const list = [vehicle(1, '2026-10-31T10:00:00Z')];
    expect(applyLocation(list, update(99, '2026-10-31T10:00:03Z'))).toEqual(list);
  });
});
