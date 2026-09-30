using System.Collections.Concurrent;
using MediFlow.PatientProfile.Domain;

namespace MediFlow.PatientProfile.Infrastructure;

/// <summary>
/// GEÇİCİ depo: hastaları uygulamanın hafızasında tutar.
/// </summary>
/// <remarks>
/// NEDEN ŞİMDİLİK BU?
/// HTTP tarafını ve veritabanı tarafını AYRI AYRI öğrenmek için. İkisi birden
/// gelirse bir hata çıktığında kaynağını ayırt etmek zorlaşır. Bu adımda
/// API'yi çalıştırıp göreceğiz; bir sonraki adımda bu sınıfın yerine EF Core
/// ile MSSQL'e yazan bir sınıf gelecek.
///
/// O gün Application katmanındaki CreatePatientHandler'ın kodu DEĞİŞMEYECEK -
/// aynı arayüzü dolduran başka bir sınıf verilmiş olacak, o kadar.
///
/// UYARI: Uygulama yeniden başlayınca veriler kaybolur. Bu kasıtlı ve geçici.
///
/// ConcurrentDictionary kullanıyoruz çünkü bir web uygulamasında birden fazla
/// istek AYNI ANDA çalışır. Sıradan bir Dictionary'e iki istek aynı anda
/// yazarsa veri yapısı bozulabilir ve teşhisi çok zor hatalar çıkar.
/// </remarks>
public sealed class InMemoryPatientRepository : IPatientRepository
{
    private readonly ConcurrentDictionary<Guid, Patient> _patients = new();

    public Task AddAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        _patients[patient.Id] = patient;
        return Task.CompletedTask;
    }

    public Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _patients.TryGetValue(id, out Patient? patient);
        return Task.FromResult(patient);
    }
}
