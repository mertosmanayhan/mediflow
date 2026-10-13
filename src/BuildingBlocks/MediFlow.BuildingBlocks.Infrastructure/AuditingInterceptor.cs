using MediFlow.BuildingBlocks.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MediFlow.BuildingBlocks.Infrastructure;

/// <summary>
/// Kaydetme anında audit alanlarını otomatik dolduran interceptor.
/// </summary>
/// <remarks>
/// SaveChanges çağrıldığında, henüz veritabanına yazılmadan ÖNCE araya
/// giriyor ve değişiklik izleyicisindeki (change tracker) varlıkları
/// gözden geçiriyor.
///
/// Değerleri doğrudan özelliğe atamıyor - özellikler salt okunur.
/// Bunun yerine EF'in Property API'si üzerinden yazıyor. Böylece domain
/// sınıflarına audit için public setter açmak gerekmiyor: kapsülleme
/// bozulmadan altyapı işini yapıyor.
///
/// TimeProvider enjekte ediliyor, DateTimeOffset.UtcNow kullanılmıyor -
/// testte zamanı sabitleyebilmek için.
/// </remarks>
public sealed class AuditingInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTimeOffset nowUtc = clock.GetUtcNow();

        foreach (EntityEntry<IAuditable> entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(IAuditable.CreatedAtUtc)).CurrentValue = nowUtc;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(IAuditable.UpdatedAtUtc)).CurrentValue = nowUtc;

                    // CreatedAtUtc'yi DEĞİŞMEZ kıl: bir güncelleme onu
                    // kazara ezerse kaydın gerçek oluşturulma zamanı
                    // sonsuza kadar kaybolur.
                    entry.Property(nameof(IAuditable.CreatedAtUtc)).IsModified = false;
                    break;

                default:
                    break;
            }
        }
    }
}
