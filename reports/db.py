"""FiloTakip veritabanı bağlantısı.

Bağlantıyı test etmek için:
    python db.py
"""

from sqlalchemy import URL, create_engine, text

# API'deki connection string'in aynısı, sadece Python (ODBC) yazımıyla:
# Windows hesabıyla giriş, sunucu sertifikasına güven
DATABASE_URL = URL.create(
    "mssql+pyodbc",
    host="localhost",
    database="FiloTakip",
    query={
        "driver": "ODBC Driver 18 for SQL Server",
        "Trusted_Connection": "yes",
        "TrustServerCertificate": "yes",
    },
)


def get_engine():
    """Veritabanı bağlantı havuzunu (engine) oluşturur."""
    return create_engine(DATABASE_URL)


if __name__ == "__main__":
    engine = get_engine()
    with engine.connect() as conn:
        version = conn.execute(text("SELECT @@VERSION")).scalar_one().splitlines()[0]
        counts = conn.execute(text("""
            SELECT (SELECT COUNT(*) FROM dbo.Vehicles)        AS Vehicles,
                   (SELECT COUNT(*) FROM dbo.LocationRecords) AS Locations,
                   (SELECT COUNT(*) FROM dbo.Alarms)          AS Alarms
        """)).one()

    print("Bağlantı başarılı ✅")
    print(f"  Sunucu: {version}")
    print(f"  {counts.Vehicles} araç, {counts.Locations} konum kaydı, {counts.Alarms} alarm")
