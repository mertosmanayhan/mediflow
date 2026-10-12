using Serilog.Context;

namespace MediFlow.Api.Observability;

/// <summary>
/// Her isteğe bir izleme kimliği (correlation id) bağlar.
/// </summary>
/// <remarks>
/// PROBLEM: Bir kullanıcı "işlem başarısız oldu" diyor. Loglarda binlerce
/// satır var. Hangileri O isteğe ait?
///
/// ÇÖZÜM: İsteğin başında bir kimlik üretip, o istek boyunca yazılan TÜM
/// log satırlarına otomatik eklemek. Sonra log sisteminde
/// "CorrelationId = 'abc123'" diye filtreleyip sadece o isteğin hikâyesini
/// görüyorsun.
///
/// NEDEN KRİTİK OLACAK: Faz 3'te 9 servis olacak. Bir randevu isteği
/// Gateway'den Scheduling'e, oradan RabbitMQ ile Notification'a geçecek.
/// Aynı kimlik hepsinde taşınırsa dağıtık bir akışı baştan sona
/// izleyebiliyorsun. Taşınmazsa hiçbir şeyi takip edemezsin.
/// Faz 5'te OpenTelemetry bunun endüstri standardı halini getirecek
/// (trace id); bu middleware aynı fikrin elle yazılmış hali.
///
/// Gelen istek kimliği GETİRİYORSA onu kullanıyoruz - böylece çağıran
/// sistemin kimliğiyle bizimki aynı kalıyor ve zincir kopmuyor.
/// </remarks>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    internal const string HeaderName = "X-Correlation-Id";
    internal const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        // İstisna işleyicisi LogContext'e erişemez (istisna yukarı çıkarken
        // aşağıdaki using bloğu kapanmış olur), o yüzden kimliği HttpContext
        // üzerinde de taşıyoruz.
        context.Items[ItemKey] = correlationId;

        // Cevaba da yazıyoruz: kullanıcı/istemci hata bildirirken bu kimliği
        // verebilsin. "Hata aldım" yerine "şu kimlikle hata aldım" demek
        // destek sürecini dakikalardan saniyelere indiriyor.
        context.Response.Headers[HeaderName] = correlationId;

        // LogContext.PushProperty: bu blok içinde yazılan HER log satırına
        // CorrelationId alanını otomatik ekler. Handler'lar, behavior'lar,
        // EF Core - hiçbiri bu kimliği bilmek zorunda değil.
        using (LogContext.PushProperty(ItemKey, correlationId))
        {
            await next(context);
        }
    }
}
