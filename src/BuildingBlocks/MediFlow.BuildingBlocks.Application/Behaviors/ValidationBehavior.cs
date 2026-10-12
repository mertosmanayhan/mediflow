using System.Reflection;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using MediFlow.BuildingBlocks.Common;

namespace MediFlow.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Her isteği, handler'a ULAŞMADAN ÖNCE doğrular.
/// </summary>
/// <remarks>
/// NASIL ÇALIŞIR?
/// MediatR bu sınıfı her istek tipi için devreye sokar. İçinde
/// <c>next()</c> çağrısı var: o çağrı zincirdeki SONRAKİ halkayı
/// (başka bir behavior veya asıl handler) çalıştırıyor.
///
/// Dolayısıyla <c>next()</c>'i ÇAĞIRMAZSAK handler hiç çalışmaz. Doğrulama
/// başarısızsa tam olarak bunu yapıyoruz - geçersiz veri iş mantığına
/// hiç dokunmuyor.
///
/// Bu kalıp "decorator pattern"in çalışma zamanı hali: her halka bir
/// sonrakini sarıyor ve araya kendi işini ekliyor.
///
/// IEnumerable&lt;IValidator&lt;TRequest&gt;&gt; enjekte ediliyor - DİKKAT,
/// çoğul. Bir istek için doğrulayıcı olmayabilir (sorgular genelde
/// doğrulanmaz) ya da birden fazla olabilir. DI konteyneri kayıtlı
/// olanların hepsini veriyor; hiç yoksa boş liste.
/// </remarks>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        IValidator<TRequest>[] applicable = [.. validators];

        // Doğrulayıcı yoksa yoldan çekil. Sorguların çoğu bu dalda.
        if (applicable.Length == 0)
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        // Doğrulayıcıları PARALEL çalıştırıyoruz: birbirlerine bağlı
        // değiller ve bazıları veritabanına gidebilir (ör. "bu e-posta
        // kayıtlı mı"). Sırayla beklemek boşa gecikme olurdu.
        ValidationResult[] results = await Task.WhenAll(
            applicable.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        // TÜM hataları topluyoruz, ilkinde durmuyoruz. Kullanıcıya formdaki
        // bütün eksikleri bir seferde söylemek istiyoruz.
        Error[] errors = [.. results
            .SelectMany(result => result.Errors)
            .Select(failure => new Error(
                failure.ErrorCode ?? "Validation.Unspecified",
                failure.ErrorMessage))];

        if (errors.Length == 0)
        {
            return await next(cancellationToken);
        }

        // HANDLER ÇAĞRILMIYOR. Zincir burada kesiliyor.
        return CreateFailureResult(new ValidationError(errors));
    }

    /// <summary>
    /// Başarısız bir <see cref="Result{T}"/> üretir.
    /// </summary>
    /// <remarks>
    /// NEDEN REFLECTION?
    /// TResponse derleme zamanında <c>Result&lt;Guid&gt;</c> gibi somut bir
    /// tip, ama bu sınıf generic olduğu için biz onun içindeki T'yi
    /// doğrudan yazamıyoruz. C#'ın tip sistemi "TResponse = Result&lt;T&gt;
    /// ise bana T'yi ver" demeye izin vermiyor.
    ///
    /// MALİYETİ KABUL EDİLEBİLİR, çünkü bu kod SADECE doğrulama
    /// başarısız olduğunda çalışıyor. Mutlu yol (doğrulamanın geçtiği
    /// durum) reflection'a hiç dokunmuyor.
    ///
    /// Alternatif, doğrulama hatasında exception fırlatıp API katmanında
    /// yakalamaktı - daha basit ama "beklenen hatalar Result ile taşınır"
    /// kararımızı bozardı. Bilinçli tercih: tutarlılık için biraz
    /// karmaşıklık.
    /// </remarks>
    private static TResponse CreateFailureResult(ValidationError error)
    {
        Type valueType = typeof(TResponse).GetGenericArguments()[0];

        MethodInfo failure = typeof(Result)
            .GetMethods()
            .Single(method => method.Name == nameof(Result.Failure) && method.IsGenericMethod)
            .MakeGenericMethod(valueType);

        return (TResponse)failure.Invoke(null, [error])!;
    }
}
