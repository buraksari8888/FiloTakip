using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Data;

// Veritabanı hatalarını tanıma
public static class DbErrors
{
    // UNIQUE kuralı ihlal edildi mi? (SQL Server hata numaraları: 2627 UNIQUE kısıtı, 2601 UNIQUE index.)
    // Önceden "var mı?" diye bakmak yetmez: aynı anda gelen iki istek ikisi de "yok" görüp ikisi de ekleyebilir.
    // Kuralı veritabanı uygular; ikinci istek bu hatayı alır, biz de 500 yerine anlaşılır bir cevap döneriz.
    // indexName: sadece bu kural/index için (mesajında adı geçer)
    public static bool IsUniqueViolation(DbUpdateException exception, string indexName) =>
        exception.InnerException is SqlException { Number: 2627 or 2601 } sql && sql.Message.Contains(indexName);
}
