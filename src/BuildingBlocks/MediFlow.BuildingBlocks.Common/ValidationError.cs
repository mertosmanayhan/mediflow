namespace MediFlow.BuildingBlocks.Common;

/// <summary>
/// An <see cref="Error"/> that carries several failures at once.
/// </summary>
/// <remarks>
/// NEDEN AYRI BİR TİP?
/// Girdi doğrulaması tek bir hatayla bitmez. Kullanıcı formu yanlış
/// doldurduğunda ona TÜM eksikleri bir seferde söylemek isteriz; tek tek
/// göstermek ("önce adı düzelt... şimdi de soyadı") kötü bir deneyim.
///
/// Yine de <see cref="Error"/>'dan TÜRÜYOR, çünkü Result tipi tek bir Error
/// taşıyor ve onu değiştirmek istemiyoruz. Böylece doğrulama hatası, akışta
/// sıradan bir hata gibi taşınabiliyor; sadece API katmanı onu açıp
/// içindeki listeyi cevaba yazıyor.
///
/// Bu, "Liskov" dediğimiz ilkenin pratik hali: türetilmiş tip, temel tipin
/// geçtiği her yerde sorunsuz geçebiliyor.
///
/// NOT: Koleksiyon içeren record'larda değer eşitliği beklendiği gibi
/// çalışmaz (liste referansla karşılaştırılır). Bu tipi eşitlik için
/// kullanmıyoruz, sorun değil.
/// </remarks>
public sealed record ValidationError(IReadOnlyList<Error> Errors)
    : Error(ErrorCode, "One or more validation errors occurred.")
{
    /// <summary>
    /// Sabit kod. API katmanı bu kodu görünce cevaba hata listesini ekliyor.
    /// </summary>
    public const string ErrorCode = "Validation.Failed";
}
