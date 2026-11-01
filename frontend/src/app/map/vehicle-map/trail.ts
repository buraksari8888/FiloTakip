import { LocationUpdate } from '../../core/live-hub';

// Haritada aracın arkasında kaç dakikalık iz çizilsin (gerçek filo sistemlerindeki gibi son 30 dakika)
export const TRAIL_MINUTES = 30;

// İz kaç parçaya bölünüp soluklaştırılsın (30 dakikalık izde 5'er dakikalık parçalar)
export const FADE_STEPS = 6;

// İki konum arasında bundan uzun boşluk ya da büyük sıçrama varsa çizgi birleştirilmez
// (cihaz kapalıydı ya da simülatör yeniden başlatıldı). Günlük rapordaki kuralların aynısı (03_reports.sql).
const MAX_GAP_SECONDS = 120;
const MAX_JUMP_KM = 2;

// İzin bir parçası. fade: 0 = en yeni (koyu), 1 = en eski (en soluk)
export interface TrailSegment {
  points: LocationUpdate[];
  fade: number;
}

// İzi parçalara böler:
// - Yaşına göre: eski parçalar soluk çizilir, böylece aracın hangi yönden geldiği ve gidip dönerken
//   üst üste binen yollar ayırt edilir. Bu parçalar bir sonrakinin ilk noktasında biter, çizgi kopuk görünmesin.
// - Boşluk ve sıçramalarda: orada çizgi gerçekten kopar, yoksa şehrin üstünden düz bir çizgi çekilirdi.
export function trailSegments(points: LocationUpdate[], minutes: number): TrailSegment[] {
  if (points.length < 2) {
    return [];
  }
  const newest = Date.parse(points[points.length - 1].recordedAt);
  const stepOf = (p: LocationUpdate) =>
    Math.min(FADE_STEPS - 1, Math.floor(((newest - Date.parse(p.recordedAt)) / (minutes * 60_000)) * FADE_STEPS));

  const segments: TrailSegment[] = [];
  let current: TrailSegment | null = null;
  let currentStep = -1;
  let previous: LocationUpdate | null = null;
  for (const p of points) {
    const step = stepOf(p);
    const broken = previous !== null && isBreak(previous, p);
    previous = p;
    if (current && step === currentStep && !broken) {
      current.points.push(p);
      continue;
    }
    if (!broken) {
      current?.points.push(p); // önceki parça bu noktaya kadar uzasın
    }
    current = { points: [p], fade: step / (FADE_STEPS - 1) };
    currentStep = step;
    segments.push(current);
  }
  return segments.filter((s) => s.points.length >= 2); // tek noktalık parça çizgi olarak çizilemez
}

// İzin son `minutes` dakikası. Süre en yeni noktaya göre hesaplanır, tarayıcının saatine göre değil:
// bilgisayarın saati sunucudan farklıysa da iz doğru uzunlukta kalır.
export function recentTrail(points: LocationUpdate[], minutes: number): LocationUpdate[] {
  if (points.length === 0) {
    return points;
  }
  const since = Date.parse(points[points.length - 1].recordedAt) - minutes * 60_000;
  return points.filter((p) => Date.parse(p.recordedAt) >= since);
}

// İki ardışık konum arasında çizgi kopmalı mı? (geçmiş rota oynatma da boşlukları bununla bulur)
export function isBreak(a: LocationUpdate, b: LocationUpdate): boolean {
  const gapSeconds = (Date.parse(b.recordedAt) - Date.parse(a.recordedAt)) / 1000;
  return gapSeconds > MAX_GAP_SECONDS || distanceKm(a, b) > MAX_JUMP_KM;
}

// İki konum arası kuş uçuşu mesafe (haversine formülü: dünyayı küre kabul eder)
function distanceKm(a: LocationUpdate, b: LocationUpdate): number {
  const toRad = (degrees: number) => (degrees * Math.PI) / 180;
  const dLat = toRad(b.latitude - a.latitude);
  const dLon = toRad(b.longitude - a.longitude);
  const h = Math.sin(dLat / 2) ** 2 + Math.cos(toRad(a.latitude)) * Math.cos(toRad(b.latitude)) * Math.sin(dLon / 2) ** 2;
  return 2 * 6371 * Math.asin(Math.sqrt(h));
}
