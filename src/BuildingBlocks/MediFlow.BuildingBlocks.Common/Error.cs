namespace MediFlow.BuildingBlocks.Common;

/// <summary>
/// Describes a single failure: a stable machine-readable <paramref name="Code"/>
/// plus a human-readable <paramref name="Message"/>.
/// </summary>
/// <remarks>
/// NEDEN 'sealed' DEĞİL? (Faz 0'da sealed'dı, 2026-10-12'de açıldı.)
///
/// Girdi doğrulaması BİRDEN FAZLA hata üretir ("ad boş" VE "soyad çok uzun").
/// Tek bir Error bunu taşıyamaz, o yüzden <see cref="ValidationError"/>
/// türetiliyor.
///
/// Faz 0'da sealed yapma gerekçem "kalıtım record'ların değer eşitliğini
/// şaşırtıcı hale getirir"di. Bakınca o endişe yersiz: C# record'ları
/// eşitlik karşılaştırmasına gizli bir 'EqualityContract' (çalışma zamanı
/// tipi) kontrolü ekler. Yani bir Error ile aynı içerikli bir
/// ValidationError eşit SAYILMAZ - istenen davranış tam olarak bu.
/// Record'larda kalıtım, sıradan sınıflardaki kadar riskli değil.
/// </remarks>
/// <param name="Code">
/// Machine-readable, stable identifier such as <c>Appointment.SlotTaken</c>.
/// Clients branch on this, HTTP status mapping is derived from it, and it must
/// never change once released - it is part of the API contract.
/// </param>
/// <param name="Message">
/// Human-readable description, intended for developers and logs.
/// End-user text is resolved from <paramref name="Code"/> by the presentation
/// layer, so this string is safe to change at any time.
/// </param>
public record Error(string Code, string Message)
{
    /// <summary>
    /// The absence of an error. Every successful <see cref="Result"/> carries this.
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty);
}
