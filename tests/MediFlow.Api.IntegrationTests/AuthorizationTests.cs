using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MediFlow.Api.IntegrationTests;

/// <summary>
/// Yetkilendirmenin uçtan uca doğru çalıştığını kanıtlar.
/// </summary>
[Collection(ApiTestSuite.Name)]
public sealed class AuthorizationTests(MediFlowApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

    private static string UniqueEmail() => $"user{Guid.NewGuid():N}@mediflow.test";

    private static object ValidPatient() =>
        new { firstName = "Integration", lastName = "Test", birthDate = "1990-01-01" };

    private sealed record Tokens(string AccessToken, string RefreshToken);

    private static async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/auth/login", new
        {
            email = MediFlowApiFactory.AdminEmail,
            password = MediFlowApiFactory.AdminPassword,
        });

        response.EnsureSuccessStatusCode();

        Tokens tokens = (await response.Content.ReadFromJsonAsync<Tokens>(Json))!;
        return tokens.AccessToken;
    }

    private static async Task<string> RegisterAndLoginAsPatientAsync(HttpClient client)
    {
        string email = UniqueEmail();
        const string Password = "HastaSifresi2026!";

        HttpResponseMessage registration = await client.PostAsJsonAsync(
            "/auth/register", new { email, password = Password });

        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        HttpResponseMessage login = await client.PostAsJsonAsync(
            "/auth/login", new { email, password = Password });

        login.EnsureSuccessStatusCode();

        Tokens tokens = (await login.Content.ReadFromJsonAsync<Tokens>(Json))!;
        return tokens.AccessToken;
    }

    [Fact]
    public async Task Patients_Should_Reject_AnonymousRequests()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/patients", ValidPatient());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Patients_Should_Reject_MalformedToken()
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not.a.real.token");

        HttpResponseMessage response = await client.PostAsJsonAsync("/patients", ValidPatient());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// İzin bazlı yetkilendirmenin ASIL testi.
    /// </summary>
    /// <remarks>
    /// Kendi kaydolan kullanıcı "Patient" rolü alıyor; bu rol
    /// patients:read taşıyor ama patients:write TAŞIMIYOR.
    ///
    /// 401 ile 403 arasındaki fark burada görünür hale geliyor:
    ///   401 = kimliğini bilmiyorum
    ///   403 = kimliğini biliyorum ama bu işlem için yetkin yok
    /// Bu ayrım olmasa istemci "giriş yap" ile "yetkin yok" durumlarını
    /// ayırt edemezdi.
    /// </remarks>
    [Fact]
    public async Task PatientRole_Should_Read_ButNotWrite()
    {
        HttpClient client = factory.CreateClient();
        string token = await RegisterAndLoginAsPatientAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Yazma izni YOK -> 403, 401 değil.
        HttpResponseMessage write = await client.PostAsJsonAsync("/patients", ValidPatient());
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);

        // Okuma izni VAR -> yetki geçiyor, kayıt bulunamadığı için 404.
        HttpResponseMessage read = await client.GetAsync($"/patients/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task Administrator_Should_BeAbleToWrite()
    {
        HttpClient client = factory.CreateClient();
        string token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.PostAsJsonAsync("/patients", ValidPatient());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
