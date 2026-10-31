// API'deki AlarmDto'nun TypeScript karşılığı (GET /api/alarms)
export interface Alarm {
  alarmId: number;
  vehicleId: number;
  plate: string;
  alarmType: AlarmType;
  message: string;
  isRead: boolean;
  createdAt: string; // UTC
  latitude: number | null;
  longitude: number | null;
}

// Veritabanındaki CK_Alarms_Type kuralının izin verdiği değerler
export type AlarmType = 'SpeedLimit' | 'LowFuel' | 'Idle' | 'FuelDrain';

export const ALARM_TYPE_LABELS: Record<AlarmType, string> = {
  SpeedLimit: 'Hız',
  LowFuel: 'Düşük yakıt',
  Idle: 'Rölanti',
  FuelDrain: 'Yakıt kaybı',
};
