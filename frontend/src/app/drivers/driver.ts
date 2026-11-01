// API'deki DriverDto'nun TypeScript karşılığı (GET /api/drivers)
export interface Driver {
  driverId: number;
  firstName: string;
  lastName: string;
  phone: string | null;
  licenseNumber: string;
  vehiclePlate: string | null; // şu an kullandığı aktif araç
}

// API'ye gönderilen yeni sürücü (API'deki CreateDriverDto)
export interface NewDriver {
  firstName: string;
  lastName: string;
  phone: string | null;
  licenseNumber: string;
}

// Seçim listelerinde: "Ahmet Yılmaz (34 ABC 123)"
export function driverLabel(d: Driver): string {
  const name = `${d.firstName} ${d.lastName}`;
  return d.vehiclePlate ? `${name} (${d.vehiclePlate})` : name;
}
