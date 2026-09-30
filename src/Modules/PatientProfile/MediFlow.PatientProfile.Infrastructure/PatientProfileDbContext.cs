using MediFlow.PatientProfile.Domain;
using Microsoft.EntityFrameworkCore;

namespace MediFlow.PatientProfile.Infrastructure;

/// <summary>
/// PatientProfile modülünün veritabanı oturumu.
/// </summary>
/// <remarks>
/// DbContext iki iş yapar:
///   1. Nesnelerle tablolar arasında çeviri (ORM = Object-Relational Mapper)
///   2. Değişiklikleri takip eder ve SaveChanges'te tek seferde yazar
///
/// MODÜL BAŞINA AYRI DbContext. Tek bir büyük DbContext yerine her modülün
/// kendi oturumu var, ve her biri kendi ŞEMASINDA çalışıyor. Sebebi ADR-0001:
/// Faz 2'de bu modülü ayrı bir servise çıkaracağız; o gün şemayı ayrı bir
/// veritabanına taşımak, iç içe geçmiş tablolardan ayıklamaktan kat kat kolay.
/// </remarks>
public sealed class PatientProfileDbContext(DbContextOptions<PatientProfileDbContext> options)
    : DbContext(options)
{
    /// <summary>Hasta tablosuna sorgu başlangıç noktası.</summary>
    public DbSet<Patient> Patients => Set<Patient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Bu modülün tabloları kendi şemasında toplanır.
        modelBuilder.HasDefaultSchema(Schema);

        // ============================================================
        //  COLLATION - ADR-0003
        //
        //  Sunucu varsayılanı SQL_Latin1_General_CP1_CI_AS ve Türkçe için
        //  YANLIŞ: Türkçe'de i'nin büyüğü İ, I'nın küçüğü ı'dır. Latin1
        //  bunu bilmediği için "İSMAİL" aramak "ismail" kaydını bulamaz.
        //  Ayrıca sıralama bozuk olur: Ç, C'den sonra gelmelidir.
        //
        //  Turkish_100_CI_AS_SC:
        //    Turkish -> Türk alfabesi sırası ve harf kuralları
        //    100     -> collation sürümü (eski sürümsüzden daha güncel)
        //    CI      -> büyük/küçük harf duyarsız (arama kolaylığı)
        //    AS      -> aksan DUYARLI ("Cetin" ile "Çetin" farklı soyadlar)
        //    SC      -> temel düzlem dışı karakter desteği
        //
        //  _UTF8 varyantı alınmadı: o varchar için anlamlı, biz nvarchar
        //  kullanıyoruz (EF'in metin varsayılanı).
        // ============================================================
        modelBuilder.UseCollation("Turkish_100_CI_AS_SC");

        // Yapılandırma sınıflarını (IEntityTypeConfiguration) otomatik bul.
        // Böylece her varlığın eşlemesi kendi dosyasında durur; bu metot
        // 20 varlıkta da 5 satır kalır.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PatientProfileDbContext).Assembly);
    }

    /// <summary>Bu modülün veritabanı şeması.</summary>
    internal const string Schema = "PatientProfile";
}
