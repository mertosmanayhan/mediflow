namespace MediFlow.Api.IntegrationTests;

/// <summary>
/// Tüm integration testlerinin PAYLAŞTIĞI tek uygulama örneği.
/// </summary>
/// <remarks>
/// NEDEN TEK ÖRNEK?
///
/// Önce her test sınıfına ayrı bir fabrika (IClassFixture) verdim ve
/// testler çöktü:
///
///     System.InvalidOperationException: The logger is already frozen.
///
/// Sebebi: her fabrika uygulamayı baştan ayağa kaldırıyor, yani
/// Program.cs'teki top-level kod yeniden çalışıyor. Serilog'un bootstrap
/// logger'ı ikinci kez kurulamıyor - normal bir uygulamada o kod ömür
/// boyunca BİR kez çalıştığı için bu bir sorun değil, ama testte birden
/// fazla host oluşturmak o varsayımı bozuyor.
///
/// ICollectionFixture tüm sınıflar için TEK örnek oluşturuyor:
///   - Program bir kez çalışıyor, hata ortadan kalkıyor
///   - Uygulama bir kez ayağa kalkıyor, testler belirgin şekilde hızlanıyor
///
/// BEDELİ: aynı koleksiyondaki testler PARALEL koşmaz (xUnit koleksiyon
/// içini sıralı çalıştırır) ve durum paylaşırlar. Bu yüzden testler
/// birbirinden bağımsız olacak şekilde yazıldı: her biri kendi kaydını
/// oluşturuyor, tekil e-posta kullanıyor ve başkasının verisine bakmıyor.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class ApiTestSuite : ICollectionFixture<MediFlowApiFactory>
{
    public const string Name = "MediFlow API";
}
