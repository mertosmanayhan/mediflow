using MediFlow.PatientProfile.Domain;
using Microsoft.EntityFrameworkCore;

namespace MediFlow.PatientProfile.Infrastructure;

/// <summary>
/// <see cref="IPatientRepository"/>'nin veritabanı karşılığı.
/// </summary>
/// <remarks>
/// SaveChanges ARTIK BURADA ÇAĞRILMIYOR.
///
/// Önceki hali her ekleme işleminde hemen yazıyordu. Bir komut iki şey
/// değiştirdiğinde bu bir problem: ilki yazılır, ikincisi patlar, ortada
/// tutarsız veri kalır.
///
/// Artık yazma işini pipeline'daki UnitOfWorkBehavior yapıyor ve istek
/// başına TEK SaveChanges çağrılıyor - EF onu kendi transaction'ına
/// sardığı için ya hepsi yazılıyor ya hiçbiri. Repository'nin işi
/// "değişikliği bildirmek", "yazmak" değil.
/// </remarks>
public sealed class EfPatientRepository(PatientProfileDbContext context) : IPatientRepository
{
    public Task AddAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        context.Patients.Add(patient);
        return Task.CompletedTask;
    }

    /// <remarks>
    /// Alerjiler için <c>Include</c> yazmıyoruz: "owned" koleksiyon
    /// oldukları için EF onları her zaman hastayla beraber getiriyor.
    ///
    /// Silinmiş kayıtlar için de filtre yazmıyoruz: global query filter
    /// her sorguya "IsDeleted = 0" ekliyor.
    /// </remarks>
    public async Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Patients
            .FirstOrDefaultAsync(patient => patient.Id == id, cancellationToken);

    // EF'e "sil" diyoruz; SoftDeleteInterceptor bunu güncellemeye
    // çeviriyor. Bu sınıf soft delete diye bir şey bilmiyor.
    public void Remove(Patient patient) => context.Patients.Remove(patient);
}
