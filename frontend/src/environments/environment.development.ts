// Geliştirme ayarları. `ng serve` bunu environment.ts yerine kullanır (angular.json > fileReplacements).
export const environment = {
  apiUrl: 'http://localhost:5275/api',
  hubUrl: 'http://localhost:5275/hubs/live', // canlı bildirim kanalı (SignalR)
};
