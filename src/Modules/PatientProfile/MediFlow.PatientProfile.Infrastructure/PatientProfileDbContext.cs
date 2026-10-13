using MediFlow.BuildingBlocks.Application;
using MediFlow.BuildingBlocks.Infrastructure;
using MediFlow.PatientProfile.Domain;
using Microsoft.EntityFrameworkCore;

namespace MediFlow.PatientProfile.Infrastructure;

/// <summary>
/// PatientProfile modülünün veritabanı oturumu.
/// </summary>
/// <remarks>
/// IUnitOfWork uyguluyor: pipeline'daki UnitOfWorkBehavior bunu bulup
/// komut başarıyla tamamlandığında SaveChanges çağırıyor. DbContext zaten
/// bu sözleşmeyi karşılayan bir metoda sahip olduğu için ek bir sınıf
/// yazmak gerekmiyor - gereksiz bir sarmalayıcı katmanı eklemiyoruz.
///
/// Modül başına ayrı DbContext ve ayrı şema: Faz 2'de bu modül kendi
/// servisine çıkarıldığında şemayı taşımak yeterli olacak.
/// </remarks>
public sealed class PatientProfileDbContext(DbContextOptions<PatientProfileDbContext> options)
    : DbContext(options), IUnitOfWork
{
    internal const string Schema = "PatientProfile";

    public DbSet<Patient> Patients => Set<Patient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // Türkçe harf ve sıralama kuralları - ADR-0003.
        modelBuilder.UseCollation("Turkish_100_CI_AS_SC");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PatientProfileDbContext).Assembly);

        // ⚠️ SIRA ÖNEMLİ: filtre, varlık yapılandırmalarından SONRA
        // uygulanıyor. Önce çağrılsa modelde henüz varlıklar olmadığı için
        // hiçbir şeye filtre takılmazdı - sessizce çalışan ama işe
        // yaramayan bir satır olurdu.
        modelBuilder.ApplySoftDeleteFilter();
    }
}
