using MediFlow.BuildingBlocks.Common;
using Microsoft.AspNetCore.Mvc;

namespace MediFlow.Api;

/// <summary>
/// Domain hatalarını HTTP cevaplarına çevirir.
/// </summary>
/// <remarks>
/// Bu eşleme SADECE burada, API katmanında yaşıyor. Domain'in HTTP diye bir
/// şeyin varlığından haberi yok - "404" veya "409" kelimesi o katmanda hiç
/// geçmiyor.
///
/// Eşlemeyi hata KODUNA göre yapıyoruz. Faz 0'da Error tipini tasarlarken
/// "Code makine için, Message insan için" demiştik; işte o kararın karşılığı.
/// Mesaja bakarak eşleme yapsaydık, bir yazım hatasını düzeltmek HTTP
/// davranışını bozardı.
/// </remarks>
internal static class ErrorMapping
{
    public static IResult ToHttpProblem(this Error error)
    {
        int status = StatusCodeFor(error);

        // ProblemDetails, HTTP hata gövdeleri için bir STANDART (RFC 9457).
        // Kendi JSON formatımızı uydurmuyoruz: istemciler, tarayıcı
        // araçları ve API test araçları bu formatı zaten tanıyor.
        var problem = new ProblemDetails
        {
            Status = status,
            Title = TitleFor(status),
            Detail = error.Message,
        };

        // Makine tarafının dallanacağı asıl bilgi: kod.
        // React arayüzü "code === 'Patient.NotFound'" diye kontrol edecek,
        // Türkçe mesaj metnine bakmayacak.
        problem.Extensions["code"] = error.Code;

        return Results.Problem(problem);
    }

    private static int StatusCodeFor(Error error) => error.Code switch
    {
        // "Bir şey bulunamadı" -> 404
        var code when code.EndsWith(".NotFound", StringComparison.Ordinal) =>
            StatusCodes.Status404NotFound,

        // "Zaten var" -> 409 Conflict. 400 değil, çünkü istek BOZUK değil;
        // sistemin o anki durumuyla çelişiyor.
        var code when code.EndsWith("AlreadyExists", StringComparison.Ordinal) =>
            StatusCodes.Status409Conflict,

        // Geri kalan her şey geçersiz girdi -> 400
        _ => StatusCodes.Status400BadRequest,
    };

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Validation failed",
    };
}
