using MediFlow.PatientProfile.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediFlow.PatientProfile.Infrastructure;

/// <summary>
/// <see cref="Patient"/> varlığının tablolara nasıl eşlendiği.
/// </summary>
/// <remarks>
/// NEDEN AYRI BİR DOSYA?
/// Eşleme bilgisi Domain'e YAZILAMAZ - Domain'in EF Core'u tanımaması
/// gerekiyor. Bu dosya Infrastructure'da olduğu için Domain hiç etkilenmiyor:
/// Patient sınıfında tek bir EF özniteliği (attribute) yok.
///
/// Domain'in kendini koruma önlemleri (private kurucu, private set, private
/// liste) EF'in işini zorlaştırır. EF bu kapıların arkasına nasıl geçeceğini
/// aşağıda öğreniyor.
/// </remarks>
internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");

        builder.HasKey(patient => patient.Id);

        // ValueGeneratedNever: kimliği VERİTABANI değil DOMAIN üretiyor
        // (Patient.Create içinde Guid.NewGuid). Bunu söylemezsek EF
        // "identity" sütunu bekler ve kendi ürettiğini sanar.
        //
        // Domain'in kimliği üretmesi bilinçli: nesne veritabanına hiç
        // gitmeden geçerli ve tam olmalı.
        builder.Property(patient => patient.Id)
            .ValueGeneratedNever();

        // HasMaxLength olmadan EF nvarchar(max) üretir: indekslenemez ve
        // gereksiz yer kaplar. Uzunluk sınırı bir VERİTABANI kararı,
        // o yüzden Domain'de değil burada.
        builder.Property(patient => patient.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(patient => patient.LastName)
            .HasMaxLength(100)
            .IsRequired();

        // DateOnly -> SQL 'date' sütunu. Saat alanı hiç oluşmuyor.
        builder.Property(patient => patient.BirthDate)
            .IsRequired();

        // Ada göre arama yapacağız (Faz 1 ilerisi), indeks ekliyoruz.
        builder.HasIndex(patient => new { patient.LastName, patient.FirstName });

        // ============================================================
        //  ALERJİLER — "owned" (sahip olunan) koleksiyon
        //
        //  OwnsMany, EF'e şunu söyler: Allergy BAĞIMSIZ bir varlık değil,
        //  Patient'a AİT. Sonuçları:
        //    - Kendi başına sorgulanamaz; her zaman hastasıyla gelir
        //    - Hasta silinince alerjileri de silinir
        //    - Hasta yüklenince alerjileri OTOMATİK yüklenir (Include gerekmez)
        //
        //  Bu, Faz 1 başında anlattığımız "aggregate" fikrinin veritabanı
        //  karşılığı: küme birlikte tutarlıdır, tek kapıdan yönetilir.
        // ============================================================
        builder.OwnsMany(patient => patient.Allergies, allergy =>
        {
            allergy.ToTable("PatientAllergies");

            // Alerjinin kendi kimliği YOK (domain'de bilinçli bir karar).
            // Ama ilişkisel tabloda satırları ayırt edecek bir anahtar
            // gerekiyor. Çözüm: "gölge özellik" (shadow property) - sadece
            // veritabanında var olan, C# sınıfında karşılığı olmayan bir alan.
            allergy.Property<int>("Id").ValueGeneratedOnAdd();
            allergy.HasKey("Id");

            allergy.WithOwner().HasForeignKey("PatientId");

            allergy.Property(a => a.Name)
                .HasMaxLength(200)
                .IsRequired();

            // Severity int olarak saklanıyor (EF'in enum varsayılanı).
            // Metin olarak saklamak okunabilir olurdu AMA şiddet SIRALI bir
            // değer: "High >= Moderate" sorgusu yazabilmek istiyoruz.
            // Metinde sıralama alfabetik olur ve "High" < "Low" çıkar - yanlış.
            // Enum üyelerine açık sayı vermemizin sebebi de bu: saklanan
            // sayıların anlamı, üyeleri yeniden sıralasak bile kaymaz.
            allergy.Property(a => a.Severity)
                .IsRequired();

            // Aynı hastada aynı alerji iki kez olamaz. Kural Patient içinde
            // ZATEN var; bu indeks İKİNCİ bir savunma hattı. Uygulamada bir
            // hata olsa bile veritabanı bozuk veriyi kabul etmez.
            allergy.HasIndex("PatientId", nameof(Allergy.Name)).IsUnique();
        });

        // ============================================================
        //  EN KRİTİK SATIR
        //
        //  Patient.Allergies özelliği "_allergies.AsReadOnly()" döndürüyor -
        //  yani hesaplanmış ve SALT OKUNUR. EF oraya yazamaz.
        //
        //  Bu satır EF'e "özelliği değil, ARKASINDAKİ ALANI kullan" diyor.
        //  EF, ismi eşleşen '_allergies' alanını bulup doğrudan ona yazıyor.
        //
        //  Bu olmadan EF ya model kurarken patlar ya da alerjileri hiç
        //  yükleyemez. Ve dikkat: kapsüllemeden VAZGEÇMİYORUZ - dışarıya
        //  hâlâ salt okunur görünüm gidiyor, sadece EF'e özel bir kapı açtık.
        // ============================================================
        builder.Navigation(patient => patient.Allergies)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
