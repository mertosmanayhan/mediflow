namespace MediFlow.BuildingBlocks.Common;

/// <summary>
/// Oluşturma ve güncelleme zamanı takip edilen varlık.
/// </summary>
/// <remarks>
/// Özellikler SALT OKUNUR. Değerleri bir EF interceptor'ı otomatik yazıyor;
/// domain kodu bunlara hiç dokunmuyor.
///
/// NEDEN HANDLER'DA DEĞİL INTERCEPTOR'DA?
/// Audit alanı klasik bir cross-cutting concern: 40 handler'ın hepsinde
/// "CreatedAtUtc = now" yazmak gerekirdi ve biri unutulduğunda kimse fark
/// etmezdi - üstelik eksik audit verisi, en çok ihtiyaç duyulduğu anda
/// (bir olay soruşturulurken) eksik olur.
///
/// Interceptor SaveChanges'i yakalayıp değişen tüm varlıkları işaretliyor:
/// unutulması imkansız.
/// </remarks>
public interface IAuditable
{
    DateTimeOffset CreatedAtUtc { get; }

    DateTimeOffset? UpdatedAtUtc { get; }
}

/// <summary>
/// Silinmek yerine silinmiş olarak İŞARETLENEN varlık.
/// </summary>
/// <remarks>
/// NEDEN GERÇEKTEN SİLMİYORUZ?
///   - Yasal: sağlık verisinde saklama yükümlülüğü var; "sildim" demek
///     kaydı yok etmek anlamına gelmiyor.
///   - Denetim: "bu kaydı kim, ne zaman sildi" sorusu cevaplanabilir kalır.
///   - Kurtarma: yanlışlıkla silinen kayıt geri alınabilir.
///   - Bütünlük: kayda referans veren başka kayıtlar (geçmiş muayeneler)
///     yetim kalmaz.
///
/// BEDELİ GERÇEK - mülakatta bu kısım sorulur:
///   - Her sorguya "AND IsDeleted = 0" eklenmesi gerekir. Global query
///     filter bunu otomatikleştiriyor ama unutulduğu yerde silinmiş kayıt
///     geri sızar.
///   - UNIQUE indeksler bozulur: aynı e-posta ile silinmiş bir kayıt
///     varsa yenisi eklenemez. Çözüm filtrelenmiş indeks
///     (WHERE IsDeleted = 0).
///   - Tablo büyümeye devam eder; arşivleme stratejisi gerekir.
///   - İlişkili veri gerçekten silinmediği için KVKK'nın "silme hakkı"
///     talepleri ayrı ele alınmak zorunda - soft delete tek başına
///     "veriyi sildim" demek için YETERLİ DEĞİLDİR.
/// </remarks>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAtUtc { get; }
}
