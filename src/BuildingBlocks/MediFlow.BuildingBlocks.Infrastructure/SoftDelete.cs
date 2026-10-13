using System.Linq.Expressions;
using MediFlow.BuildingBlocks.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MediFlow.BuildingBlocks.Infrastructure;

/// <summary>
/// Silme isteklerini güncellemeye çeviren interceptor.
/// </summary>
/// <remarks>
/// Repository <c>Remove(entity)</c> çağırıyor - domain ve uygulama kodu
/// "sil" diyor ve soft delete diye bir şey bilmiyor. Bu interceptor
/// EF'in <c>Deleted</c> durumunu <c>Modified</c>'a çevirip işaretleri
/// yazıyor.
///
/// Böylece soft delete TAMAMEN altyapıda kalıyor: yarın gerçekten silmeye
/// karar verirsek interceptor'ı kaldırmak yeterli, iş kodu değişmez.
/// </remarks>
public sealed class SoftDeleteInterceptor(TimeProvider clock) : SaveChangesInterceptor
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

        foreach (EntityEntry<ISoftDeletable> entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
            {
                continue;
            }

            // SİLME -> GÜNCELLEME. SQL'e DELETE değil UPDATE gidiyor.
            entry.State = EntityState.Modified;
            entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
            entry.Property(nameof(ISoftDeletable.DeletedAtUtc)).CurrentValue = nowUtc;
        }
    }
}

public static class SoftDeleteModelBuilderExtensions
{
    /// <summary>
    /// <see cref="ISoftDeletable"/> olan tüm varlıklara "silinmemiş olanlar"
    /// filtresi uygular.
    /// </summary>
    /// <remarks>
    /// GLOBAL QUERY FILTER: EF bundan sonra o varlığa yapılan HER sorguya
    /// otomatik olarak "WHERE IsDeleted = 0" ekliyor. Sorgu yazan kişinin
    /// bunu hatırlaması gerekmiyor.
    ///
    /// Filtreyi aşmak gerektiğinde (yönetici ekranı, denetim raporu)
    /// <c>IgnoreQueryFilters()</c> kullanılır - açıkça yazılması gereken
    /// bir istisna, varsayılan davranış değil.
    ///
    /// ⚠️ BİLİNEN TUZAK: Filtre navigasyonlara otomatik uygulanmaz ve
    /// zorunlu (required) ilişkilerde EF uyarı verir - ana kayıt
    /// filtrelenirken bağlı kayıt filtrelenmezse tutarsız sonuç çıkar.
    /// Bu projede soft delete sadece aggregate root'a (Patient) uygulandı;
    /// ona ait alerjiler zaten onunla birlikte yükleniyor.
    ///
    /// Yansıma (reflection) ile tüm varlıkları gezip filtre kuruyoruz;
    /// her varlığa elle filtre yazmak unutulmaya açık olurdu.
    /// </remarks>
    public static void ApplySoftDeleteFilter(this ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            ParameterExpression parameter = Expression.Parameter(entityType.ClrType, "entity");

            MemberExpression isDeleted = Expression.Property(
                parameter,
                nameof(ISoftDeletable.IsDeleted));

            // entity => entity.IsDeleted == false
            LambdaExpression filter = Expression.Lambda(
                Expression.Equal(isDeleted, Expression.Constant(false)),
                parameter);

            entityType.SetQueryFilter(filter);
        }
    }
}
