import * as L from 'leaflet';

// Canlı harita ve geçmiş rota sayfasının ortak harita parçaları

const ISTANBUL: L.LatLngTuple = [41.03, 28.98];

// İstanbul'a bakan, OpenStreetMap altlıklı boş bir harita
export function createMap(element: HTMLElement): L.Map {
  const map = L.map(element).setView(ISTANBUL, 11);
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> katkıcıları',
  }).addTo(map);
  return map;
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
