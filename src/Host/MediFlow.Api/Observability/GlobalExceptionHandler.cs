using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MediFlow.Api.Observability;

/// <summary>
/// Yakalanmamış istisnaları güvenli bir HTTP cevabına çevirir.
/// </summary>
/// <remarks>
/// NEDEN GEREKLİ - BİR GÜVENLİK KONUSU:
/// Bu olmadan ASP.NET Core geliştirme modunda ham yığın izini (stack trace)
/// cevap gövdesine yazıyor. Doğrulama testlerimizde bunu gördük: SQL
/// bağlantısı koptuğunda cevap, sunucu dosya yollarını, kütüphane
/// sürümlerini ve iç sınıf adlarını içeriyordu.
///
/// Üretimde bu bilgi saldırgana harita verir: hangi ORM, hangi sürüm,
/// hangi dosya yapısı. Bilinen açıkları hedeflemek kolaylaşır.
///
/// KURAL: Detay LOGA gider, kullanıcıya GİTMEZ.
/// Kullanıcı sadece correlation id'yi alır; destek o kimlikle logdaki tam
/// hikâyeyi bulur.
///
/// IExceptionHandler, ASP.NET Core 8 ile gelen arayüz. Eskiden bu iş
/// try/catch'li bir middleware ile yapılıyordu; artık birinci sınıf bir
/// soyutlama ve birden fazla işleyici zincirlenebiliyor.
/// </remarks>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        string correlationId =
            httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? "unknown";

        // İstisnanın TAMAMI loga gidiyor - yığın izi dahil.
        ExceptionHandlerMessages.Unhandled(
            logger,
            exception,
            httpContext.Request.Method,
            httpContext.Request.Path,
            correlationId);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred",
            Detail = "The request could not be completed. Quote the correlation id when reporting this.",
        };

        problem.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        // true = "bu istisnayı ben ele aldım, başka işleyiciye geçme".
        return true;
    }
}

internal static partial class ExceptionHandlerMessages
{
    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Error,
        Message = "Unhandled exception on {Method} {Path} (correlation {CorrelationId})")]
    public static partial void Unhandled(
        ILogger logger,
        Exception exception,
        string method,
        string path,
        string correlationId);
}
