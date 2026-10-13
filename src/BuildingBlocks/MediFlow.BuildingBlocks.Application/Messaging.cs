using MediatR;
using MediFlow.BuildingBlocks.Common;

namespace MediFlow.BuildingBlocks.Application;

/// <summary>
/// Komutları tip seviyesinde işaretleyen generic OLMAYAN arayüz.
/// </summary>
/// <remarks>
/// NEDEN AYRI BİR İŞARETLEYİCİ?
/// Pipeline behavior'ları generic kısıtla filtrelenebilir:
///
///     where TRequest : IBaseCommand
///
/// MediatR bu kısıtı sağlamayan isteklere o behavior'ı UYGULAMIYOR.
/// Böylece UnitOfWorkBehavior sadece komutlarda çalışıyor; sorgularda
/// hiç devreye girmiyor - sorgu veri değiştirmediği için kaydetmeye
/// çalışmak boşa maliyet olurdu.
///
/// Generic ICommand&lt;T&gt; ile bu yapılamaz çünkü T her komutta farklı;
/// generic olmayan bir üst arayüz gerekiyor.
///
/// Blok 1'de bu arayüzleri kurarken "ileride transaction behavior'ını
/// sadece komutlara uygulamak isteyeceğiz" demiştik. Karşılığı bu.
/// </remarks>
public interface IBaseCommand
{
}

/// <summary>Durumu DEĞİŞTİREN bir istek.</summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand
{
}

/// <summary>Sadece OKUYAN bir istek.</summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}

/// <summary>
/// Bir modülün bekleyen değişikliklerini tek seferde yazma sözleşmesi.
/// </summary>
/// <remarks>
/// Her modülün DbContext'i bunu uyguluyor. Handler'lar artık SaveChanges
/// ÇAĞIRMIYOR - sadece depoya ekliyor/değiştiriyor, yazma işini pipeline
/// yapıyor.
///
/// KAZANCI: bir komut iki aggregate değiştirdiğinde ikisi TEK SaveChanges
/// ile yazılıyor. EF SaveChanges'i kendi içinde bir transaction'a sarıyor,
/// yani ya ikisi birden yazılıyor ya hiçbiri. Handler'ların arasına
/// SaveChanges serpiştirildiğinde bu garanti kaybolur: ilki yazılır,
/// ikincisi patlar, ortada tutarsız veri kalır.
/// </remarks>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
