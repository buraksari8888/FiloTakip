using FiloTakip.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Veritabanı bağlantısı: bağlantı bilgisi appsettings.json > ConnectionStrings > FiloTakip
builder.Services.AddDbContext<FiloTakipDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FiloTakip")));

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

app.UseAuthorization();

app.MapControllers();

app.Run();
