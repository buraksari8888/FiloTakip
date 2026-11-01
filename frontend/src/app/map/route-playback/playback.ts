import { LocationUpdate } from '../../core/live-hub';
import { isBreak } from '../vehicle-map/trail';

// 1x hızda 1 saniyede kaç saniyelik yol oynatılır. Gerçek zamanlı oynatma (1 saniyede 1 saniye) saatlerce
// sürerdi; 1x = 1 dakikalık yol 1 saniyede. 1 saatlik yol 1x'te 1 dakika, 16x'te ~4 saniye sürer.
export const SECONDS_PER_SECOND_AT_1X = 60;
export const PLAYBACK_SPEEDS = [1, 4, 16] as const;
export type PlaybackSpeed = (typeof PLAYBACK_SPEEDS)[number];

// API'deki AlarmRules.StationarySpeedKmh ile aynı: bunun altında araç duruyor sayılır
const STATIONARY_SPEED_KMH = 3;

// Araç en az bu kadar durduysa durak sayılır (kırmızı ışık, trafik gibi kısa duruşlar sayılmaz)
export const MIN_STOP_SECONDS = 60;

export interface Stop {
  latitude: number;
  longitude: number;
  start: string; // durduğu an (UTC)
  end: string; // yeniden hareket ettiği an (ya da verinin bittiği/koptuğu an)
  seconds: number;
}

// Oynatılacak güzergâh: konumlar (eskiden yeniye) ve zamana göre konum hesapları.
// Zamanlar milisaniye (Date.parse), oynatma saati de öyle.
export class Track {
  readonly times: number[];
  // Kopmadan devam eden parçalar [ilk sıra, son sıra]: veri boşluğunda ya da büyük sıçramada çizgi kopar
  readonly parts: [number, number][] = [];

  constructor(readonly points: LocationUpdate[]) {
    this.times = points.map((p) => Date.parse(p.recordedAt));
    let start = 0;
    for (let i = 1; i < points.length; i++) {
      if (isBreak(points[i - 1], points[i])) {
        this.parts.push([start, i - 1]);
        start = i;
      }
    }
    this.parts.push([start, points.length - 1]);
  }

  get start(): number {
    return this.times[0];
  }

  get end(): number {
    return this.times[this.times.length - 1];
  }

  // `time` anındaki ya da ondan önceki son konumun sırası (ikili arama: on binlerce konumda da hızlı)
  indexAt(time: number): number {
    let low = 0;
    let high = this.times.length - 1;
    while (low < high) {
      const mid = Math.ceil((low + high) / 2);
      if (this.times[mid] <= time) {
        low = mid;
      } else {
        high = mid - 1;
      }
    }
    return low;
  }

  // Aracın `time` anındaki yeri: iki konum arasında, geçen süreye göre aradaki doğru üzerinde.
  // Kopuk yerde (veri boşluğu) ara hesap yapılmaz, araç son bilinen yerinde durur.
  positionAt(time: number): [number, number] {
    const i = this.indexAt(time);
    const a = this.points[i];
    const b = this.points[i + 1];
    if (!b || time <= this.times[i] || isBreak(a, b)) {
      return [a.latitude, a.longitude];
    }
    const ratio = (time - this.times[i]) / (this.times[i + 1] - this.times[i]);
    return [a.latitude + (b.latitude - a.latitude) * ratio, a.longitude + (b.longitude - a.longitude) * ratio];
  }

  // Oynatma saatini ilerletir (en fazla sona kadar). Veri boşluğuna (cihaz kapalıydı) denk gelirse boşluk
  // atlanır: yoksa saatlerce süren bir boşlukta ekranda uzun süre hiçbir şey olmazdı.
  advance(time: number, deltaMs: number): number {
    const next = Math.min(time + deltaMs, this.end);
    const i = this.indexAt(next);
    const b = this.points[i + 1];
    return b && isBreak(this.points[i], b) ? this.times[i + 1] : next;
  }

  // Haritada gidilmiş yol: baştan `index` numaralı konuma kadar, parçalar halinde
  latLngsUntil(index: number): [number, number][][] {
    return this.parts
      .filter(([start]) => start <= index)
      .map(([start, end]) =>
        this.points.slice(start, Math.min(end, index) + 1).map((p): [number, number] => [p.latitude, p.longitude]),
      );
  }
}

// Aracın en az `minSeconds` durduğu yerler. Duruş, hızın STATIONARY_SPEED_KMH altına indiği ilk konumda başlar,
// aracın yeniden hareket ettiği ilk konumda biter. Veri koptuysa duruş son bilinen konumda biter:
// cihaz kapalıyken ne olduğunu bilmiyoruz.
export function findStops(points: LocationUpdate[], minSeconds = MIN_STOP_SECONDS): Stop[] {
  const stops: Stop[] = [];
  let first: LocationUpdate | null = null;
  let last: LocationUpdate | null = null;

  const close = () => {
    if (first && last) {
      const seconds = (Date.parse(last.recordedAt) - Date.parse(first.recordedAt)) / 1000;
      if (seconds >= minSeconds) {
        stops.push({
          latitude: first.latitude,
          longitude: first.longitude,
          start: first.recordedAt,
          end: last.recordedAt,
          seconds,
        });
      }
    }
    first = last = null;
  };

  for (const p of points) {
    if (last && isBreak(last, p)) {
      close();
    }
    if (p.speedKmh < STATIONARY_SPEED_KMH) {
      first ??= p;
      last = p;
    } else if (last) {
      last = p; // duruş, aracın hareket ettiği ilk konumda biter
      close();
    }
  }
  close();
  return stops;
}

// Süre metni: "45 sn", "7 dk", "2 sa 5 dk"
export function formatDuration(seconds: number): string {
  if (seconds < 60) {
    return `${Math.round(seconds)} sn`;
  }
  const minutes = Math.round(seconds / 60);
  return minutes < 60 ? `${minutes} dk` : `${Math.floor(minutes / 60)} sa ${minutes % 60} dk`;
}
