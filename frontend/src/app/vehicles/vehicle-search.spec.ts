import { VehicleLastLocation } from './vehicle-last-location';
import { matchesSearch, searchKey } from './vehicle-search';

const vehicle: VehicleLastLocation = {
  vehicleId: 1,
  plate: '34 ABC 123',
  vehicleType: 'HafifTicari',
  brand: 'Ford',
  model: 'Transit',
  speedLimitKmh: 90,
  driverId: 1,
  driverName: 'Ahmet Yılmaz',
  latitude: null,
  longitude: null,
  speedKmh: null,
  fuelPercent: null,
  ignitionOn: null,
  recordedAt: null,
  roadSpeedLimitKmh: null,
};

describe('searchKey', () => {
  it('ignores case, spaces and Turkish letters', () => {
    expect(searchKey('  İstanbul ŞÇĞ Ü ')).toBe('istanbulscgu');
    expect(searchKey('YILMAZ')).toBe(searchKey('yilmaz'));
  });
});

describe('matchesSearch', () => {
  it.each([
    ['', true], // boş arama hepsini gösterir
    ['34abc', true], // plaka, boşluksuz
    ['abc 123', true],
    ['ford', true],
    ['TRANSIT', true],
    ['yilmaz', true], // Türkçe harf yazmadan sürücü
    ['ahmet yılmaz', true],
    ['ford ahmet', true], // kelimeler farklı alanlarda
    ['hafif', true], // tür
    ['renault', false],
    ['ford ayşe', false], // her kelime uymalı
  ])('"%s" -> %s', (query, expected) => {
    expect(matchesSearch(vehicle, query)).toBe(expected);
  });

  it('works for vehicles without a driver', () => {
    expect(matchesSearch({ ...vehicle, driverName: null }, 'ahmet')).toBe(false);
  });
});
