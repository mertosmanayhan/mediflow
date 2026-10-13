# Faz 1 — Modüler Monolit: Identity + PatientProfile

- **Süre:** 2026-09-30 → 2026-10-13
- **PR'lar:** #18 (domain), #20 (CQRS + pipeline), #22 (EF Core + endpoint'ler),
  #24 (Serilog + exception handler), #25 (JWT auth), #26 (yetkilendirme +
  soft delete + entegrasyon testleri)
- **ADR'ler:** [0004](../adr/0004-permission-based-authorization.md),
  [0005](../adr/0005-soft-delete-and-auditing.md)
- **Sonuç:** 64 test yeşil, CI-modu derleme 0 uyarı

---

## Ne yapıldı

Faz 0 altyapıyı kurmuştu; Faz 1'de ilk gerçek iş mantığı geldi. İki modül:

**`Identity`** — kayıt, giriş, jeton yenileme. **`PatientProfile`** — hasta
kaydı oluştur, oku, sil.

İkisi de aynı iskeleti paylaşıyor: `Domain` (iş kuralları, hiçbir şeye bağımlı
değil) → `Application` (komut/sorgu ve handler'lar) → `Infrastructure` (EF Core,
BCrypt, JWT). Bağımlılık okları hep içe doğru: `Infrastructure`, `Domain`'deki
arayüzü uyguluyor, `Domain` `Infrastructure`'ı hiç tanımıyor.

Ortak kodun ikinci kullanıcısı çıktığında `BuildingBlocks`'a taşındı — önce
değil. `Result`/`Error` Faz 0'da ortaktı; `ICommand`/`IQuery`/pipeline
behavior'ları ise `PatientProfile.Application` içinde doğdu ve `Identity`
ikinci kullanıcı olunca `BuildingBlocks.Application`'a çıktı.

---

## Nerede tıkandık

Bu bölüm bu dosyanın asıl değeri. Dokuz gerçek olay:

### 1. `init`-only property sessizce bağlanmıyor

`JwtOptions`'ın property'leri `{ get; init; }` yazılmıştı. `services.Configure<JwtOptions>(section)`
çağrısı **hata vermedi**, sadece hiçbir değeri set etmedi. Belirti: jeton
üretimi "signing key boş" diye patladı, ama yapılandırmada anahtar vardı.

**Sebep:** Options binder property'leri `set` ile yazıyor; `init` yalnızca
nesne başlatıcıda çalışır. Binder bir property'yi set edemediğinde **sessizce
geçiyor**.

**Ders:** Options sınıflarında `{ get; set; }`. Ve her options için
`.Validate(...).ValidateOnStart()` — eksik yapılandırma açılışta patlamalı,
ilk kullanımda değil.

### 2. Content root ≠ çıktı klasörü — ve bu Serilog'u sessizce kapatmış

`dotnet src/Host/MediFlow.Api/bin/Debug/net10.0/MediFlow.Api.dll` komutunu repo
kökünden çalıştırdık. Uygulama ayağa kalktı, endpoint'ler çalıştı. Ama
`appsettings.json` **hiç okunmadı**: content root, çalışma dizini (repo kökü)
oldu, dosya ise çıktı klasöründeydi.

İki sonuç: (a) `Jwt:Issuer`/`Audience` boş kaldı → her istek 401, (b) Serilog
yapılandırması okunmadı → **Blok 2 boyunca Seq'e hiçbir şey ulaşmamış**. İkinci
sorunu hiç fark etmemiştik, çünkü eksik log kendini göstermiyor.

**Ders:** Eksik yapılandırma "varsayılanla devam et" değil, "patla" olmalı.
`ValidateOnStart` tam bunun için var. Ve loglama altyapısının *çalıştığını*
bir kez gözle doğrulamak gerekiyor — sessizliği başarı sanmak kolay.

### 3. 8000 karakterlik stack trace istemciye gidiyordu

Global exception handler yazmadan önce bir hata fırlattık: cevap gövdesi
~8000 karakter, iç sınıf adları, dosya yolları, satır numaraları. Bu bir
**bilgi sızıntısı**: saldırgan hangi kütüphaneleri, hangi sürümleri ve kod
yapısını öğreniyor.

`IExceptionHandler` + `ProblemDetails` ile aynı cevap **179 karaktere** indi;
detay loga gidiyor, istemciye gitmiyor.

**Ders:** Hata cevabı iki ayrı izleyici kitlesine yazılır — istemci "ne
yapmalıyım"ı, log "ne oldu"yu görür. İkisini karıştırmak güvenlik açığı.

### 4. "The logger is already frozen"

İki entegrasyon test sınıfı `IClassFixture<MediFlowApiFactory>` kullanıyordu.
xUnit her sınıf için fixture'ı **yeniden** kuruyor → iki ayrı host →
`CreateBootstrapLogger()` iki kez → Serilog'un global `Log.Logger`'ı ikinci
çağrıda donmuş olduğu için patlıyor.

Çözüm: `ICollectionFixture` + `[Collection]` — tüm sınıflar **tek** host
paylaşıyor.

**Ders:** `Program.cs`'teki global durum (`Log.Logger`, `SelfLog`) uygulamanın
süreçte **bir kez** ayağa kalktığı varsayımına dayanıyor. Testler bu varsayımı
bozan ilk yer.

### 5. Her kimlik doğrulaması 401 — eager vs lazy yapılandırma

Entegrasyon testleri yazıldıktan sonra 8 testin 6'sı 401 aldı. Jeton
*üretiliyordu*, giriş 200 dönüyordu, ama o jetonla yapılan istek reddediliyordu.

**Sebep:** Jeton doğrulama ayarları `Program.cs`'te **hemen** okunuyordu:

```csharp
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>();  // o anki değer
```

Jeton **üretimi** ise `IOptions<JwtOptions>` ile **sonradan** okuyordu.
Normalde ikisi aynı sonucu verir. Ama `WebApplicationFactory` yapılandırmayı
Program'ın bu satırından **sonra** eziyor:

- üretim (tembel) → test anahtarını gördü
- doğrulama (hemen) → gerçek anahtarı gördü

Yani jeton bir anahtarla imzalanıp başka anahtarla doğrulanıyordu. Belirti
("401") ile sebep ("iki okuma farklı zamanda yapıldı") arasında hiçbir görünür
bağ yoktu.

Çözüm: `JwtBearerOptionsSetup : IConfigureNamedOptions<JwtBearerOptions>` —
doğrulama ayarları da `IOptions` üzerinden, yani tembel okunuyor.

**Ders:** Yapılandırmayı hemen okumak değeri **o an** donduruyor. Aynı ayarı
iki farklı zamanda okuyan iki bileşen, farklı değer görebilir. Ve bu hatayı
**birim test asla yakalayamazdı** — iki bileşeni ayrı ayrı doğrulayan test,
ikisinin farklı değer gördüğünü göremez. Entegrasyon testinin varlık sebebi
tam olarak bu.

### 6. Aynı hata ikinci kez: testler GELİŞTİRME veritabanında koşuyormuş

5. olaydaki eager/lazy hatasını düzelttim, testler yeşil oldu, PR açtım.
**CI kırmızı döndü.** Yerelde geçen testler CI'da `Test` adımında patladı.

İlk şüphem SQL Server service container'ıydı; `sqlcmd` yolunu ve healthcheck'i
doğruladım, sorun orada değildi. Sonra bağlantı dizelerinin nerede durduğunu
kontrol ettim: **user-secrets'ta.** CI'da user-secrets yoktur,
`appsettings.json`'da da yoktu → `GetConnectionString` null → modül kaydı
patlıyor.

Ama veritabanı listesine bakınca çok daha kötüsü çıktı:

```
MediFlow.PatientProfile
MediFlow.Identity
```

`MediFlow.PatientProfile.Tests` ve `MediFlow.Identity.Tests` **hiç
oluşturulmamış**. Yani entegrasyon testleri, test fabrikasının verdiği
bağlantı dizesini hiç kullanmamış — doğrudan **geliştirme veritabanına**
yazıyorlardı. Testler yeşildi, ama yanlış veritabanında.

**Sebep 5. olayın birebir aynısı, ikinci bir yerde:** modüller bağlantı
dizesini DI kaydı anında, yani `builder.Configuration`'dan **hemen**
okuyordu. `WebApplicationFactory` yapılandırmayı bu satırdan *sonra* eziyor.

**Çözüm iki parçalı.** Bağlantı dizesi artık `AddDbContext` lambda'sının
içinde, servis sağlayıcıdan `IConfiguration` çözülerek okunuyor — o lambda
DbContext ilk istendiğinde çalışıyor, yapılandırma o anda kesinleşmiş
durumda. Ve modül metotları artık `IConfiguration` parametresi **almıyor**:

```csharp
services.AddPatientProfileModule();   // önce (builder.Configuration) alıyordu
```

**Ders — ve bu fazın en değerli dersi bu:** aynı hatayı iki ayrı yerde
yaptım. İlkini düzeltirken "tamam, anladım" dedim ve *deseni* aramadım.
Bir hatayı düzeltmek yetmiyor; **aynı hatanın başka nerede olduğunu**
aramak gerekiyor.

İkinci ders: düzeltmeyi *imkânsız kılmak* > hatırlamaya çalışmak. Metot
`IConfiguration` almadığı sürece onu çok erken okuyamam. Bu yüzden
parametreyi kaldırdım, "dikkat et" yorumu yazmakla yetinmedim.

Üçüncü ders: **yeşil test, doğru test demek değil.** Testler geçiyordu
çünkü geliştirme veritabanı da aynı şemaya sahipti. Testin *ne üzerinde*
koştuğunu doğrulamak, sonucuna bakmak kadar önemli. Düzeltmenin kanıtı
testlerin yeşil olması değil, `sys.databases`'te `.Tests` veritabanlarının
görünmesiydi.

### 7. İşletim hatası 400 dönüyordu, 401 değil

Yanlış şifreyle giriş denemesi `400 Bad Request` dönüyordu. Yanlış: 400
"isteğin biçimi bozuk" demek; burada biçim doğru, **kimlik** doğrulanamadı.

`ErrorMapping`'de `Identity.InvalidCredentials` → 401, `*Already*` içeren
kodlar → 409 yapıldı.

**Ders:** HTTP durum kodu bir sözleşme. İstemci 400'ü "isteğimi düzelt",
401'i "giriş yap" diye yorumluyor; yanlış kod istemciyi yanlış davranışa iter.

### 8. `PrimitiveCollection` olmadan `User.Roles` migration'ı patlıyor

`Navigation 'User.Roles' was not found` hatası. `Roles` bir `List<string>`
ve arkasında field var; EF onu navigation sanıp ilişkili entity aradı.

Çözüm: `PrimitiveCollection(u => u.Roles).UsePropertyAccessMode(PropertyAccessMode.Field)`.

**Ders:** Zengin domain modelinde (`private set`, backing field, `AsReadOnly()`)
EF'in varsayılan keşfi yetmiyor; eşlemeyi açıkça söylemek gerekiyor. Anemic
modelde bu sorun hiç çıkmaz — zengin modelin bedeli bu.

### 9. İfade içine yazılan yorum `dotnet format`'ı kırıyor

Bu faz boyunca ~5 kez tekrarlandı. Ternary'nin ortasına, metot zincirinin
arasına veya expression-bodied member'ın `=>`'sinden sonra yazılan yorum,
`dotnet format --verify-no-changes`'i `error WHITESPACE` ile düşürüyor.

**Kural:** Yorumlar **ifadenin üstüne** yazılır, içine değil.

---

## Ne öğrendim

| Konu | Kavradığım şey |
|---|---|
| Clean Architecture | Katman sayısı değil, **bağımlılık yönü** önemli. `Domain` hiçbir şeyi tanımıyorsa iş kuralı test edilebilir. |
| CQRS | Okuma ve yazma **ayrı modellenebilir**. Faydası şimdi az; asıl faydası okuma ve yazmanın farklı ölçeklendiği yerde. |
| Pipeline behavior | Decorator zinciri. Sıra anlamlı: Logging dışta (reddedilen istek de loglanır), Validation ortada, UnitOfWork içte. |
| Result pattern | Beklenen başarısızlık (yanlış şifre) **akış**; beklenmeyen (bağlantı koptu) **exception**. Exception'ın maliyeti ölçüldü. |
| İki katmanlı doğrulama | FluentValidation **biçimi** (boş mu, uzun mu), domain **kuralı** (doğum tarihi gelecekte olamaz) doğruluyor. Domain ilk hatada durur, pipeline hepsini toplar. |
| JWT | **İmzalı, şifreli değil** — payload herkes tarafından okunabilir. Jetona hassas veri konmaz. `ClockSkew` varsayılanı 5 dk: 15 dakikalık jeton fiilen 20 dk yaşar. |
| BCrypt vs SHA-256 | Şifre **düşük entropili** → yavaş + tuzlu hash şart. Refresh token **yüksek entropili rastgele** → SHA-256 yeterli, BCrypt gereksiz yavaşlık. Seçim entropiye göre yapılıyor. |
| Refresh token rotation | Yenileme sırasında eski jeton iptal edilir. Aynı jeton **ikinci kez** gelirse bu bir **saldırı sinyalidir** → kullanıcının tüm zinciri iptal edilir. |
| Kullanıcı sızdırmama | "E-posta yok" ile "şifre yanlış" **aynı** cevabı vermeli, yoksa kayıtlı e-postalar numaralanabilir. Zamanlama da eşitlenmeli (sahte hash) — yoksa süre farkı aynı bilgiyi verir. |
| İzin bazlı yetki | Endpoint **izin** istiyor, rol değil. Rol → izin eşlemesi tek yerde; yeni rol endpoint'lere dokunmadan eklenir. 401 ≠ 403. |
| Interceptor + query filter | Doğru davranışı **varsayılan** yapmak, her handler'da hatırlamaktan güçlü. Unutulabilen kural, kural değil. |
| Soft delete bedeli | Unique indeks artık unique değil (filtrelenmiş indeks şart), ham SQL filtreyi atlar, veritabanı hiç küçülmez. Ve **KVKK silme hakkını karşılamıyor**. |
| Unit of Work | Bir istek = bir `SaveChanges`. Açık transaction açmıyoruz: `EnableRetryOnFailure` ile birlikte execution strategy tuzağına düşüyor. Cross-modül atomiklik yok — Faz 3'te Outbox. |
| Test piramidi | Birim test parçayı doğrular; **parçaların doğru bağlandığını kanıtlamaz**. DI kaydı, middleware sırası, options bağlama hepsi birim testten kaçar (5. olay bunun kanıtı). |
| Structured logging | Log satırı metin değil, **sorgulanabilir veri**. `[LoggerMessage]` kaynak üreticisi boxing ve string birleştirmeyi ortadan kaldırıyor. İş hatası `Warning`, sistem hatası `Error`. |

---

## Hâlâ net olmayanlar

- **Cross-modül atomiklik.** İki modül aynı istekte yazarsa biri başarılı
  diğeri başarısız olabilir. Outbox'ın bunu *nasıl* çözdüğünü henüz kavramadım
  — Faz 3'ün konusu.
- **C# tarafında Türkçe metin karşılaştırma.** ADR-0003 veritabanını çözdü ama
  `StringComparison.OrdinalIgnoreCase` Türkçe kurallarını uygulamıyor.
  `Patient.AddAllergy` içindeki tekrar kontrolü hâlâ bu açıkla çalışıyor.
- **Jeton iptali.** İzin bazlı yetkide izin geri alınsa bile jeton süresi
  bitene kadar geçerli. Redis kara listesi mantıklı görünüyor ama ne zaman
  gerçekten gerektiğini bilmiyorum.
- **CQRS'in okuma tarafı.** Şu an okuma da yazma da aynı EF modelini
  kullanıyor. "Ayrı okuma modeli" ne zaman gerçekten gerekir, karar eşiği ne?
- **Kapsama oranı.** Raporu CI'a ekledik ama eşik koymadık. Hangi oranın
  anlamlı olduğundan emin değilim — eşik koymanın anlamsız test yazmaya ittiği
  uyarısını okudum ama sınırı nerede çizeceğimi bilmiyorum.

---

## Sonraki faza taşınanlar

- `ChronicCondition` value object'i (Faz 2)
- Kaynak bazlı yetkilendirme: "bu doktor BU hastayı görebilir mi?" (Faz 2)
- Dapper / Specification pattern — ağır sorgu çıkınca (Faz 5)
- Testcontainers: entegrasyon testleri şu an lokaldeki SQL Server'a bağımlı
  (Faz 6)
- Kapsama eşiği (Faz 6)
- KVKK kalıcı silme / anonimleştirme akışı (ADR-0005'te açık iş)
