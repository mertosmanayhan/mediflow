using MediatR;
using MediFlow.BuildingBlocks.Common;

namespace MediFlow.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Komut başarıyla tamamlandığında bekleyen değişiklikleri yazar.
/// </summary>
/// <remarks>
/// SADECE KOMUTLARDA ÇALIŞIR - generic kısıt sayesinde
/// (<c>where TRequest : IBaseCommand</c>). Sorgular bu behavior'a hiç
/// girmiyor.
///
/// BAŞARISIZ KOMUT YAZILMIYOR. Handler <c>Result.Failure</c> döndürdüyse
/// SaveChanges çağrılmıyor: iş kuralı reddettiğinde yarım kalmış
/// değişiklikler veritabanına sızmıyor. Bu, "exception fırlatmıyoruz"
/// kararının gizli bir maliyetini kapatıyor - exception olsaydı akış
/// zaten kesilirdi, Result ile akış devam ettiği için bu kontrol şart.
///
/// ⚠️ NEDEN ELLE TRANSACTION AÇMIYORUZ?
/// EF'in SaveChanges'i zaten kendi içinde bir transaction kullanıyor, ve
/// istek başına TEK SaveChanges çağırdığımız için ek bir transaction
/// gerekmiyor. Bu aynı zamanda EnableRetryOnFailure ile yaşanacak çakışmayı
/// da önlüyor: retry açıkken elle açılan transaction, yeniden denemenin
/// nereden başlayacağını belirsiz bıraktığı için EF hata verir ve
/// execution strategy içinde sarmak gerekir. İhtiyaç yoksa karmaşıklığı
/// da eklemiyoruz.
///
/// ⚠️ MODÜLLER ARASI ATOMİKLİK YOK - BİLİNÇLİ.
/// Identity ve PatientProfile AYRI veritabanlarında. İki ayrı veritabanına
/// tek transaction ile yazmak dağıtık transaction (2PC) gerektirir; bu
/// hem kırılgan hem ölçeklenmeyen bir çözümdür ve modern dağıtık
/// sistemlerde kaçınılır.
///
/// Doğru model: bir komut TEK modülü değiştirir. Modüller arası
/// tutarlılık, Faz 3'te Outbox pattern ile "sonunda tutarlı" (eventually
/// consistent) olarak sağlanacak - mülakatta "mikroservislerde transaction
/// nasıl yönetilir?" sorusunun cevabı tam olarak budur.
/// </remarks>
public sealed class UnitOfWorkBehavior<TRequest, TResponse>(
    IEnumerable<IUnitOfWork> unitsOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        TResponse response = await next(cancellationToken);

        if (response is Result { IsFailure: true })
        {
            return response;
        }

        foreach (IUnitOfWork unitOfWork in unitsOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return response;
    }
}
