using System.ComponentModel.DataAnnotations;

namespace FiloTakip.Api.Dtos;

// API'nin döndürdüğü sürücü. VehiclePlate: sürücünün şu an atanmış olduğu aktif araç (yoksa null)
public record DriverDto(
    int DriverId,
    string FirstName,
    string LastName,
    string? Phone,
    string LicenseNumber,
    string? VehiclePlate);

// Yeni sürücü. Ehliyet numarası her sürücüde farklı olmalı (veritabanında UNIQUE)
public record CreateDriverDto(
    [Required, StringLength(50)] string FirstName,
    [Required, StringLength(50)] string LastName,
    [StringLength(20)] string? Phone,
    [Required, StringLength(20)] string LicenseNumber);

// Sürücünün adını düzeltme (yazım hatası gibi). Araç başka birine verildiyse ad değil sürücü değiştirilir.
public record UpdateDriverNameDto(
    [Required, StringLength(50)] string FirstName,
    [Required, StringLength(50)] string LastName);
