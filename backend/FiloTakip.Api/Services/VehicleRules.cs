using System.Text.RegularExpressions;

namespace FiloTakip.Api.Services;

// Araç bilgileriyle ilgili kurallar. Veritabanına dokunmaz, test etmesi kolay.
public static partial class VehicleRules
{
    // Türkiye plakası: il kodu (01-81) + 1-3 harf + 2-4 rakam. Plakada Ç, Ğ, İ, Ö, Ş, Ü kullanılmaz.
    [GeneratedRegex(@"^(0[1-9]|[1-7][0-9]|8[01])([A-Z]{1,3})([0-9]{2,4})$")]
    private static partial Regex PlatePattern();

    // Plakayı "34 ABC 123" biçimine getirir: boşluklar ve büyük/küçük harf önemli değil ("34abc123" de olur).
    // Geçersizse null. ToUpperInvariant: Türkçe ayarda "i" büyütülünce "İ" olur, plakada o harf yok.
    public static string? NormalizePlate(string plate)
    {
        var compact = string.Concat(plate.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        var match = PlatePattern().Match(compact);
        return match.Success ? $"{match.Groups[1]} {match.Groups[2]} {match.Groups[3]}" : null;
    }

    // Baştaki/sondaki boşlukları atar, aradaki birden fazla boşluğu teke indirir ("  Ford   Transit " -> "Ford Transit")
    public static string CleanText(string text) => string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
