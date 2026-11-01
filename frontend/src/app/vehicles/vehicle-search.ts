import { VehicleLastLocation } from './vehicle-last-location';
import { VEHICLE_TYPE_LABELS } from './vehicle-type';

const FOLD: Record<string, string> = { ç: 'c', ğ: 'g', ı: 'i', ö: 'o', ş: 's', ü: 'u' };

// Aramada büyük/küçük harf, Türkçe harfler ve boşluklar önemsiz:
// "yilmaz" "Yılmaz"ı, "34abc" "34 ABC 123"ü bulur
export function searchKey(text: string): string {
  return text
    .toLocaleLowerCase('tr')
    .replace(/\s+/g, '')
    .replace(/[çğıöşü]/g, (ch) => FOLD[ch]);
}

// Araç aramaya uyuyor mu? Plaka, marka, model, tür ve sürücü adında aranır. Aramadaki her kelime
// bunlardan birinde geçmeli: "ford ahmet" Ahmet'in kullandığı Ford'u bulur.
export function matchesSearch(v: VehicleLastLocation, query: string): boolean {
  const haystack = [v.plate, v.brand, v.model, v.driverName ?? '', VEHICLE_TYPE_LABELS[v.vehicleType]]
    .map(searchKey)
    .join('|');
  return query
    .split(/\s+/)
    .map(searchKey)
    .every((word) => haystack.includes(word));
}
