"""Günlük filo raporu: her aracın bir gündeki mesafesi, hareket süresi, hızları, hız alarmları ve yakıt tüketimi.

Kullanım:
    python daily_report.py                     # bugün (İstanbul saatiyle)
    python daily_report.py --date 2026-10-30   # belirli bir gün

Hesabın asıl kısmı veritabanında, dbo.usp_DailyFleetReport saklı yordamında (database/03_reports.sql):
veritabanı binlerce kaydı Python'a taşımadan kendi içinde özetler. Python günü hesaplar, yordamı çağırır
ve sonucu gösterir.
"""

import argparse
import sys
from datetime import date, datetime, time, timedelta, timezone
from zoneinfo import ZoneInfo

import pandas as pd
from sqlalchemy import text
from sqlalchemy.exc import DBAPIError

from db import get_engine

ISTANBUL = ZoneInfo("Europe/Istanbul")


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
    """3900 -> '1 sa 05 dk'"""
    minutes = round(seconds / 60)
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


def main():
    """Çıkış kodu: 0 = başarılı, 1 = veritabanı hatası."""
    parser = argparse.ArgumentParser(description="FiloTakip günlük filo raporu")
    parser.add_argument("--date", type=date.fromisoformat, default=datetime.now(ISTANBUL).date(),
                        help="rapor günü, YYYY-AA-GG biçiminde (varsayılan: bugün, İstanbul saatiyle)")
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
    return 0


if __name__ == "__main__":
    sys.exit(main())
