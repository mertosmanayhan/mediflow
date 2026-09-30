using MediFlow.PatientProfile.Domain;

namespace MediFlow.PatientProfile.Application.Tests;

/// <summary>
/// Hastaları hafızada tutan depo. Sipariş formunu (IPatientRepository)
/// dolduran ilk sınıf.
/// </summary>
/// <remarks>
/// Veritabanı YOK. Docker YOK. Kurulum YOK. Testler milisaniyeler içinde
/// koşuyor ve her test kendi boş deposuyla başlıyor.
///
/// İki adım sonra bu arayüzü EF Core ile MSSQL'e yazan bir sınıf dolduracak.
/// CreatePatientHandler'ın kodu O ZAMAN DA DEĞİŞMEYECEK - arayüz sayesinde
/// hangisinin verildiğini ayırt edemiyor.
/// </remarks>
internal sealed class InMemoryPatientRepository : IPatientRepository
{
    private readonly List<Patient> _patients = [];

    /// <summary>Testin "kaydedildi mi?" sorusunu sorabilmesi için.</summary>
    public IReadOnlyList<Patient> Saved => _patients.AsReadOnly();

    public Task AddAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        _patients.Add(patient);
        return Task.CompletedTask;
    }

    public Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Patient? found = _patients.FirstOrDefault(patient => patient.Id == id);
        return Task.FromResult(found);
    }
}

/// <summary>
/// Saati sabitleyen <see cref="TimeProvider"/>. Testler gerçek takvimden
/// bağımsız olsun diye.
/// </summary>
/// <remarks>
/// TimeProvider .NET'in hazır saat soyutlamasıdır. Üretimde
/// <c>TimeProvider.System</c> gerçek saati verir; testte bunu veriyoruz.
/// Tek bir metodu geçersiz kılmak yeterli.
/// </remarks>
internal sealed class FixedClock(DateOnly today) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() =>
        new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
