// Uygulamanın ayarları. `ng build` (yayın derlemesi) bu dosyayı kullanır.
// Uygulama bir sunucuya yüklendiğinde apiUrl gerçek API adresiyle değiştirilir.
export const environment = {
  apiUrl: 'http://localhost:5275/api',
  hubUrl: 'http://localhost:5275/hubs/live', // canlı bildirim kanalı (SignalR)
};
