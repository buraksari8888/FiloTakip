import { LocationUpdate } from '../../core/live-hub';
import { findStops, formatDuration, Track } from './playback';

// 10:00:00'dan itibaren `seconds` saniye sonraki konum
const point = (seconds: number, speedKmh = 50, latitude = 41): LocationUpdate => ({
  locationId: seconds,
  vehicleId: 1,
  latitude,
  longitude: 29,
  speedKmh,
  fuelPercent: 60,
  ignitionOn: true,
  recordedAt: new Date(Date.UTC(2026, 9, 31, 10, 0, seconds)).toISOString(),
  roadSpeedLimitKmh: 50,
});
const at = (seconds: number) => Date.UTC(2026, 9, 31, 10, 0, seconds);

describe('Track', () => {
  // 0-6. saniyede kuzeye giden araç, sonra 10 dakikalık veri boşluğu (cihaz kapalı)
  const track = new Track([point(0, 50, 41), point(3, 50, 41.001), point(6, 50, 41.002), point(606, 50, 41.002)]);

  it('finds the last location at or before a time', () => {
    expect(track.indexAt(at(0))).toBe(0);
    expect(track.indexAt(at(4))).toBe(1);
    expect(track.indexAt(at(6))).toBe(2);
    expect(track.indexAt(at(9999))).toBe(3);
  });

  it('interpolates the position between two locations', () => {
    const [latitude] = track.positionAt(at(0) + 1500);
    expect(latitude).toBeCloseTo(41.0005, 6);
  });

  it('does not interpolate across a data gap', () => {
    expect(track.positionAt(at(300))).toEqual([41.002, 29]);
  });

  it('skips data gaps while playing and stops at the end', () => {
    expect(track.advance(at(0), 2000)).toBe(at(2));
    expect(track.advance(at(5), 2000)).toBe(at(606)); // boşluğa düştü: boşluğun sonuna atladı
    expect(track.advance(at(600), 60_000)).toBe(at(606));
  });

  it('splits the drawn route at data gaps', () => {
    expect(track.parts).toEqual([
      [0, 2],
      [3, 3],
    ]);
    expect(track.latLngsUntil(1)).toEqual([
      [
        [41, 29],
        [41.001, 29],
      ],
    ]);
  });
});

describe('findStops', () => {
  it('finds a stop from the first stationary location until the vehicle moves again', () => {
    const points = [point(0), point(3, 0), point(33, 1), point(63, 0), point(66, 40)];
    const stops = findStops(points);
    expect(stops).toHaveLength(1);
    expect(stops[0]).toMatchObject({ start: point(3).recordedAt, end: point(66).recordedAt, seconds: 63 });
  });

  it('ignores short stops like traffic lights', () => {
    expect(findStops([point(0), point(3, 0), point(12, 0), point(15, 50)])).toEqual([]);
  });

  it('ends a stop where the data breaks off', () => {
    const points = [point(0, 0), point(90, 0), point(3600, 0)]; // 90. saniyeden sonra ~1 saat veri yok
    const stops = findStops(points);
    expect(stops).toHaveLength(1);
    expect(stops[0].seconds).toBe(90);
  });
});

describe('formatDuration', () => {
  it('formats seconds, minutes and hours', () => {
    expect(formatDuration(45)).toBe('45 sn');
    expect(formatDuration(420)).toBe('7 dk');
    expect(formatDuration(7500)).toBe('2 sa 5 dk');
  });
});
