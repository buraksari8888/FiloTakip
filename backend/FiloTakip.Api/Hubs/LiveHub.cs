using FiloTakip.Api.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace FiloTakip.Api.Hubs;

// Canlı bildirim kanalı. Tarayıcılar buraya bağlanır (/hubs/live) ve bağlantı açık kalır;
// API yeni bir şey olunca bağlı herkese kendisi haber verir, tarayıcının tekrar tekrar sorması gerekmez.
// Tarayıcının çağırdığı bir metot yok: mesajlar sadece sunucudan tarayıcıya gidiyor.
public class LiveHub : Hub<ILiveClient>
{
}

// Tarayıcılara gönderilebilecek mesajlar. Arayüz olarak yazınca mesaj adları ve içerikleri derleyici tarafından
// kontrol edilir (yanlış yazılmış bir mesaj adı derlenmez).
public interface ILiveClient
{
    // Yeni konum kaydedildi: liste ve harita o aracı günceller
    Task LocationAdded(LocationDto location);

    // Alarmlar değişti (yeni alarm üretildi ya da okundu işaretlendi): rozet ve alarm listesi yeniden çekilir
    Task AlarmsChanged();
}
