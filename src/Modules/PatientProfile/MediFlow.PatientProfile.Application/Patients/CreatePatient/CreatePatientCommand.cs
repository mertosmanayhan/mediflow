using MediFlow.PatientProfile.Application.Abstractions;

namespace MediFlow.PatientProfile.Application.Patients.CreatePatient;

/// <summary>
/// "Yeni hasta kaydet" komutu.
/// </summary>
/// <remarks>
/// EMİR KİPİNDE ADLANDIRILIR: CreatePatient, CancelAppointment,
/// PublishLabResult. Komut bir İSTEK değil, bir TALİMATTIR.
///
/// Dönüş tipi <c>ICommand&lt;Guid&gt;</c>: oluşan hastanın kimliği.
/// Komutlar genelde tüm nesneyi geri döndürmez - çağıran zaten ne
/// gönderdiğini biliyor, tek eksiği sunucunun ürettiği kimlik.
/// </remarks>
public sealed record CreatePatientCommand(
    string FirstName,
    string LastName,
    DateOnly BirthDate) : ICommand<Guid>;
