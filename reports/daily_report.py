"""Günlük filo raporu: her aracın bir gündeki mesafesi, hareket süresi, hızları, hız alarmları ve yakıt tüketimi.

Kullanım:
    python daily_report.py                     # bugün (İstanbul saatiyle)
    python daily_report.py --date 2026-10-30   # belirli bir gün
    python daily_report.py --date 2026-10-30 --excel             # ayrıca filo-raporu-2026-10-30.xlsx dosyasına
    python daily_report.py --excel C:\\Raporlar\\bugun.xlsx        # ayrıca bu Excel dosyasına

Hesabın asıl kısmı veritabanında, dbo.usp_DailyFleetReport saklı yordamında (database/03_reports.sql):
veritabanı binlerce kaydı Python'a taşımadan kendi içinde özetler. Python günü hesaplar, yordamı çağırır
ve sonucu gösterir.
"""

import argparse
import sys
from datetime import date, datetime, time, timedelta, timezone
from zoneinfo import ZoneInfo

import pandas as pd
from openpyxl.styles import Font, PatternFill
from openpyxl.utils import get_column_letter
from sqlalchemy import text
from sqlalchemy.exc import DBAPIError

from db import get_engine

ISTANBUL = ZoneInfo("Europe/Istanbul")

# Excel sütunları: (başlık, rapordaki alan, Excel sayı biçimi, genişlik, altına toplam yazılsın mı).
# Sayı biçimi sadece görünümü değiştirir, hücrede sayının kendisi durur (Excel toplayabilsin, sıralayabilsin).
# Ondalık ayırıcıyı Excel kullanıcının diline göre kendisi gösterir (Türkçe Excel'de virgül).
EXCEL_COLUMNS = [
    ("Plaka", "Plate", None, 12, False),
    ("Sürücü", "Driver", None, 18, False),
    ("Kayıt", "Records", "0", 9, True),
    ("Mesafe (km)", "DistanceKm", "0.0", 13, True),
    ("Hareket (sa:dk)", "MovingSeconds", "[h]:mm", 16, True),   # [h]: 24 saati geçince başa dönmesin
    ("Ort. hız (km/s)", "AvgMovingSpeedKmh", "0.0", 16, False),
    ("Maks. hız (km/s)", "MaxSpeedKmh", "0.0", 17, False),
    ("Hız alarmı", "SpeedAlarms", "0", 12, True),
    ("Yakıt (%)", "FuelUsedPercent", "0.0", 11, True),
]
HEADER_FONT = Font(bold=True, color="FFFFFF")
HEADER_FILL = PatternFill("solid", fgColor="1F3A5F")   # arayüzdeki üst menünün rengi


def day_bounds_utc(day):
    """İstanbul saatiyle bir günün başı ve sonu (sonu hariç), UTC olarak: veritabanındaki saatler UTC."""
    start = datetime.combine(day, time.min, tzinfo=ISTANBUL)
    end = datetime.combine(day + timedelta(days=1), time.min, tzinfo=ISTANBUL)
    # Veritabanındaki sütun saat dilimi bilgisi tutmuyor (DATETIME2), o yüzden bilgiyi çevirdikten sonra atıyoruz
    return start.astimezone(timezone.utc).replace(tzinfo=None), end.astimezone(timezone.utc).replace(tzinfo=None)


def load_report(engine, day):
    """Raporu veritabanından çeker: her satır bir araç."""
    start_utc, end_utc = day_bounds_utc(day)
    # :start ve :end yer tutucu; değerler ayrı gönderilir, SQL metnine yapıştırılmaz (SQL injection olmaz)
    query = text("EXEC dbo.usp_DailyFleetReport @StartUtc = :start, @EndUtc = :end")
    with engine.connect() as conn:
        return pd.read_sql(query, conn, params={"start": start_utc, "end": end_utc})


def format_number(value, digits=1):
    """Türkçe ondalık: 12.34 -> '12,3'. Değer yoksa (araç o gün hiç hareket etmediyse) '—'."""
    if pd.isna(value):
        return "—"
    return f"{value:.{digits}f}".replace(".", ",")


def format_duration(seconds):
    """3900 -> '1 sa 05 dk'. Artan saniyeler atılır (Excel'in [h]:mm biçimi de böyle gösterir, ikisi aynı olsun)."""
    minutes = int(seconds // 60)
    return f"{minutes // 60} sa {minutes % 60:02d} dk"


def print_report(report, day):
    start_utc, end_utc = day_bounds_utc(day)
    print(f"Günlük filo raporu: {day:%d.%m.%Y} (İstanbul saatiyle)")
    print(f"Kapsanan aralık (UTC): {start_utc:%d.%m %H:%M} – {end_utc:%d.%m %H:%M}\n")

    table = pd.DataFrame({
        "Plaka": report["Plate"],
        "Sürücü": report["Driver"].fillna("—"),
        "Kayıt": report["Records"],
        "Mesafe (km)": report["DistanceKm"].map(format_number),
        "Hareket": report["MovingSeconds"].map(format_duration),
        "Ort. hız": report["AvgMovingSpeedKmh"].map(format_number),
        "Maks. hız": report["MaxSpeedKmh"].map(format_number),
        "Hız alarmı": report["SpeedAlarms"],
        "Yakıt (%)": report["FuelUsedPercent"].map(format_number),
    })
    print(table.to_string(index=False))

    print(f"\nToplam: {format_number(report['DistanceKm'].sum())} km, "
          f"{format_duration(report['MovingSeconds'].sum())} hareket, "
          f"{report['SpeedAlarms'].sum()} hız alarmı")
    print("Hızlar km/s; ortalama hız sadece araç hareket halindeyken. Yakıt: gün içinde harcanan depo yüzdesi.")


def save_excel(report, day, path):
    """Raporu biçimlendirilmiş bir Excel dosyasına yazar."""
    table = pd.DataFrame({title: report[field] for title, field, *_ in EXCEL_COLUMNS})
    # Excel'de süre "günün kesri" olarak tutulur: 1 = 24 saat, 0,5 = 12 saat
    table["Hareket (sa:dk)"] = report["MovingSeconds"] / 86400

    sheet_name = "Günlük rapor"
    header_row = 3                               # 1. satır başlık, 2. satır boş
    first_row, last_row = header_row + 1, header_row + len(table)
    total_row = last_row + 1

    with pd.ExcelWriter(path, engine="openpyxl") as writer:
        table.to_excel(writer, sheet_name=sheet_name, startrow=header_row - 1, index=False)
        sheet = writer.sheets[sheet_name]

        sheet["A1"] = f"Günlük filo raporu: {day:%d.%m.%Y} (İstanbul saatiyle)"
        sheet["A1"].font = Font(bold=True, size=14)
        sheet.cell(total_row, 1, "Toplam").font = Font(bold=True)

        for column, (_, _, number_format, width, has_total) in enumerate(EXCEL_COLUMNS, start=1):
            letter = get_column_letter(column)
            sheet.column_dimensions[letter].width = width
            sheet.cell(header_row, column).font = HEADER_FONT
            sheet.cell(header_row, column).fill = HEADER_FILL
            if number_format:
                for row in range(first_row, total_row + 1):
                    sheet.cell(row, column).number_format = number_format
            if has_total:
                # Formül: dosyada bir sayı değiştirilirse toplam da kendiliğinden güncellenir
                total = sheet.cell(total_row, column, f"=SUM({letter}{first_row}:{letter}{last_row})")
                total.font = Font(bold=True)

        sheet.freeze_panes = f"A{first_row}"     # aşağı kaydırınca başlıklar görünür kalsın
        sheet.auto_filter.ref = f"A{header_row}:{get_column_letter(len(EXCEL_COLUMNS))}{last_row}"   # filtre okları


def main():
    """Çıkış kodu: 0 = başarılı, 1 = veritabanı hatası ya da Excel dosyası yazılamadı."""
    parser = argparse.ArgumentParser(description="FiloTakip günlük filo raporu")
    parser.add_argument("--date", type=date.fromisoformat, default=datetime.now(ISTANBUL).date(),
                        help="rapor günü, YYYY-AA-GG biçiminde (varsayılan: bugün, İstanbul saatiyle)")
    parser.add_argument("--excel", nargs="?", const="", metavar="DOSYA",
                        help="raporu ayrıca Excel dosyasına da yaz (dosya adı verilmezse filo-raporu-<gün>.xlsx)")
    args = parser.parse_args()

    try:
        report = load_report(get_engine(), args.date)
    except (DBAPIError, pd.errors.DatabaseError) as error:
        # Bağlantı hatası SQLAlchemy'den (DBAPIError) gelir; sorgu hatasını ise pandas kendi türüne sarar.
        # Veritabanının asıl mesajı en içteki (pyodbc) hatada: ('42000', "[...] Could not find ...")
        driver_error = error.orig if isinstance(error, DBAPIError) else getattr(error.__cause__, "orig", error)
        message = str(driver_error.args[-1])
        print(f"Veritabanından rapor alınamadı: {message}", file=sys.stderr)
        if "usp_DailyFleetReport" in message:
            print("Rapor yordamı oluşturulmamış olabilir. database klasöründe çalıştır: "
                  "sqlcmd -S localhost -E -C -i 03_reports.sql", file=sys.stderr)
        return 1

    print_report(report, args.date)

    if args.excel is not None:
        path = args.excel or f"filo-raporu-{args.date}.xlsx"
        try:
            save_excel(report, args.date, path)
        except PermissionError:
            # Windows, Excel'de açık olan dosyanın üzerine yazdırmaz
            print(f"\n{path} yazılamadı. Dosya Excel'de açık olabilir, kapatıp tekrar dene.", file=sys.stderr)
            return 1
        print(f"\nExcel dosyası kaydedildi: {path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
