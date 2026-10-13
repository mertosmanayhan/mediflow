using MediFlow.BuildingBlocks.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MediFlow.Api.Authorization;

/// <summary>Belirli bir iznin varlığını şart koşan yetkilendirme gereksinimi.</summary>
internal sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>
/// <see cref="PermissionRequirement"/>'ı jetondaki izin iddialarına bakarak
/// karara bağlar.
/// </summary>
internal sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        bool hasPermission = context.User.Claims.Any(claim =>
            claim.Type == Permissions.ClaimType &&
            claim.Value == requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        // DİKKAT: context.Fail() ÇAĞIRMIYORUZ, sadece Succeed etmiyoruz.
        //
        // Fark önemli: Fail() kararı KESİNLEŞTİRİR - başka bir handler
        // sonradan Succeed etse bile istek reddedilir. Succeed etmemek ise
        // "ben bu gereksinimi karşılayamadım" demek; aynı gereksinim için
        // birden fazla handler varsa (ör. ileride kaynak bazlı kontrol)
        // diğerine şans verir.
        //
        // Fail() yalnızca "bu istek KESİNLİKLE reddedilmeli" dediğinde
        // kullanılır (ör. hesap askıya alınmış).
        return Task.CompletedTask;
    }
}

/// <summary>
/// İzin politikalarını İHTİYAÇ ANINDA üretir.
/// </summary>
/// <remarks>
/// NEDEN GEREKLİ?
/// ASP.NET Core yetkilendirme politikalarının başlangıçta kayıtlı olmasını
/// bekler: AddAuthorization(o => o.AddPolicy("patients:write", ...)).
/// 50 iznimiz olsa 50 satır kayıt yazmak ve her yeni izinde bir satır daha
/// eklemeyi hatırlamak gerekirdi - unutulmaya açık bir tekrar.
///
/// Bu sağlayıcı, adı tanımadığı bir politika istendiğinde onu ANINDA
/// üretiyor. Yeni izin eklemek için sadece Permissions sınıfına bir sabit
/// yazmak yeterli; kayıt kodu yok.
///
/// Tanımadığı adlarda (ör. çerçevenin varsayılan politikaları) temel
/// sınıfa devrediyor - kendi işini yapıp gerisine karışmıyor.
/// </remarks>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    internal const string PolicyPrefix = "perm:";

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
        {
            return base.GetPolicyAsync(policyName);
        }

        string permission = policyName[PolicyPrefix.Length..];

        AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

/// <summary>Endpoint ve grupları izne bağlayan kısayollar.</summary>
internal static class PermissionEndpointExtensions
{
    /// <summary>
    /// Bu endpoint'in çağrılabilmesi için gereken izni belirtir.
    /// </summary>
    /// <remarks>
    /// Okunuşu niyeti doğrudan anlatıyor:
    ///     .RequirePermission(Permissions.PatientsWrite)
    ///
    /// Endpoint hangi rollerin bu izne sahip olduğunu BİLMİYOR - o bilgi
    /// RolePermissions'da. Rol tanımı değişince burası etkilenmiyor.
    /// </remarks>
    internal static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization($"{PermissionPolicyProvider.PolicyPrefix}{permission}");
}
