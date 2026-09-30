using MediFlow.PatientProfile.Domain;
using Microsoft.EntityFrameworkCore;

namespace MediFlow.PatientProfile.Infrastructure;

/// <summary>
/// <see cref="IPatientRepository"/>'nin gerçek veritabanı karşılığı.
/// </summary>
/// <remarks>
/// SİPARİŞ FORMUNU DOLDURAN İKİNCİ SINIF.
///
/// Birincisi InMemoryPatientRepository'ydi (hafızada tutuyordu). Bu sınıf
/// aynı arayüzü dolduruyor ama MSSQL'e yazıyor.
///
/// VE EN ÖNEMLİSİ: Application katmanındaki CreatePatientHandler'ın TEK
/// SATIRI DEĞİŞMEDİ. O hangi sınıfın verildiğini ayırt edemiyor. İki adım
/// önce "arayüz = sipariş formu" derken verdiğimiz sözün karşılığı bu.
/// </remarks>
public sealed class EfPatientRepository(PatientProfileDbContext context) : IPatientRepository
{
    public async Task AddAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        context.Patients.Add(patient);

        // NOT: SaveChanges'i şimdilik burada çağırıyoruz - tek aggregate
        // değiştiren basit bir akış için yeterli.
        //
        // Ama bu geçici: bir istek İKİ ayrı aggregate değiştirdiğinde
        // (ör. "randevu oluştur + hastanın geçmişine ekle") ikisinin TEK
        // transaction'da yazılması gerekir. O zaman SaveChanges buradan
        // çıkıp ortak bir yere (Unit of Work / transaction behavior)
        // taşınacak. Sorunu yaşamadan soyutlama eklemiyoruz.
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <remarks>
    /// Alerjiler için <c>Include</c> yazmıyoruz: "owned" koleksiyon oldukları
    /// için EF onları her zaman hastayla beraber getiriyor.
    /// </remarks>
    public async Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Patients
            .FirstOrDefaultAsync(patient => patient.Id == id, cancellationToken);
}
