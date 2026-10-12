using MediFlow.BuildingBlocks.Common;

namespace MediFlow.Identity.Domain;

/// <summary>
/// E-posta adresi. Değer nesnesi (value object).
/// </summary>
/// <remarks>
/// NEDEN 'string' DEĞİL AYRI BİR TİP?
/// İmzalarda fark yaratıyor: <c>Login(Email email, string password)</c>
/// ile <c>Login(string a, string b)</c> arasındaki fark, ikincisinde
/// parametreleri yanlış sırada geçmenin derleyici tarafından
/// yakalanmamasıdır. Ayrıca normalleştirme (küçük harf, kırpma) tek bir
/// yerde yapılır; 10 ayrı yerde "ToLower()" çağırmayı unutma riski kalkar.
///
/// NORMALLEŞTİRME KRİTİK: "Ahmet@Mail.com" ve "ahmet@mail.com" AYNI
/// kullanıcıdır. Normalleştirmezsen aynı e-posta ile iki hesap açılır ve
/// "e-posta zaten kayıtlı" kontrolü işe yaramaz.
///
/// Biçim doğrulaması burada DEĞİL: "@ var mı" gibi şekil kontrolü girdi
/// doğrulamasıdır (FluentValidation). Burada sadece boş olmama ve
/// normalleştirme var - yani domain'in gerçekten bildiği şeyler.
/// </remarks>
public sealed record Email
{
    public const int MaxLength = 256;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Result<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<Email>(IdentityErrors.EmailEmpty);
        }

        string normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            return Result.Failure<Email>(IdentityErrors.EmailTooLong);
        }

        return Result.Success(new Email(normalized));
    }

    public override string ToString() => Value;
}
