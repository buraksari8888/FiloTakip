// API'deki VehicleTypes ile aynı değerler (veritabanındaki CK_Vehicles_VehicleType kuralı)
export type VehicleType = 'Otomobil' | 'HafifTicari' | 'Kamyon' | 'Otobus';

export const VEHICLE_TYPES: VehicleType[] = ['Otomobil', 'HafifTicari', 'Kamyon', 'Otobus'];

export const VEHICLE_TYPE_LABELS: Record<VehicleType, string> = {
  Otomobil: 'Otomobil',
  HafifTicari: 'Hafif ticari',
  Kamyon: 'Kamyon / Tır',
  Otobus: 'Otobüs',
};

// Yeni araç eklerken türe göre önerilen araç hız sınırı (şirketin aracı için koyduğu en yüksek hız).
// Formda değiştirilebilir. Geçerli sınır, bununla yolun sınırından küçük olanıdır.
export const DEFAULT_SPEED_LIMITS: Record<VehicleType, number> = {
  Otomobil: 120,
  HafifTicari: 90,
  Kamyon: 80,
  Otobus: 90,
};

// API'ye gönderilen yeni araç (API'deki CreateVehicleDto)
export interface NewVehicle {
  plate: string;
  vehicleType: VehicleType;
  brand: string;
  model: string;
  modelYear: number;
  speedLimitKmh: number;
  driverId: number | null;
}
