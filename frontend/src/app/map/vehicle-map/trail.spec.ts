import { LocationUpdate } from '../../core/live-hub';
import { recentTrail, trailSegments } from './trail';

const point = (recordedAt: string, latitude = 41): LocationUpdate => ({
  locationId: 1,
  vehicleId: 1,
  latitude,
  longitude: 29,
  speedKmh: 50,
  fuelPercent: 60,
  ignitionOn: true,
  recordedAt,
});

describe('recentTrail', () => {
  it('keeps only the last minutes, counted from the newest point', () => {
    const points = [
      point('2026-10-31T09:29:59Z'), // 30 dakika 1 saniye önce: atılır
      point('2026-10-31T09:30:00Z'), // tam 30 dakika önce: kalır
      point('2026-10-31T09:45:00Z'),
      point('2026-10-31T10:00:00Z'),
    ];

    expect(recentTrail(points, 30).map((p) => p.recordedAt)).toEqual([
      '2026-10-31T09:30:00Z',
      '2026-10-31T09:45:00Z',
      '2026-10-31T10:00:00Z',
    ]);
  });

  it('returns an empty trail as is', () => {
    expect(recentTrail([], 30)).toEqual([]);
  });
});

describe('trailSegments', () => {
  const times = (segment: { points: LocationUpdate[] }) => segment.points.map((p) => p.recordedAt.slice(11, 19));

  it('splits the trail into connected parts by age, oldest first and most faded', () => {
    // 30 dakikalık iz, 6 parça: her parça 5 dakika. Noktalar 2 dakika arayla (boşluk sayılmaz).
    const points = ['09:44', '09:46', '09:48', '09:50', '09:52', '09:54', '09:56', '09:58', '10:00'].map((t) =>
      point(`2026-10-31T${t}:00Z`),
    );
    const segments = trailSegments(points, 30);

    // Her parça bir sonrakinin ilk noktasında biter, çizgi kopmaz
    expect(segments.map(times)).toEqual([
      ['09:44:00', '09:46:00'], // 15-20 dakika önce
      ['09:46:00', '09:48:00', '09:50:00', '09:52:00'], // 10-15 dakika önce
      ['09:52:00', '09:54:00', '09:56:00'], // 5-10 dakika önce
      ['09:56:00', '09:58:00', '10:00:00'], // son 5 dakika
    ]);
    expect(segments.map((s) => s.fade)).toEqual([0.6, 0.4, 0.2, 0]);
  });

  it('breaks the line at a long gap or a jump instead of joining the two points', () => {
    const points = [
      point('2026-10-31T09:57:00Z'),
      point('2026-10-31T09:57:03Z'),
      point('2026-10-31T09:59:10Z'), // 127 saniye sonra: boşluk
      point('2026-10-31T09:59:13Z'),
      point('2026-10-31T09:59:16Z', 41.03), // 3,3 km kuzeye sıçrama
      point('2026-10-31T09:59:19Z', 41.03),
    ];

    expect(trailSegments(points, 30).map(times)).toEqual([
      ['09:57:00', '09:57:03'],
      ['09:59:10', '09:59:13'],
      ['09:59:16', '09:59:19'],
    ]);
  });

  it('draws nothing for less than two points', () => {
    expect(trailSegments([], 30)).toEqual([]);
    expect(trailSegments([point('2026-10-31T10:00:00Z')], 30)).toEqual([]);
  });
});
