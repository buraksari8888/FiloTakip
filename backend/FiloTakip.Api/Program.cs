using FiloTakip.Api.Data;
using FiloTakip.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Veritabanı bağlantısı: bağlantı bilgisi appsettings.json > ConnectionStrings > FiloTakip
builder.Services.AddDbContext<FiloTakipDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FiloTakip")));

// Servis katmanı (iş mantığı). Scoped: her HTTP isteği için yeni bir örnek, DbContext ile aynı ömür
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<LocationService>();
builder.Services.AddScoped<AlarmService>();

// CORS: tarayıcı, Angular'ın (localhost:4200) bu API'ye (localhost:5275) istek atmasına
// ancak API izin verirse izin verir
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Tarayıcıdan API'yi test etmek için: http://localhost:5275/swagger
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "FiloTakip API"));
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
