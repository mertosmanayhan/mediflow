using System.Diagnostics;
using MediatR;
using MediFlow.BuildingBlocks.Common;
using Microsoft.Extensions.Logging;

namespace MediFlow.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Her isteği loglar ve süresini ölçer.
/// </summary>
/// <remarks>
/// Tek bir sınıf, TÜM iş akışlarını kapsıyor. 40 handler'a 40 kez log
/// satırı yazmıyoruz - cross-cutting concern'ün çözümü tam olarak bu.
/// </remarks>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;

        // Stopwatch NESNESİ oluşturmadan süre ölçümü. Her istekte çalışan
        // bir kodda gereksiz tahsis yapmamak önemli.
        long startedAt = Stopwatch.GetTimestamp();

        LoggingMessages.Handling(logger, requestName);

        TResponse response = await next(cancellationToken);

        TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);

        // İŞ HATASI, SİSTEM HATASI DEĞİLDİR.
        // "Ad boş" bir arıza değil, beklenen bir sonuç -> Warning.
        // Error seviyesi gerçek arızalara (DB koptu, bug) saklanır; yoksa
        // alarm panosu iş hatalarıyla dolar ve kimse ona bakmaz.
        if (response is Result { IsFailure: true } failed)
        {
            LoggingMessages.Failed(logger, requestName, failed.Error.Code, elapsed.TotalMilliseconds);
        }
        else
        {
            LoggingMessages.Handled(logger, requestName, elapsed.TotalMilliseconds);
        }

        return response;
    }
}

/// <summary>
/// Log mesajlarının derleme zamanında üretilen halleri.
/// </summary>
/// <remarks>
/// NEDEN DOĞRUDAN logger.LogInformation(...) ÇAĞIRMIYORUZ?
///
/// O yöntem her çağrıda bir 'params object?[]' dizisi tahsis eder, değer
/// tiplerini (double gibi) box'lar ve argümanları log seviyesi KAPALI olsa
/// bile hesaplar. Tek bir endpoint'te önemsiz; her istekte çalışan bir
/// behavior'da ölçülebilir çöp.
///
/// [LoggerMessage] bir KAYNAK ÜRETİCİSİ (source generator): derleme
/// zamanında tahsis içermeyen bir gövde üretir ve seviye kapalıysa
/// argümanlara hiç dokunmaz. Analizörün CA1848 ve CA1873 uyarıları da
/// tam bunu söylüyordu.
///
/// 'partial' olmaları şart: gövdeyi üretici yazıyor.
///
/// EventId: her mesaja sabit bir numara. Mesaj metni değişse bile
/// log sorguları (ör. "EventId = 1002 olanları göster") çalışmaya devam
/// eder. Metne göre sorgulamak kırılgandır.
///
/// Şablondaki {RequestName} hâlâ YAPILANDIRILMIŞ ALAN - string
/// interpolasyonu değil. Blok 2'de Seq'te bu alanlara göre
/// sorgulayacağız.
/// </remarks>
internal static partial class LoggingMessages
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Handling {RequestName}")]
    public static partial void Handling(ILogger logger, string requestName);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "{RequestName} handled in {ElapsedMilliseconds} ms")]
    public static partial void Handled(ILogger logger, string requestName, double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "{RequestName} failed with {ErrorCode} in {ElapsedMilliseconds} ms")]
    public static partial void Failed(
        ILogger logger,
        string requestName,
        string errorCode,
        double elapsedMilliseconds);
}
