using MediatR;
using MediFlow.BuildingBlocks.Common;

namespace MediFlow.PatientProfile.Application.Abstractions;

/// <summary>
/// Durumu DEĞİŞTİREN bir istek.
/// </summary>
/// <remarks>
/// MediatR'ın <c>IRequest</c>'ini doğrudan kullanmak yerine kendi
/// arayüzümüzü koyuyoruz. İki kazancı var:
///
/// 1. NİYET GÖRÜNÜR OLUR. Tipin adına bakmadan "bu yazma işlemi mi okuma
///    işlemi mi" anlaşılır. CQRS'in kod seviyesindeki karşılığı bu.
///
/// 2. BEHAVIOR'LARI HEDEFLEYEBİLİRİZ. İleride transaction behavior'ını
///    SADECE komutlara uygulamak isteyeceğiz - sorgular zaten veri
///    değiştirmediği için transaction açmak boşa maliyet. Marker arayüz
///    olmadan "bu bir komut mu?" sorusunu soramazdık.
///
/// Dönüş tipi <see cref="Result{T}"/>'e sabitlendi: her iş akışı başarı
/// veya hata döndürmek ZORUNDA, exception fırlatmakla kurtulamaz.
/// </remarks>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}

/// <summary>
/// Sadece OKUYAN bir istek. Veriyi değiştirmez.
/// </summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}

// ============================================================
//  NEDEN BU DOSYA BuildingBlocks'TA DEĞİL?
//
//  Bu arayüzler gerçekten geneldir ve Faz 2'de her modül kullanacak.
//  Ama BuildingBlocks.Common'a koysak MediatR paketi oraya girerdi -
//  ve Domain katmanı Common'a referans verdiği için MediatR DOMAIN'E
//  DE sızardı. Domain'in hiçbir altyapı kütüphanesini tanımaması ise
//  mimarinin temel kuralı.
//
//  Çözüm ikinci modül gelince: ayrı bir BuildingBlocks.Application
//  projesi açıp buraya taşımak. Şimdi yapmıyoruz çünkü tek modül var
//  ve soyutlamayı ihtiyaç doğmadan kurmuyoruz (YAGNI).
// ============================================================
