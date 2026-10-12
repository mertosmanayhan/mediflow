using MediFlow.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediFlow.Identity.Infrastructure;

/// <summary>
/// Identity modülünün veritabanı oturumu. Kendi şemasında çalışır.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options)
{
    internal const string Schema = "Identity";

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.UseCollation("Turkish_100_CI_AS_SC");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        // Email bir değer nesnesi: tek sütuna dönüştürülüyor.
        // Conversion sayesinde Domain'de Email tipi, veritabanında string.
        builder.Property(user => user.Email)
            .HasConversion(
                email => email.Value,
                value => Email.Create(value).Value)
            .HasColumnName("Email")
            .HasMaxLength(Email.MaxLength)
            .IsRequired();

        // ⚠️ TEKİLLİK GARANTİSİ BURADA.
        // Handler'daki "zaten kayıtlı mı" kontrolü kullanıcıya anlaşılır
        // hata vermek için; GERÇEK garanti bu indeks. İki istek aynı anda
        // gelirse kontrol ikisinde de "yok" diyebilir, ama veritabanı
        // ikinciyi reddeder.
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc).IsRequired();

        // Roller basit metin listesi: ayrı tabloya değer, ama bu ölçekte
        // JSON sütunu yeterli ve sorgusu kolay. Rol sayısı artıp rol
        // bazlı sorgu gerekirse ayrı tabloya taşınır.
        // PrimitiveCollection: metin listesi tek bir JSON sütununa yazılır.
        // Ayrı tabloya değer ama bu ölçekte gereksiz; rol bazlı sorgu
        // ihtiyacı doğarsa ayrı tabloya taşınır.
        //
        // UsePropertyAccessMode(Field): Roles özelliği _roles.AsReadOnly()
        // döndürüyor, EF oraya yazamaz. Arkadaki alanı kullanmasını
        // söylüyoruz - PatientProfile'daki alerji koleksiyonuyla aynı
        // mantık, sadece bu bir navigation değil ilkel koleksiyon olduğu
        // için API farklı.
        builder.PrimitiveCollection(user => user.Roles)
            .HasColumnName("Roles")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        builder.Property(token => token.UserId).IsRequired();

        // SHA-256 Base64 = 44 karakter.
        builder.Property(token => token.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        // Her yenileme isteği bu sütunla arama yapıyor -> indeks şart.
        // Unique: aynı hash iki kez kaydedilemez.
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // "Bu kullanıcının tüm jetonlarını iptal et" sorgusu için.
        builder.HasIndex(token => token.UserId);

        builder.Property(token => token.CreatedAtUtc).IsRequired();
        builder.Property(token => token.ExpiresAtUtc).IsRequired();
        builder.Property(token => token.RevokedAtUtc);

        builder.Property(token => token.ReplacedByTokenHash).HasMaxLength(64);
    }
}
