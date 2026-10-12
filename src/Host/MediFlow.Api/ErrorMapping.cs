using MediFlow.BuildingBlocks.Common;
using Microsoft.AspNetCore.Mvc;

namespace MediFlow.Api;

/// <summary>
/// Domain ve uygulama hatalarını HTTP cevaplarına çevirir.
/// </summary>
/// <remarks>
/// Bu eşleme SADECE burada, API katmanında yaşıyor. Domain'de ve
/// Application'da "404" veya "409" kelimesi hiç geçmiyor.
///
/// Eşleme hata KODUNA göre yapılıyor, mesaja göre değil. Mesajdaki bir
/// yazım hatasını düzeltmek HTTP davranışını değiştirmemeli.
/// </remarks>
internal static class ErrorMapping
{
    public static IResult ToHttpProblem(this Error error)
    {
        // Doğrulama hatası özel: içinde BİRDEN FAZLA hata var ve
        // hepsini cevaba yazmak istiyoruz.
        if (error is ValidationError validation)
        {
            return ValidationProblem(validation);
        }

        int status = StatusCodeFor(error);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = TitleFor(status),
            Detail = error.Message,
        };

        problem.Extensions["code"] = error.Code;

        return Results.Problem(problem);
    }

    /// <summary>
    /// Doğrulama hatalarını tek cevapta döndürür.
    /// </summary>
    /// <remarks>
    /// Kullanıcıya formdaki TÜM eksikleri bir seferde söylüyoruz. Tek tek
    /// göstermek ("önce adı düzelt... şimdi soyadı") kötü bir deneyim ve
    /// gereksiz sunucu turu demek.
    ///
    /// 'errors' dizisindeki her eleman makine okunabilir bir 'code' taşıyor,
    /// böylece arayüz hatayı ilgili form alanının yanında gösterebilir.
    /// </remarks>
    private static IResult ValidationProblem(ValidationError validation)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = validation.Message,
        };

        problem.Extensions["code"] = validation.Code;
        problem.Extensions["errors"] = validation.Errors
            .Select(error => new { code = error.Code, message = error.Message })
            .ToArray();

        return Results.Problem(problem);
    }

    private static int StatusCodeFor(Error error) => error.Code switch
    {
        // KİMLİK DOĞRULAMA HATALARI -> 401, 400 DEĞİL.
        //
        // Ayrım önemli: 400 "isteğin kendisi bozuk" demek (eksik alan,
        // yanlış tip). 401 "istek doğru biçimde geldi ama kimliğini
        // doğrulayamadım" demek. İstemciler bu ikisine farklı tepki verir:
        // 401'de kullanıcıyı giriş ekranına yönlendirmek veya jetonu
        // yenilemek gerekir, 400'de formu düzeltmek gerekir.
        //
        // Yenileme jetonu hataları da buraya girer - jeton geçersiz,
        // süresi dolmuş veya yeniden kullanılmış: hepsi "yeniden giriş
        // yapman gerekiyor" anlamına gelir.
        var code when code.EndsWith("InvalidCredentials", StringComparison.Ordinal) =>
            StatusCodes.Status401Unauthorized,

        var code when code.StartsWith("Identity.RefreshToken", StringComparison.Ordinal) =>
            StatusCodes.Status401Unauthorized,

        // "Bir şey bulunamadı" -> 404
        var code when code.EndsWith(".NotFound", StringComparison.Ordinal) =>
            StatusCodes.Status404NotFound,

        // "Zaten var / zaten kayıtlı" -> 409 Conflict. 400 değil, çünkü
        // istek BOZUK değil; sistemin o anki durumuyla çelişiyor.
        var code when code.Contains("Already", StringComparison.Ordinal) =>
            StatusCodes.Status409Conflict,

        // Geri kalan her şey geçersiz girdi -> 400
        _ => StatusCodes.Status400BadRequest,
    };

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => "Authentication failed",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Validation failed",
    };
}
