using MediFlow.PatientProfile.Application.Abstractions;

namespace MediFlow.PatientProfile.Application.Patients.GetPatientById;

/// <summary>
/// Kimliğe göre hasta getirme sorgusu.
/// </summary>
/// <remarks>
/// SORU KİPİNDE ADLANDIRILIR: GetPatientById, SearchPatients,
/// ListTodaysAppointments. Sorgu hiçbir şeyi değiştirmez.
///
/// NEDEN SORGULARI AYIRIYORUZ (CQRS)?
/// Yazma ve okuma ihtiyaçları farklı yönde evrilir:
///   - Yazma: iş kuralları, tutarlılık, aggregate sınırları önemli
///   - Okuma: hız, şekil esnekliği, birden fazla kaynağı birleştirme önemli
///
/// Faz 8'de bu ayrımın karşılığını somut olarak göreceksin: okuma tarafı
/// Elasticsearch'ten beslenecek, yazma tarafı MSSQL'de kalacak. Ayrım
/// şimdiden kurulu olduğu için o geçiş yazma tarafına hiç dokunmayacak.
///
/// Şu an basit: ikisi de aynı veritabanına gidiyor. CQRS "iki veritabanı
/// kullan" demek DEĞİL - "iki sorumluluğu ayır" demek.
/// </remarks>
public sealed record GetPatientByIdQuery(Guid PatientId) : IQuery<PatientDto>;
