using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FiloTakip.Api.Errors;

// Hiçbir yerde yakalanmayan hatalar (veritabanı kapalı, koddaki bir hata...) en son buraya düşer.
// Hatayı bütün ayrıntısıyla loglar, kullanıcıya ise iç ayrıntıları göstermeden standart bir 500 cevabı döner.
public class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // Aynı iz numarası (traceId) hem bu logda hem de kullanıcıya dönen cevapta var:
        // kullanıcı numarayı söylerse hatanın ayrıntısını logda bulabiliriz
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        // İstemci cevabı beklemeden vazgeçti (sayfadan çıktı, aynı veriyi yeniden istedi). Veritabanı sorgusu yarıda
        // kesilince SqlClient bunu "önemli bir hata" diye bildiriyor; bu bir sunucu hatası değil, cevap da gidemez.
        if (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("İstek istemci tarafından iptal edildi: {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = 499;   // "istemci kapattı" (nginx'in kullandığı standart dışı kod)
            return true;
        }

        logger.LogError(exception, "Beklenmeyen hata: {Method} {Path} (traceId {TraceId})",
            context.Request.Method, context.Request.Path, traceId);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Sunucuda beklenmeyen bir hata oluştu."
            }
        });
    }
}
