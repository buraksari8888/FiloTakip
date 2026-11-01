import * as L from 'leaflet';

// Canlı harita ve geçmiş rota sayfasının ortak harita parçaları

const ISTANBUL: L.LatLngTuple = [41.03, 28.98];

// Haritaya çizilen çizgi ve halkaların renkleri. Leaflet bunları CSS değişkeni olarak alamıyor; değerler
// styles.scss'teki koyu temanın --accent ve --danger renkleri, iki temada (koyu ve açık harita) da okunuyor.
export const MAP_COLORS = {
  accent: '#4c8dff', // gidilmiş yol, iz
  route: '#7b8aa3', // geçmiş rotanın henüz oynatılmamış kısmı
  danger: '#f26464', // alarm halkası
};

// Araç işaretini yeni konumuna `durationMs` içinde kaydırır (konumlar 3 saniyede bir gelir; ışınlanmak yerine
// yol alıyormuş gibi görünsün). Önceki kayma bitmeden yeni konum gelirse o anki yerinden yenisine devam eder.
const animations = new WeakMap<L.Marker, number>();

export function animateMarker(marker: L.Marker, to: L.LatLngTuple, durationMs = 1000): void {
  const previous = animations.get(marker);
  if (previous !== undefined) {
    cancelAnimationFrame(previous);
  }
  const from = marker.getLatLng();
  const start = performance.now();
  const step = (now: number) => {
    const t = Math.min((now - start) / durationMs, 1);
    const eased = 1 - (1 - t) ** 3; // sona doğru yavaşlar
    marker.setLatLng([from.lat + (to[0] - from.lat) * eased, from.lng + (to[1] - from.lng) * eased]);
    if (t < 1) {
      animations.set(marker, requestAnimationFrame(step));
    } else {
      animations.delete(marker);
    }
  };
  animations.set(marker, requestAnimationFrame(step));
}

// Dünyanın tamamı. Enlem ±85: düz harita (Web Mercator) kutupları gösteremez, OpenStreetMap de bu sınırda biter.
const WORLD = L.latLngBounds([-85, -180], [85, 180]);

// İstanbul'a bakan, OpenStreetMap altlıklı boş bir harita. Dünya tek kopya: Leaflet varsayılan olarak
// uzaklaşınca dünyayı yan yana tekrarlar, kaydırınca araçlar başka bir kopyada kalıp kaybolurdu.
export function createMap(element: HTMLElement): L.Map {
  const map = L.map(element, {
    maxBounds: WORLD, // dünyanın dışına kaydırılamaz
    maxBoundsViscosity: 1, // sınırda yumuşakça esnemek yerine tam durur
  }).setView(ISTANBUL, 11);
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    noWrap: true, // dünyayı yan yana tekrarlama
    bounds: WORLD,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> katkıcıları',
  }).addTo(map);

  // En fazla, dünya harita kutusunu tam dolduracak kadar uzaklaşılabilsin (kenarlarda boşluk kalmasın).
  // Bu, kutunun boyutuna bağlı; pencere boyutu değişince yeniden hesaplanır.
  const fitWorld = () => map.setMinZoom(map.getBoundsZoom(WORLD, true));
  fitWorld();
  map.on('resize', fitWorld);
  return map;
}

// Sayfadan çıkarken haritayı kaldırır. Leaflet 1.9'da yakınlaştırma animasyonu sürerken harita kaldırılırsa,
// animasyonun 250 ms sonra çalışan bitiş fonksiyonu kaldırılmış haritada hata veriyor
// ("Cannot read properties of undefined (reading '_leaflet_pos')"; menüde hızlı gezinince oluyordu).
// Bitiş fonksiyonu "animasyon sürüyor mu" bayrağına bakıp çıkıyor: kaldırmadan önce bayrağı indiriyoruz.
export function removeMap(map: L.Map): void {
  (map as unknown as { _animatingZoom: boolean })._animatingZoom = false;
  map.remove();
}

// Haritadaki araç işareti: renkli nokta + yanında plaka
export function markerIcon(plate: string): L.DivIcon {
  return L.divIcon({
    className: 'vehicle-marker',
    html: `<span class="label">${escapeHtml(plate)}</span>`,
    iconSize: [14, 14],
    popupAnchor: [0, -8],
  });
}

// Adresteki ?arac=3 / ?alarm=46 değerini sayıya çevirir; yoksa ya da geçersizse (ör. ?arac=abc) yok sayar
export function toId(value: string | undefined): number | undefined {
  const id = Number(value);
  return Number.isInteger(id) && id > 0 ? id : undefined;
}

// Veritabanından gelen metni HTML'e koymadan önce zararsız hale getir
// (ör. sürücü adına "<script>" yazılmışsa çalışmasın, düz yazı olarak görünsün)
export function escapeHtml(text: string): string {
  const replacements: Record<string, string> = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
  return text.replace(/[&<>"']/g, (ch) => replacements[ch]);
}
