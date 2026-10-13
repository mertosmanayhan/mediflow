using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MediFlow.PatientProfile.Domain;
using MediFlow.PatientProfile.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediFlow.Api.IntegrationTests;

/// <summary>
/// Hasta kaydının tüm yaşam döngüsü: oluştur, oku, sil.
/// Faz 1'in bitmiş sayılma şartını (DoD) doğrular.
/// </summary>
[Collection(ApiTestSuite.Name)]
public sealed class PatientLifecycleTests(MediFlowApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Tokens(string AccessToken, string RefreshToken);

    private sealed record CreatedResource(Guid Id);

    private sealed record PatientResponse(
        Guid Id,
        string FirstName,
        string LastName,
        DateOnly BirthDate,
        IReadOnlyList<AllergyResponse> Allergies);

    private sealed record AllergyResponse(string Name, string Severity);

    /// <summary>
    /// Durum kodunu doğrular; eşleşmezse CEVAP GÖVDESİNİ de hata mesajına
    /// koyar.
    /// </summary>
    /// <remarks>
    /// Çıplak Assert.Equal(Created, response.StatusCode) başarısız olduğunda
    /// sadece "Values differ" der - sunucunun ne dediğini göstermez ve
    /// teşhis için testi tekrar tekrar çalıştırmak gerekir. Entegrasyon
    /// testlerinde hata mesajının kendi kendine yeterli olması, kazanılan
    /// zamanın büyük kısmını oluşturuyor.
    /// </remarks>
    private static async Task AssertStatusAsync(
        HttpStatusCode expected,
        HttpResponseMessage response)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync();

        Assert.Fail(
            $"Beklenen {(int)expected} {expected}, gelen {(int)response.StatusCode} " +
            $"{response.StatusCode}. Govde: {(string.IsNullOrWhiteSpace(body) ? "(bos)" : body)}");
    }
    private async Task<HttpClient> AuthenticatedAdminClientAsync()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new
        {
            email = MediFlowApiFactory.AdminEmail,
            password = MediFlowApiFactory.AdminPassword,
        });

        login.EnsureSuccessStatusCode();

        Tokens tokens = (await login.Content.ReadFromJsonAsync<Tokens>(Json))!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return client;
    }

    /// <summary>
    /// FAZ 1 DoD: kayıt → giriş → jetonla hasta oluştur → geri oku.
    /// </summary>
    [Fact]
    public async Task FullFlow_Should_CreateAndReadBackAPatient()
    {
        HttpClient client = await AuthenticatedAdminClientAsync();

        HttpResponseMessage created = await client.PostAsJsonAsync("/patients", new
        {
            firstName = "Zeynep",
            lastName = "Aydın",
            birthDate = "1992-07-08",
        });

        await AssertStatusAsync(HttpStatusCode.Created, created);

        // 201 Created, kaynağın adresini Location başlığında vermeli.
        Assert.NotNull(created.Headers.Location);

        CreatedResource resource = (await created.Content.ReadFromJsonAsync<CreatedResource>(Json))!;

        HttpResponseMessage read = await client.GetAsync($"/patients/{resource.Id}");
        await AssertStatusAsync(HttpStatusCode.OK, read);

        PatientResponse patient = (await read.Content.ReadFromJsonAsync<PatientResponse>(Json))!;

        Assert.Equal(resource.Id, patient.Id);
        // Türkçe karakter uçtan uca korunuyor (ADR-0003 collation kararı).
        Assert.Equal("Aydın", patient.LastName);
        Assert.Empty(patient.Allergies);
    }

    [Fact]
    public async Task Create_Should_ReportEveryValidationFailureAtOnce()
    {
        HttpClient client = await AuthenticatedAdminClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/patients", new
        {
            firstName = "",
            lastName = "",
            birthDate = "0001-01-01",
        });

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Validation.Failed",
            problem.RootElement.GetProperty("code").GetString());

        // Üç kural birden ihlal edildi ve ÜÇÜ BİRDEN döndü.
        // Domain ilk hatada durur; pipeline'daki doğrulayıcı durmaz.
        Assert.Equal(3, problem.RootElement.GetProperty("errors").GetArrayLength());
    }

    /// <summary>
    /// Soft delete'in GERÇEKTEN soft olduğunu kanıtlar.
    /// </summary>
    /// <remarks>
    /// Bu test birim testle yazılamaz: soft delete bir EF interceptor'ı ve
    /// global query filter ile çalışıyor, yani gerçek bir DbContext
    /// gerektiriyor. Sahte depo ile yazılsa sahtenin kendisi test edilmiş
    /// olurdu - test piramidinin hangi katmanında olduğumuzun önemi bu.
    /// </remarks>
    [Fact]
    public async Task Delete_Should_HideTheRecord_ButKeepTheRow()
    {
        HttpClient client = await AuthenticatedAdminClientAsync();

        CreatedResource resource = (await (await client.PostAsJsonAsync("/patients", new
        {
            firstName = "Silinecek",
            lastName = "Kayıt",
            birthDate = "1980-03-03",
        })).Content.ReadFromJsonAsync<CreatedResource>(Json))!;

        HttpResponseMessage deleted = await client.DeleteAsync($"/patients/{resource.Id}");
        await AssertStatusAsync(HttpStatusCode.NoContent, deleted);

        // API artık görmüyor: global query filter devrede.
        HttpResponseMessage afterDelete = await client.GetAsync($"/patients/{resource.Id}");
        await AssertStatusAsync(HttpStatusCode.NotFound, afterDelete);

        // İkinci silme de 404 - istemci açısından kayıt yok.
        HttpResponseMessage secondDelete = await client.DeleteAsync($"/patients/{resource.Id}");
        await AssertStatusAsync(HttpStatusCode.NotFound, secondDelete);

        // AMA SATIR DURUYOR. IgnoreQueryFilters ile filtreyi aşıp bakıyoruz.
        using IServiceScope scope = factory.Services.CreateScope();
        PatientProfileDbContext context =
            scope.ServiceProvider.GetRequiredService<PatientProfileDbContext>();

        Patient? row = await context.Patients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(patient => patient.Id == resource.Id);

        Assert.NotNull(row);
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAtUtc);
    }

    /// <summary>
    /// Audit alanlarının interceptor tarafından doldurulduğunu kanıtlar.
    /// </summary>
    [Fact]
    public async Task Create_Should_StampAuditFields_WithoutAnyHandlerCode()
    {
        HttpClient client = await AuthenticatedAdminClientAsync();

        CreatedResource resource = (await (await client.PostAsJsonAsync("/patients", new
        {
            firstName = "Audit",
            lastName = "Kontrol",
            birthDate = "1975-11-11",
        })).Content.ReadFromJsonAsync<CreatedResource>(Json))!;

        using IServiceScope scope = factory.Services.CreateScope();
        PatientProfileDbContext context =
            scope.ServiceProvider.GetRequiredService<PatientProfileDbContext>();

        Patient row = await context.Patients.FirstAsync(patient => patient.Id == resource.Id);

        // Hiçbir handler CreatedAtUtc yazmıyor - interceptor yazdı.
        Assert.NotEqual(default, row.CreatedAtUtc);

        // Henüz güncellenmediği için null kalmalı: interceptor "Added" ve
        // "Modified" durumlarını ayırt ediyor.
        Assert.Null(row.UpdatedAtUtc);
    }
}
