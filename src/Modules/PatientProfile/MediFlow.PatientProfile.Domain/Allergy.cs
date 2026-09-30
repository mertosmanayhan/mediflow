namespace MediFlow.PatientProfile.Domain;

/// <summary>
/// How dangerous a reaction is. Drives triage and alerting later on.
/// </summary>
public enum AllergySeverity
{
    // Sayılar AÇIKÇA yazıldı. Enum üyelerini ileride alfabetik sıraya
    // dizmek isteyebiliriz; numarayı yazmazsak sıra değişince kayıtlı
    // veriler kayar (veritabanında sayı olarak tutulacak).
    Low = 1,
    Moderate = 2,
    High = 3,
    LifeThreatening = 4,
}

/// <summary>
/// An allergy on a patient's record.
/// </summary>
/// <remarks>
/// Bunun kimliği (Id) YOK ve olmamalı: içeriği aynı olan iki alerji kaydı
/// aynı şeydir. Bu yüzden <c>record</c> - tıpkı BuildingBlocks'taki
/// <c>Error</c> gibi. <c>record</c> değer eşitliği verir, yani iki alerjinin
/// eşit olup olmadığı içeriğine bakılarak anlaşılır.
///
/// Kuralları (adın boş olmaması, tekrarlanmaması) burada değil
/// <see cref="Patient"/> içinde korunuyor: listenin tutarlılığından sorumlu
/// olan, listeyi tutan nesnedir.
/// </remarks>
public sealed record Allergy(string Name, AllergySeverity Severity);
