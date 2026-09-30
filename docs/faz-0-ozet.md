# Faz 0 — Sade Özet

> Bu dosya, Faz 0'da yaptıklarımızı **hatırlaman** için yazıldı; öğretmek için
> değil (öğrenme sohbette oldu). Faz 1'e başlamadan önce bir kez baştan sona
> oku; 10 dakika sürer.

**Durum:** `main` → 7 commit, 27 dosya, 5 merge edilmiş PR, 10 test, 4 container.

---

## 1. Faz 0 neydi, neden ilk o?

**Tek cümle:** Kod yazmaya başlamadan önce, *"kodu nasıl yazacağız, nasıl
koruyacağız, nerede çalıştıracağız"* sorularının cevabını kurduk.

Neden önce bu? Çünkü bu üç şeyi sonradan eklemek çok daha pahalı:

| Sonradan eklemek | Neden pahalı |
|---|---|
| Git disiplini | 100 commit birikmiş, geçmiş dağınık, düzeltilemez |
| Kalite kapıları | 50 uyarı birikmiş, hiçbirini kimse okumaz |
| Docker | Makinene 3 farklı veritabanı kurmuşsun, temizlemek dert |

Faz 0'ın ürünü **kod değil, alışkanlık ve altyapıdır.** Kalan 15 hafta bunun
üzerine kurulacak.

---

## 2. Büyük resim: üç ayrı iş yaptık

```
┌─────────────────────────┐  ┌──────────────────────────┐  ┌────────────────────────┐
│  1. GIT & GITHUB        │  │  2. .NET DERLEME         │  │  3. DOCKER ALTYAPISI   │
│                         │  │                          │  │                        │
│  Kodu nasıl saklıyor,   │  │  Kod nasıl derlenip test  │  │  Kod neyin üstünde     │
│  nasıl koruyoruz?       │  │  ediliyor, kurallar ne?  │  │  çalışacak?            │
│                         │  │                          │  │                        │
│  branch → PR → CI →     │  │  solution, merkezi        │  │  MSSQL, Redis,         │
│  merge akışı            │  │  yapılandırma, analizör  │  │  RabbitMQ, Seq         │
└─────────────────────────┘  └──────────────────────────┘  └────────────────────────┘
```

Üçü birbirine bağlı — bölüm 7'de zincirleri göreceksin.

---

## 3. Klasör yapısı — neden böyle?

```
mediflow/
├── .github/            ← GitHub'ın otomasyon dosyaları (bu yol SABİT, değiştirilemez)
│   └── workflows/         CI tarifleri
├── docs/               ← insan için yazılanlar
│   ├── adr/               "bu kararı neden verdim" kayıtları
│   └── journal/           haftalık günlük (senin yazacağın)
├── src/                ← ÜRETİM kodu
│   ├── BuildingBlocks/    tüm servislerin paylaştığı TEKNİK altyapı
│   └── Services/          mikroservisler (Faz 1'de ilki gelecek)
├── tests/              ← TEST kodu (src'den ayrı)
├── deploy/             ← dağıtım dosyaları
│   └── docker/            üretim compose dosyaları (Faz 7)
└── (kök dosyalar)      ← tüm projeyi etkileyen yapılandırma
```

### Neden `src` ve `tests` ayrı?

İki sebep:

1. **Üretim imajına test kodu sızmaz.** Faz 7'de Docker imajı üretirken sadece
   `src` içine alacağız. Aynı klasörde olsalardı ayıklamak zor olurdu.
2. **Tek komutla hedeflenebilir.** `dotnet test` test projelerini bulur; CI'da
   ayrı adım yazabiliriz.

### Neden `BuildingBlocks` ayrı bir klasör?

Servislerin **paylaştığı** kod buraya gelir. Ama önemli bir kural var:

> **`BuildingBlocks` sadece TEKNİK şeyler içerir. İş kuralı ASLA girmez.**

Neden? Çünkü buradaki bir dosyayı değiştirdiğinde **9 servis** etkilenir. `Result`
tipi gibi teknik bir şey için bu kabul edilebilir. Ama `Patient` sınıfını buraya
koysan, hasta kurallarını değiştirmek Laboratory servisini de bozardı — yani
mikroservislerin bağımsızlığı kaybolurdu. Bu hataya **"dağıtık monolit"** deniyor.

### Neden `deploy/docker` boş ama var?

Faz 7'de üretim için ayrı bir compose dosyası gelecek. Şu an içinde `.gitkeep`
var — çünkü **Git klasör takip etmez, dosya takip eder.** Boş klasörü depoda
tutmanın tek yolu içine bir dosya koymak; `.gitkeep` bunun için kullanılan bir
gelenektir (Git'in özel bir kelimesi değil).

---

## 4. Kök dizindeki dosyalar — hangisi ne yapıyor?

Kökte 11 yapılandırma dosyası var. Hepsi **tüm projeyi** etkilediği için orada.

| Dosya | Tek cümleyle | Anahtar kavram |
|---|---|---|
| `.gitignore` | "Bu dosyaları Git takip etmesin" | Derleme çıktısı + IDE ayarı + **sırlar** |
| `.gitattributes` | Satır sonu (CRLF/LF) yönetimi | Windows'ta yaz, Linux'ta çalıştır |
| `.editorconfig` | Biçim + kod stili + **analizör şiddetleri** | Kural `none`/`warning`/`error` |
| `global.json` | Hangi .NET SDK sürümü kullanılacak | Local = CI aynı toolchain |
| `Directory.Build.props` | **Tüm** projelere ortak derleme ayarları | Tekrarı ve sapmayı önler |
| `Directory.Packages.props` | NuGet sürümlerinin **tek** kaynağı | Central Package Management |
| `MediFlow.slnx` | Proje listesi (solution) | Yeni, GUID'siz XML format |
| `compose.yaml` | 4 altyapı servisinin tanımı | Docker Compose |
| `.env.example` | Sır **şablonu** (gerçek `.env` repoda yok) | Yapılandırma koddan ayrı |
| `README.md` | Proje nedir + nasıl çalıştırılır | Repo'nun yüzü |
| `ROADMAP.md` | 16 haftalık plan | Harita, yapılacaklar listesi değil |

---

## 5. GitHub'ta neler oldu?

### Kurduğumuz akış

Artık **her** değişiklik bu yoldan geçiyor. `main`'e doğrudan yazmak **imkânsız**:

```
1. git switch -c feat/bir-sey     ← yeni dal aç (bedava, 41 byte'lık işaretçi)
2. kod yaz, commit at             ← küçük, anlamlı commit'ler
3. git push -u origin feat/...    ← dalı GitHub'a gönder
4. PR aç                          ← şablon otomatik dolar: What / Why / How verified
5. CI otomatik çalışır            ← derle + test + format (~2 dk)
6. Files changed'ı KENDİN OKU     ← PR'ın asıl faydası burada
7. CI yeşilse Squash and merge    ← kırmızıysa buton KİLİTLİ
8. Dal otomatik silinir
9. git switch main && git pull     ← local'i senkronla
```

### Neden PR? "Tek başıma çalışıyorum" diye düşünürsen

PR'ın amacı izin almak değil, **düşünmeye ara vermek**:

- Yarım iş dalda kalır, `main` her zaman çalışır durumdadır
- Diff'i okurken, yazarken göremediğin hataları görürsün
- `git log` anlamlı bir "yapılan işler listesi" olur
- Portfolyoda 40 düzgün PR = "bu kişi ekipte çalışabilir"

### Kurduğumuz korumalar (ruleset: `protect-main`)

| Kural | Ne engelliyor |
|---|---|
| Require a pull request | `main`'e doğrudan push |
| Require status checks (`Build & Test`) | Testler kırıkken merge |
| Require branches to be up to date | İki PR ayrı ayrı yeşil ama birleşimi kırık (*semantic conflict*) |
| Require linear history | Merge commit'leri; geçmiş düz kalır |
| Block force pushes | Geçmişi silen push |

**Ve ikisini de bilerek ihlal edip test ettik.** Hiç tetiklendiğini görmediğin
koruma, koruma değil — sadece varsayımdır.

### Squash merge ne yapıyor?

Dalda 5 dağınık commit olsa bile (`wip`, `fix typo`...) `main`'e **tek temiz
commit** girer. İki sonucu var:

- ⚠️ **PR başlığı = commit mesajı** olur → başlık `feat(scope): ...` formatında olmalı
- GitHub sonuna `(#6)` ekler → `git log`'dan PR tartışmasına tıklanabilir

### Otomatik yardımcılar

| Araç | Ne yapıyor |
|---|---|
| **PR şablonu** | Her PR'da *What / Why / How verified / What I learned* sorar |
| **CODEOWNERS** | Klasör bazlı sahiplik; otomatik reviewer ataması |
| **Dependabot** | Haftalık paket güncellemesi; küçükler tek PR'da gruplu, major'lar ayrı |
| **CI (`ci.yml`)** | Her PR'da temiz bir Ubuntu makinesinde: restore → build → test → format |

### Commit mesajı formatı (Conventional Commits)

```
tip(kapsam): ne yaptın

feat(scheduling): add slot locking with Redis
fix(identity): prevent refresh token reuse
chore(ci): cache nuget packages
docs(adr): record modular monolith decision
```

Tipler: `feat` `fix` `chore` `docs` `test` `refactor` `perf` `ci` `build`

Faydası: okunabilir geçmiş + Faz 6'da **otomatik** CHANGELOG ve sürüm numarası.

---

## 6. .NET tarafında ne kurduk?

### Solution ve proje mantığı

```
MediFlow.slnx                          ← sadece bir LİSTE ("şu projelerden oluşuyorum")
├── MediFlow.BuildingBlocks.Common     ← sınıf kütüphanesi (üretim kodu)
└── MediFlow.BuildingBlocks.Common.Tests ← test projesi
```

**Referans yönü tek yönlüdür ve bu çok önemli:**

```
Tests  ──────►  Common
(test)          (üretim)
```

Test kodu üretim kodunu bilir; üretim kodu testlerin varlığından habersizdir.
Faz 1'de aynı fikri mimarinin tamamına uygulayacağız:

```
Api ──► Application ──► Domain ◄── Infrastructure
```

`Domain` (iş kuralları) hiçbir şeyi bilmez, herkes onu bilir. **Bu yüzden MSSQL
inmeden de Faz 1'e başlayabilirdik** — iş kuralları veritabanından bağımsız.

### Merkezi yapılandırma — hangi problemi çözdü?

Başta iki `.csproj` dosyası da aynı üç satırı tekrarlıyordu:

```xml
<TargetFramework>net10.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable>
```

15 projede iki problem doğurur:

1. **Tekrar** — `net11.0`'a geçmek 15 dosya düzenlemek demek
2. **Sessiz sapma** — yeni bir projede `Nullable`'ı yazmayı unutursan, o proje
   null güvenliğini **hiç hata vermeden** kaybeder

Çözüm: `Directory.Build.props`. MSBuild klasör ağacında **yukarı çıkarak** bu
dosyayı bulur ve her projeye otomatik uygular. Artık `src` tarafındaki `.csproj`
**tamamen boş**.

> ⚠️ Tuzak: Alt klasöre ikinci bir `Directory.Build.props` koyarsan MSBuild
> **aramayı orada bitirir** ve kökteki dosya o alt ağaç için hiç okunmaz.

Paketler için aynı fikir: `Directory.Packages.props` sürümleri tutar, `.csproj`
sadece "bu paketi istiyorum" der. Sürüm yazarsa NuGet **hata verir** (`NU1008`)
— yani kural tavsiye değil, zorunlu.

### `Nullable enable` — en yüksek getirili tek ayar

```csharp
string  ad    = null;   // ⚠️ derleyici uyarır
string? soyad = null;   // ✅ ? ile "null olabilir" dedim

void Yaz(string? s)
{
    Console.WriteLine(s.Length);       // ⚠️ s null olabilir
    if (s is not null)
        Console.WriteLine(s.Length);   // ✅ derleyici artık biliyor
}
```

`NullReferenceException`'ı **çalışma zamanından derleme zamanına** taşıyor. Hata
kullanıcıda patlamak yerine sen yazarken görünüyor.

### Yazdığımız tek gerçek kod: `Result` ve `Error`

**Soru:** Bir iş kuralı ihlal edildiğinde (örn. "bu slot dolu") ne yapmalı?

```csharp
// ❌ Exception yolu
throw new SlotAlreadyTakenException();

// ✅ Bizim yolumuz
return Result.Failure<Appointment>(AppointmentErrors.SlotTaken);
```

Neden ikincisi:

| Sebep | Açıklama |
|---|---|
| **İmza dürüst olur** | `Result<Appointment> Book(...)` "başarısız olabilirim" der. `Appointment Book(...)` yalan söyler |
| **Derleyici yardım eder** | C#'ta `try/catch` yazmaya zorlanmazsın; unutursan derleyici susar |
| **Exception pahalı** | `throw` stack unwinding yapar; "slot dolu" beklenen bir sonuç, bunun için pahalı mekanizma gerekmez |
| **İş kuralı ≠ kaza** | Exception beklenmedik durumlar için: DB koptu, disk doldu, bug var |
| **HTTP'ye çeviri kolay** | `Appointment.SlotTaken` → `409`, `Patient.NotFound` → `404` |

> **Ayrım şu:** beklenen sonuçlar → `Result`. Beklenmeyen durumlar ve
> **programcı hataları** → `exception`.

`Error` tipini `record` yaptık çünkü `record` **değer eşitliği** verir: içeriği
aynı iki `Error` nesnesi eşit sayılır (normal `class`'ta olmazdı).

`Code` (makine için, asla değişmez) ve `Message` (insan için, her an değişebilir)
ayrı tutuldu — böylece React arayüzü metin karşılaştırmak zorunda kalmıyor.

### Analizörler ve katılık

`AnalysisLevel = latest-recommended` yazdığımız an 9 uyarı çıktı. **Her birini
tek tek değerlendirip kararı gerekçesiyle `.editorconfig`'e yazdık.**

İki uç hatadan da kaçındık:

| ❌ Uyarıları yok saymak | ❌ Hepsini körü körüne "düzeltmek" |
|---|---|
| 400 uyarı birikir, gerçek problemler kaybolur | Araç senin yerine tasarım kararı vermiş olur |

Örnek kararlar:
- `CA1707` (alt çizgi yasağı) → **sadece testlerde kapalı**, çünkü test isimleri
  cümle gibi okunmalı
- `CA1716` (`Error` ismi VB.NET'te ayrılmış kelime) → kapalı, çünkü C#-only
  projeyiz ve `Error` Result pattern'in evrensel adı

### En önemli mekanizma: koşullu katılık

```xml
<TreatWarningsAsErrors Condition="'$(ContinuousIntegrationBuild)' == 'true'">
  true
</TreatWarningsAsErrors>
```

| Nerede | Sonuç |
|---|---|
| **Local** `dotnet build` | Uyarı **uyarı** kalır → fikir denerken engellenmezsin |
| **CI** (`-p:ContinuousIntegrationBuild=true`) | Uyarı **hata** olur → `main`'e sızamaz |

Aynı kod, aynı dosya, iki farklı sonuç. Bunu bilerek kanıtladık: kullanılmayan
bir `using` ekledik → local "başarılı + 1 uyarı", CI "BAŞARISIZ + 1 hata".

---

## 7. Docker tarafında ne kurduk?

### Dört servis

| Servis | Adres | Ne için | Hangi fazda |
|---|---|---|---|
| **MSSQL** | `localhost:1433` | Ana veritabanı | Faz 1 |
| **Redis** | `localhost:6379` | Cache + distributed lock | Faz 2 |
| **RabbitMQ** | `5672` / arayüz `15672` | Asenkron mesajlaşma | Faz 3 |
| **Seq** | `localhost:5341` | Log toplama ve arama | Faz 1 |

### Neden container, neden kurulum değil?

| Elle kurulum | Container |
|---|---|
| SQL Server kurulumu ~10 dk, sistem geneline yayılır | `docker compose up -d` |
| Sürüm değiştirmek acı | Tag'i değiştir |
| "Bende 2019, sende 2022" → farklı davranış | Herkes **aynı** image'ı çalıştırır |
| Makinen kirlenir | `docker compose down -v` → iz kalmaz |

Ayrıca **AWS'de de aynı image'ları çalıştıracağız** → Faz 7'de sürpriz azalır.

### Bilmen gereken 4 Docker kavramı

**1. Image vs Container**
`class` ve `new MyClass()` gibi. Image salt-okunur şablon, container çalışan örnek.

**2. Volume — veri container'dan uzun yaşar**
Container silinince içine yazılanlar gider. Volume, Docker'ın yönettiği kalıcı
alandır:

```
docker compose down      → container'lar silinir, VERİ KALIR
docker compose down -v   → veri de SİLİNİR
```

Bunu deneyle kanıtladık: Redis'e ve MSSQL'e veri yazdık, tüm container'ları yok
ettik, geri kurduk — veri yerindeydi.

**3. Servis adı ≠ localhost** ⭐ *(en sık yapılan hata)*

```
Container İÇİNDEN:   Server=mssql,1433      ← servis adı
Senin makinenden:    localhost:1433         ← port dışarı açıldığı için
```

Container'ın içinde `localhost`, **o container'ın kendisi** demektir. Compose'un
DNS'i servis adlarını doğru IP'ye çevirir. Faz 1'de bağlantı dizesini yazarken
bunu hatırla.

**4. Healthcheck — `Up` ≠ hazır** ⭐

`docker compose ps` çıktısında `Up 2 seconds` iki farklı şeyi aynı gösterir:
- "2 saniye önce başladı, ısınıyor"
- "2 saniye önce **yine çöktü** ve yeniden başladı"

Bunu bizzat yaşadık: Seq çökerken `Up 2 seconds` gördüm, "yeni başlamış" sandım.
Şimdi her servise healthcheck var → `(healthy)` etiketi arıyoruz.

Emin olmanın en kesin yolu:
```powershell
docker inspect mediflow-mssql --format "{{.RestartCount}} {{.State.Health.Status}}"
```
`RestartCount` artıyorsa container çöküyor.

### Bulduğumuz iki gerçek hata

**1. `latest` etiketi** — Seq `:latest` idi, 2026.1 sürümü zorunlu bir yeni ayar
(admin şifresi) getirdi ve container sürekli çöktü. Ders: **sürümü sabitle.**
Üretimde bu, "hiçbir şey değiştirmedim ama sistem çöktü" demektir.

**2. `container_name` ≠ hostname** — SQL Server ilk açılışta makine adını
`master` veritabanına **yazıyor**. Container yenilendiğinde `@@SERVERNAME` eski
(silinmiş) container ID'sini döndürmeye devam etti. Çözüm:
`hostname: mediflow-mssql`.

### Sır yönetimi

| Dosya | Repoda? | İçerik |
|---|---|---|
| `.env.example` | ✅ | Şablon, örnek değerler |
| `.env` | ❌ | Gerçek şifreler, sadece senin makinende |

`compose.yaml`'da iki farklı sözdizimi var:

```yaml
${MSSQL_SA_PASSWORD:?mesaj}      # yoksa HATA ver (sır için)
${RABBITMQ_USER:-mediflow}       # yoksa varsayılanı kullan (zararsız değer için)
```

---

## 8. Zincirler — Faz 0'ın asıl içeriği

Dosyaları tek tek anlamak kolay. Zor olan **birbirlerine nasıl bağlandıkları**.

### Katılık zinciri (en önemlisi)

```
.editorconfig            "IDE0005 = warning, CA1707 testlerde kapalı"
      ↓
Directory.Build.props    EnforceCodeStyleInBuild=true
                         → kurallar IDE'de değil DERLEMEDE uygulanır
      ↓
Directory.Build.props    TreatWarningsAsErrors (koşullu)
      ↓
ci.yml                   -p:ContinuousIntegrationBuild=true
                         → koşulu TETİKLEYEN yer burası
      ↓
GitHub ruleset           "Build & Test geçmeden merge yok"
```

Beş dosya tek sonucu üretiyor: **`main`'e uyarı sızamaz, ama sen deneme
yaparken engellenmezsin.** Tek halkayı çıkarsan zincir kopar.

### Diğer üç zincir (kısaca)

| Zincir | Nasıl çalışıyor |
|---|---|
| **SDK** | `global.json` → local `dotnet build` **+** `ci.yml`'deki `setup-dotnet` aynı dosyayı okur → sürüm tek yerde |
| **Paket** | `Directory.Packages.props` → `.csproj` sürüm yazmaz → CI cache anahtarı bu dosyanın hash'i → Dependabot tek dosyayı güncelliyor |
| **Sır** | `.gitignore` (`.env` yok, `!.env.example` var) → şablon repoda → `compose.yaml` `${VAR:?}` ile yoksa hata |

---

## 9. Faz 0'ın üç prensibi

Ezberlenecek komut listesi yoktu; **üç prensip** vardı. Faz 1'de de bunları
uygulayacağız:

| Prensip | Nerede geçti |
|---|---|
| **1. Gürültülü hata > sessiz yanlış davranış** | `Result.Value` throw eder · `${VAR:?}` · `global.json` hata verir · healthcheck |
| **2. Kuralı insana değil sisteme yaptır** | Ruleset · CI'da `TreatWarningsAsErrors` · `.gitignore` · `NU1008` |
| **3. Kurduğun kapıyı test etmeden güvenme** | `main`'e boş commit · bilerek başarısız test · volume deneyi |

Dördüncü bir prensip de kullandık: **önce problemi yaşa, sonra teknolojiyi
öğren.** Faz 2'de bu daha net olacak (çifte rezervasyon hatasını bilerek
üretip Redis lock ile çözeceğiz).

---

## 10. Günlük komutlar (yarın işine yarayacak)

### Git akışı
```powershell
git switch -c feat/bir-sey        # yeni dal
git status --short                # ne değişti
git add <dosya>                   # sahneye al (seçerek!)
git diff --cached                 # sahnede ne var? (commit'ten ÖNCE bak)
git commit -m "feat(x): ..."      # commit
git push -u origin feat/bir-sey   # ilk push (-u bir kez)
git log --oneline main..HEAD       # "bu dalda ne yaptım?"

# merge sonrası
git switch main
git pull
git branch -D feat/bir-sey        # squash merge sonrası -D normal
```

### Kurtarma
```powershell
# main'e yanlışlıkla commit attım (push etmedim)
git branch feat/dogru-dal         # commit'i güvene al
git reset --hard origin/main      # main'i geri sar

# bir şeyi kaybettim?
git reflog                        # ~90 gün geçmiş, commit'lenmiş iş kaybolmaz
```

### .NET
```powershell
dotnet build MediFlow.slnx
dotnet test MediFlow.slnx
dotnet format MediFlow.slnx --verify-no-changes

# CI'ın gördüğü katı modda dene (push etmeden önce!)
dotnet build MediFlow.slnx -p:ContinuousIntegrationBuild=true --configuration Release
```

### Docker
```powershell
docker compose up -d              # başlat
docker compose ps                 # durum + healthcheck  ← (healthy) ara
docker compose logs -f mssql      # logları izle
docker compose down               # durdur (VERİ KALIR)
docker compose down -v            # durdur + VERİYİ SİL

docker inspect mediflow-mssql --format "{{.RestartCount}} {{.State.Health.Status}}"
```

---

## 11. Faz 1'e girmeden bekleyen işler

| # | İş | Kim |
|---|---|---|
| 1 | Haftalık günlük: `docs/journal/Faz0` → `2026-W40.md` olarak yeniden adlandır ve doldur | **sen** |
| 2 | GitHub Student Developer Pack başvurusu (ücretsiz, 5 dk) | **sen** |
| 3 | **Collation kararı** (aşağıda) | birlikte |

### Collation kararı — Faz 1'in ilk kararı

MSSQL sunucu varsayılanı `SQL_Latin1_General_CP1_CI_AS`. Bu collation Türkçe'nin
büyük/küçük harf kurallarını bilmiyor:

- Türkçe'de `i` → `İ`, `I` → `ı`
- Latin1'de `i` → `I` (yanlış)

Hasta adı arama ve sıralamada somut hatalara yol açar. Faz 1'de EF Core
veritabanını oluştururken ya veritabanı bazında Türkçe collation seçeceğiz ya da
bilinçli olarak Latin1'de kalıp gerekçesini bir ADR'ye yazacağız.

---

## 12. Faz 1'de ne yapacağız?

**Modüler monolit** olarak `Identity` ve `PatientProfile` modüllerini yazacağız
(neden monolitle başladığımız: [ADR-0001](adr/0001-modular-monolith-first.md)).

Öğrenilecekler:

| Konu | Ne çözüyor |
|---|---|
| **Clean Architecture** | 4 katman, bağımlılık yönü `Api → Application → Domain ← Infrastructure` |
| **DDD temelleri** | Entity vs Value Object, aggregate, invariant, anemic model tuzağı |
| **CQRS + MediatR** | Komut (yaz) ve sorgu (oku) ayrımı |
| **Pipeline behavior** | Validation, logging, transaction — tek yerden tüm handler'lara |
| **EF Core** | Migration, soft delete, audit alanları |
| **JWT** | Access + refresh token, rotation, permission-based authorization |
| **Serilog + Seq** | Yapılandırılmış log — Faz 0'da kurduğumuz Seq devreye girecek |

İlk iş **veritabanı gerektirmiyor**: `Patient` aggregate'ini TDD ile
modelleyeceğiz. Saf C#, sıfır bağımlılık — Clean Architecture'ın asıl faydası bu.

---

*Hazırlanma tarihi: 2026-09-28 · Faz 0 kapanışı*
