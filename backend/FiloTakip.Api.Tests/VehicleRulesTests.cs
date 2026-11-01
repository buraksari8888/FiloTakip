using FiloTakip.Api.Services;

namespace FiloTakip.Api.Tests;

// Plaka ve metin düzeltme kuralları
public class VehicleRulesTests
{
    [Theory]
    [InlineData("34 ABC 123", "34 ABC 123")]
    [InlineData("34abc123", "34 ABC 123")]      // boşluksuz, küçük harf
    [InlineData("  06 a 1234 ", "06 A 1234")]   // tek harf, dört rakam
    [InlineData("81 xyz 99", "81 XYZ 99")]
    [InlineData("34 ibc 123", "34 IBC 123")]    // Türkçe ayarda bile "i" -> "I" (plakada "İ" yok)
    public void NormalizePlate_FormatsValidPlates(string input, string expected)
    {
        Assert.Equal(expected, VehicleRules.NormalizePlate(input));
    }

    [Theory]
    [InlineData("00 ABC 123")]    // il kodu 01-81
    [InlineData("82 ABC 123")]
    [InlineData("34 ABCD 123")]   // en fazla 3 harf
    [InlineData("34 ABC 1")]      // en az 2 rakam
    [InlineData("34 ŞÇ 123")]     // plakada Türkçe harf yok
    [InlineData("")]
    public void NormalizePlate_RejectsInvalidPlates(string input)
    {
        Assert.Null(VehicleRules.NormalizePlate(input));
    }

    [Fact]
    public void CleanText_TrimsAndCollapsesSpaces()
    {
        Assert.Equal("Mercedes Actros", VehicleRules.CleanText("  Mercedes   Actros "));
    }
}
