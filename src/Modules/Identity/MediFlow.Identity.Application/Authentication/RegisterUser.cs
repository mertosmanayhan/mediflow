using FluentValidation;
using MediatR;
using MediFlow.BuildingBlocks.Application;
using MediFlow.BuildingBlocks.Common;
using MediFlow.Identity.Domain;

namespace MediFlow.Identity.Application.Authentication;

public sealed record RegisterUserCommand(string Email, string Password) : ICommand<Guid>;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    /// <summary>En kısa şifre uzunluğu.</summary>
    /// <remarks>
    /// NIST'in güncel tavsiyesi: uzunluğa odaklan, karmaşıklık kurallarına
    /// (bir büyük harf, bir rakam, bir sembol) odaklanma. Karmaşıklık
    /// kuralları kullanıcıyı "Ankara1!" gibi tahmin edilebilir kalıplara ve
    /// şifreyi bir yere yazmaya itiyor. Uzunluk ise entropiyi gerçekten
    /// artırıyor.
    /// </remarks>
    public const int MinPasswordLength = 12;

    public RegisterUserCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithErrorCode("Identity.EmailEmpty")
            .EmailAddress().WithErrorCode("Identity.EmailInvalid")
            .MaximumLength(Email.MaxLength).WithErrorCode("Identity.EmailTooLong");

        RuleFor(command => command.Password)
            .NotEmpty().WithErrorCode("Identity.PasswordEmpty")
            .MinimumLength(MinPasswordLength).WithErrorCode("Identity.PasswordTooShort")
            // BCrypt girdiyi 72 bayta kırpar; daha uzunu sessizce yok sayılır.
            // Sessiz davranış yerine açık hata veriyoruz.
            .MaximumLength(72).WithErrorCode("Identity.PasswordTooLong");
    }
}

internal sealed class RegisterUserCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    TimeProvider clock) : IRequestHandler<RegisterUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        Result<Email> email = Email.Create(command.Email);

        if (email.IsFailure)
        {
            return Result.Failure<Guid>(email.Error);
        }

        // "Zaten kayıtlı mı" kontrolü BURADA, Domain'de değil.
        // Sebebi: bu bir TEKİLLİK kuralı ve tekillik tek bir nesneye
        // bakarak doğrulanamaz - diğer tüm kullanıcılara bakmak gerekir.
        // Aggregate kendi içini korur, küme genelindeki kuralları koruyamaz.
        //
        // NOT: Bu kontrol ile kaydın arasında yarış durumu (race condition)
        // vardır - iki istek aynı anda gelirse ikisi de "yok" görür.
        // Gerçek garanti veritabanındaki UNIQUE indeks; bu kontrol sadece
        // kullanıcıya anlaşılır hata vermek için. Faz 2'de eşzamanlılık
        // konusunu bu örnekle derinleştireceğiz.
        if (await users.ExistsWithEmailAsync(email.Value, cancellationToken))
        {
            return Result.Failure<Guid>(IdentityErrors.EmailAlreadyRegistered);
        }

        string passwordHash = passwordHasher.Hash(command.Password);

        Result<User> user = User.Create(
            email.Value,
            passwordHash,
            clock.GetUtcNow(),
            roles: ["Patient"]);

        if (user.IsFailure)
        {
            return Result.Failure<Guid>(user.Error);
        }

        await users.AddAsync(user.Value, cancellationToken);

        return Result.Success(user.Value.Id);
    }
}
