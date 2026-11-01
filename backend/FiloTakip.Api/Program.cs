using FiloTakip.Api.Data;
using FiloTakip.Api.Errors;
using FiloTakip.Api.Hubs;
using FiloTakip.Api.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Loglar ekrana (konsol) yazılmaya devam eder, ayrıca günlük dosyalara da yazılır: logs/api-20261101.log
// (Serilog kütüphanesiyle). Hangi logun yazılacağına ikisinde de Logging:LogLevel ayarı karar verir.
// Klasör ve kaç dosya saklanacağı: appsettings > LogFiles. Göreli klasör API projesinin klasörüne göredir.
var logFiles = builder.Configuration.GetSection("LogFiles");
var logDirectory = Path.Combine(builder.Environment.ContentRootPath, logFiles["Directory"] ?? "logs");
var fileLogger = new LoggerConfiguration()
    .MinimumLevel.Verbose() // süzmeyi Serilog değil Logging:LogLevel yapsın (ekranla aynı loglar)
    .WriteTo.File(
        Path.Combine(logDirectory, "api-.log"), // tarih araya girer: api-20261101.log
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: logFiles.GetValue("RetainedFileCount", 14), // eskiler kendiliğinden silinir
        fileSizeLimitBytes: 50 * 1024 * 1024, // bir günün dosyası 50 MB'ı geçerse api-20261101_001.log'a devam
        rollOnFileSizeLimit: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3} {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
// AddSerilog() kısayolu değil: o, Logging:LogLevel ayarını yok sayıp her seviyeyi (Debug dahil) yazdırıyor
builder.Logging.AddProvider(new SerilogLoggerProvider(fileLogger));

// Add services to the container.

// Veritabanı bağlantısı: bağlantı bilgisi appsettings.json > ConnectionStrings > FiloTakip
builder.Services.AddDbContext<FiloTakipDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FiloTakip")));

// Servis katmanı (iş mantığı). Scoped: her HTTP isteği için yeni bir örnek, DbContext ile aynı ömür
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<LocationService>();
builder.Services.AddScoped<AlarmService>();
builder.Services.AddScoped<GeofenceService>();
builder.Services.AddScoped<DriverService>();
builder.Services.AddScoped<RoadSpeedLimitService>();

// Uygulamanın belleğinde önbellek (yol parçaları her konumda veritabanından okunmasın diye)
builder.Services.AddMemoryCache();

// CORS: tarayıcı, Angular'ın (localhost:4200) bu API'ye (localhost:5275) istek atmasına
// ancak API izin verirse izin verir. İzinli adresler: appsettings > Cors > AllowedOrigins
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// Hata cevapları tek tip olsun: her hata "ProblemDetails" biçiminde JSON döner
// ({ "title": ..., "status": ..., "traceId": ... }), istemci hepsini aynı şekilde okuyabilir
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// SignalR: tarayıcılarla açık kalan bir bağlantı (WebSocket) üzerinden anlık mesajlaşma
builder.Services.AddSignalR();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// API kapanırken log dosyası düzgünce kapatılsın
app.Lifetime.ApplicationStopped.Register(fileLogger.Dispose);

// Configure the HTTP request pipeline.

// Yakalanmamış hataları GlobalExceptionHandler'a gönderir. En başta: sonraki her adımın hatasını yakalasın
app.UseExceptionHandler();
// Gövdesi boş hata cevaplarına (ör. olmayan adres -> 404) ProblemDetails gövdesi ekler
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Tarayıcıdan API'yi test etmek için: http://localhost:5275/swagger
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "FiloTakip API"));
}
else
{
    // Yayında http isteklerini https'e yönlendir. Geliştirirken sadece http://localhost:5275 kullanıyoruz,
    // orada yönlendirecek https adresi olmadığı için her başlangıçta boş yere uyarı yazıyordu.
    app.UseHttpsRedirection();
}

app.UseCors();

app.UseAuthorization();

app.MapControllers();

// Canlı bildirim kanalı: tarayıcılar http://localhost:5275/hubs/live adresine bağlanır, bağlantı WebSocket'e geçer
app.MapHub<LiveHub>("/hubs/live");

app.Run();
