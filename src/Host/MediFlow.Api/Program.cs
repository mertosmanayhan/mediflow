using System.Globalization;
using System.Text;
using FluentValidation;
using MediFlow.Api.Endpoints;
using MediFlow.Api.Observability;
using MediFlow.BuildingBlocks.Application.Behaviors;
using MediFlow.Identity.Application.Authentication;
using MediFlow.Identity.Infrastructure;
using MediFlow.PatientProfile.Application.Patients.CreatePatient;
using MediFlow.PatientProfile.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

// Serilog'un kendi hatalarını (sink'e bağlanamadı, yapılandırma çözülemedi)
// görünür kıl. Sessiz yutulan loglama hatası, "loglarım neden boş?"
// sorusunu cevaplanamaz hale getirir.
Serilog.Debugging.SelfLog.Enable(Console.Error);

// InvariantCulture şart: Türkçe kültürde ondalık ayırıcı virgül olur
// ("13,86 ms") ve log analiz araçları sayıyı okuyamaz.
// Bootstrap logger: asıl logger yapılandırma okunurken kurulur; o aşamada
// çıkan hatalar (eksik bağlantı dizesi gibi) aksi halde hiçbir yere yazılmaz.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ========================================================
    //  MODÜLLER — her biri kendi DI kaydını yapıyor
    // ========================================================
    builder.Services.AddPatientProfileModule(builder.Configuration);
    builder.Services.AddIdentityModule(builder.Configuration);

    builder.Services.AddSingleton(TimeProvider.System);

    // ========================================================
    //  MEDIATOR VE PIPELINE — iki modülün handler'ları
    // ========================================================
    builder.Services.AddMediatR(configuration =>
    {
        configuration.RegisterServicesFromAssembly(typeof(CreatePatientCommand).Assembly);
        configuration.RegisterServicesFromAssembly(typeof(LoginCommand).Assembly);

        // SIRA: Logging dışta, Validation içte. Ters olsa doğrulamadan
        // reddedilen istekler loga hiç düşmezdi.
        configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
        configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
    });

    builder.Services.AddValidatorsFromAssembly(
        typeof(CreatePatientCommand).Assembly, includeInternalTypes: true);
    builder.Services.AddValidatorsFromAssembly(
        typeof(LoginCommand).Assembly, includeInternalTypes: true);

    // ========================================================
    //  KİMLİK DOĞRULAMA — gelen JWT'yi doğrula
    // ========================================================
    JwtOptions jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException("Jwt bölümü yapılandırılmamış.");

    if (string.IsNullOrWhiteSpace(jwt.SigningKey))
    {
        throw new InvalidOperationException(
            "Jwt:SigningKey tanımlı değil. " +
            "dotnet user-secrets set \"Jwt:SigningKey\" \"<en az 32 karakter>\" " +
            "--project src/Host/MediFlow.Api");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
            // HER DOĞRULAMA AÇIK, bilerek. Kapatılan her kontrol bir açıktır.
            options.TokenValidationParameters = new TokenValidationParameters
            {
                // Jetonu BİZ mi ürettik? Başka servisin jetonu kabul edilmesin.
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,

                // Jeton BİZE mi verilmiş? Başka API için üretilmiş jeton
                // burada geçmesin.
                ValidateAudience = true,
                ValidAudience = jwt.Audience,

                // İmza geçerli mi? Kapatılırsa herkes kendi jetonunu yazıp
                // Admin olabilir.
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwt.SigningKey)),

                ValidateLifetime = true,

                // VARSAYILAN 5 DAKİKA - yani 15 dakikalık jeton fiilen 20
                // dakika yaşar. Saat kaymasına küçük tolerans yeterli.
                ClockSkew = TimeSpan.FromSeconds(30),
            });

    builder.Services.AddAuthorization();

    // ========================================================
    //  HTTP
    // ========================================================
    builder.Services.AddOpenApi();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    // ---- Middleware sırası ----
    app.UseExceptionHandler();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options =>
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            diagnosticContext.Set(
                CorrelationIdMiddleware.ItemKey,
                httpContext.Items[CorrelationIdMiddleware.ItemKey]));

    // SIRA ZORUNLU: önce "kimsin" (authentication), sonra "yetkin var mı"
    // (authorization). Kimliği bilinmeyenin yetkisi sorgulanamaz.
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapGet("/", () => Results.Redirect("/scalar"))
       .ExcludeFromDescription();

    app.MapAuthEndpoints();
    app.MapPatientEndpoints();

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Application terminated unexpectedly during startup");
    throw;
}
finally
{
    // Serilog toplu gönderim yapıyor; bu çağrı olmadan kapanış anındaki
    // son loglar Seq'e hiç ulaşmaz.
    Log.CloseAndFlush();
}
