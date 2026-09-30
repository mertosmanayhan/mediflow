# Faz 0 — Temel Kurulum

- **Tarih aralığı:** 2026-09-28 → 2026-09-30
- **Merge edilen PR'lar:** #1, #2, #3, #4, #6 *(#5 kapatıldı, bilerek)*
- **Yazılan ADR'ler:** ADR-0001 (modüler monolitle başla)
- **Sonuç:** `main`'de 7 commit, 27 dosya, 10 test, 4 container

> Bu kayıt geliştirme oturumunda **gerçekten olanlardan** derlendi. Kendi öznel
> notlarını en sondaki bölüme ekleyebilirsin.

---

## Hedef

Kod yazmaya başlamadan önce üç şeyi kurmak:

1. Kodun nasıl saklanacağı ve korunacağı (Git + GitHub akışı)
2. Nasıl derlenip test edileceği, hangi kuralların geçerli olacağı
3. Neyin üstünde çalışacağı (Docker altyapısı)

Ürün kodu **hedef değildi**; alışkanlık ve altyapı hedeftir.

---

## Ne yapıldı

**Adım 1 — Git deposu.** `git init -b main`, `.gitignore` (derleme çıktısı + IDE
+ sırlar), `.gitattributes` (satır sonu), klasör iskeleti. İlk commit.

**Adım 2 — GitHub.** Public repo (sınırsız Actions dakikası + portfolyo),
HTTPS + Git Credential Manager, `git remote add` + `push -u`. MIT lisansı
eklendi (lisanssız public repo = tüm hakları saklı).

**Adım 3 — Koruma ve hijyen (PR #1).** Ruleset `protect-main`: PR zorunlu,
linear history, force push yasak. Squash merge tek yöntem, dal otomatik silinir.
PR şablonu, CODEOWNERS, Dependabot.

**Adım 4 — Solution (PR #2).** `MediFlow.slnx`, `global.json` ile SDK sabitleme,
`BuildingBlocks.Common` + xUnit test projesi. `Result` / `Error` tipleri ve
10 test.

**Adım 5 — Merkezi yapılandırma (PR #3).** `Directory.Build.props`,
`Directory.Packages.props` (CPM), `.editorconfig`. Analizör seviyesi
`latest-recommended`.

**Adım 6 — CI (PR #4).** `ci.yml`: restore → build (Release, katı mod) → test →
format. Ruleset'e zorunlu status check eklendi.

**Adım 7 — Docker (PR #6).** `compose.yaml`: MSSQL, Redis, RabbitMQ, Seq.
Volume'ler, healthcheck'ler, `.env` / `.env.example` ayrımı.

**Adım 8 — ADR (PR #6).** ADR konvansiyonu + şablon + ADR-0001.

---

## Nerede tıkandık

> ⭐ Bu bölüm bu dosyanın asıl değeri. Her madde gerçekten yaşandı.

### 1. `global.json`'da olmayan SDK sürümü — `dotnet` tamamen durdu

**Belirti:** `dotnet --version` bile çalışmadı:
```
Requested SDK version: 10.0.400
A compatible .NET SDK was not found.
```

**Kök neden:** `global.json`'daki sürüm kurulu olmayan bir sürüme (10.0.400)
değişmişti; makinede 10.0.200 var.

**Öğrenilen:** `rollForward` **sadece ileriye** esneklik verir. Hiçbir politika
"400 istedim, 200 ile idare et" demez — bu tasarım gereği, eksik bir SDK ile
derlemek öngörülemez sonuç üretir.

**Yan fayda:** `global.json`'ın vaat ettiği davranışı canlı gördük — sessizce
başka sürüme düşmedi, durdu ve nedenini söyledi.

### 2. PowerShell'in native komut tırnak sorunu — **üç kez** düştük

**Belirti 1:** `git commit -m` çok satırlı mesajda:
```
error: pathspec 'rights' did not match any file(s) known to git
```
**Belirti 2:** `sqlcmd -Q "SET NOCOUNT ON; SELECT ..."`:
```
Msg 156: Incorrect syntax near the keyword 'SET'.
```
**Belirti 3:** `sqlcmd -s " | "`:
```
Sqlcmd: '-s': Missing argument.
```

**Kök neden:** Windows PowerShell 5.1, native (`.exe`) komutlara argüman
geçerken iç içe çift tırnakları bozuyor; argüman parçalara bölünüyor.

**Çözümler:**
- Uzun commit mesajı → dosyaya yaz, `git commit -F dosya.txt`
- SQL → dosyaya yaz, `sqlcmd -i dosya.sql` *(en temizi)*
- Kaçınılmazsa → PowerShell tek tırnaklı dizgede `''` ile bash'e tek tırnak geçir

**Ders:** Üç seviyeli tırnak (PowerShell → bash → sqlcmd) ile boğuşmak yerine
**içeriği dosyaya taşı.**

### 3. Seq container'ı sessizce çöküyordu — `Up` yanılttı

**Belirti:** `docker compose ps` → `mediflow-seq  Up 2 seconds`. Ama
`http://localhost:5341` bağlanmıyordu. "Yeni başlamış, biraz bekleyelim" diye
yorumlandı — **yanlış yorum.**

**Gerçek:** `restart: unless-stopped` yüzünden container sürekli çöküp yeniden
başlıyordu. `Up 2 seconds` aslında *"2 saniye önce yine yeniden başladı"*
demekti.

**Kök neden:** Seq 2026.1, ilk çalıştırmada admin şifresi **zorunlu** kıldı:
```
No default admin password was supplied; set firstRun.adminPassword or
SEQ_FIRSTRUN_ADMINPASSWORD, or opt out using firstRun.noAuthentication
```
Image `:latest` etiketindeydi, yeni sürüm habersiz geldi.

**İki ders:**
- **`Up` ≠ hazır.** Healthcheck olmayan servis çökerken "ayakta" görünür.
  Seq'e healthcheck yazmama kararı geri alındı.
- **`latest` = bir gün habersiz kırılacak.** Tüm image sürümleri sabitlendi.

**Üçüncü ders — yığın izi okuma:** 40 satırlık Autofac yığın izinin **sonu**
hiçbir şey söylemiyordu. Gerçek neden **ortadaki inner exception**'daydı.

### 4. Compose proje adı Türkçe karakterlerle bozuldu

**Belirti:** `Network gncelproje_default Created`, `Volume gncelproje_redis-data`

**Kök neden:** Proje adı belirtilmediğinde Compose klasör adından türetir.
`GüncelProje` → Docker kaynak adlarında ASCII dışı karakter geçersiz → `gncelproje`.

**Gerçek risk:** Klasörü yeniden adlandırırsan proje adı değişir ve Compose eski
volume'leri artık bu projeye ait saymaz → **veritabanın "kaybolur"** (aslında
yetim kalır).

**Çözüm:** `compose.yaml` içine açıkça `name: mediflow`.

### 5. `container_name` container'ın **içindeki** hostname'i ayarlamıyor

**Belirti:** `docker compose down` + `up` sonrası:
```
@@SERVERNAME -> 4ecd9ca1b273   (artık var olmayan container)
hostname     -> aa31b80bb547   (yeni container)
```

**Kök neden:** `container_name` Docker'daki addır. Container'ın **içindeki**
hostname varsayılan olarak container ID'sinin ilk 12 karakteridir ve her
yeniden oluşturmada değişir. SQL Server ilk açılışta makine adını `master`
veritabanına **yazdığı** ve volume kalıcı olduğu için eski ad orada kaldı.

**Neden önemli:** replication, linked server ve Always On `@@SERVERNAME`'e
güvenir; yönetim script'leri instance'ı bu adla bulur.

**Çözüm:** `hostname: mediflow-mssql`. Tam sıfırlama sonrası doğrulandı ve
ikinci bir `down`/`up` döngüsünde sabit kaldığı görüldü.

**Meta-ders:** Bu hata **sadece doğrulama yapıldığı için** bulundu. PR'ı
doğrulamadan merge etseydik Faz 3 veya Faz 7'de, teşhisi çok daha pahalı bir
anda çıkacaktı.

### 6. `IDE0005` sessizce çalışmıyordu

**Belirti:** `.editorconfig`'de `IDE0005.severity = warning` yazılıydı ama
kullanılmayan `using` yakalanmıyordu. Derleyici bunu söyledi:
```
warning EnableGenerateDocumentationFile: ... 'GenerateDocumentationFile' = true
```

**Kök neden:** Roslyn'in bilinen kısıtı — bu analiz derleme sırasında ancak XML
dokümantasyon dosyası üretiliyorsa çalışabiliyor (dotnet/roslyn#41640).

**Çözüm:** `GenerateDocumentationFile = true`, yan etkisi olan `CS1591`
gerekçeli olarak kapatıldı.

**Ders:** Bir kuralı `.editorconfig`'e yazmak, uygulandığı anlamına gelmez.
Kural gerçekten tetikleniyor mu diye **test etmek** gerekiyor.

### 7. MSSQL image indirmesi ~1,5 saat sürdü

**Ölçüm:** Cloudflare'den 19 MB → 74 saniye = **~0,3 MB/s (≈2 Mbps)**. Yani
darboğaz Docker veya MCR değil, **bağlantı hızıydı**. `max-concurrent-downloads`
gibi ayarları değiştirmek hiçbir şey kazandırmazdı.

**Ders:** Bir yavaşlığı "optimize etmeye" başlamadan önce **ölç ve darboğazı
tespit et.** Yanlış yeri optimize etmek zaman kaybı.

**Yan not:** Docker'ın pull ilerlemesi terminal akışına basıldığı için
yönlendirilmiş çıktıda okunamıyor. İlerlemeyi görmenin yolu Docker Desktop
arayüzü.

---

## Ne öğrendik

### Git / GitHub
- Git **dağıtıktır**: `.git` klasöründe tam geçmiş var; komutların %90'ı çevrimdışı çalışır
- Dal = bir commit'i gösteren **41 byte'lık işaretçi** → açmak bedava
- Staging area, değişiklikleri anlamlı gruplara bölmek için var (`git diff --cached` ile kontrol)
- `git reset` üç mod: `--soft` (staged kalır) / `--mixed` (diskte kalır) / `--hard` (siler)
- Squash merge'de **PR başlığı commit mesajı olur** → başlık Conventional Commits formatında olmalı
- Squash sonrası `git branch -d` "merge edilmemiş" der → `-D` bu akışta normal
- `git reflog` erişilemez commit'leri ~90 gün tutar → commit'lenmiş iş kaybolmaz
- Kurallar **sunucuda** uygulanır (`remote:` öneki); local'de hiçbir şey yasak değil
- PR şablonu **varsayılan daldan** okunur → şablonu ekleyen PR onu kullanamaz
- PR ve issue numaraları tek sayaç paylaşır ve asla yeniden kullanılmaz

### .NET / derleme
- `.csproj` artık dosyaları otomatik dahil eder → merge conflict kaynağı olmaktan çıktı
- SDK (`global.json`) ile çalışma zamanı (`TargetFramework`) farklı şeyler
- MSBuild `Directory.Build.props`'u yukarı çıkarak arar ve **en yakınını** alır; ikincisi kökteki dosyayı gizler
- `.props` başa aktarılır (ezilebilir), `.targets` sona (zorla uygulanır)
- `Nullable enable`, `NullReferenceException`'ı çalışma zamanından derleme zamanına taşır
- `record` değer eşitliği verir; `class` referans eşitliği
- Beklenen başarısızlık → `Result`; programcı hatası ve beklenmedik durum → `exception`
- `!` (null-forgiving) bir kaçış kapısıdır; gerekçesi açıklanabildiğinde kullanılır
- Analizör kuralları araç, kutsal metin değil → her istisna gerekçesiyle yazılır

### CI / Docker
- Runner her seferinde **sıfırdan temiz** makine → "bende çalışıyordu" böyle yakalanır
- Actions dakikası OS çarpanlı: Linux 1×, Windows 2×, macOS 10×
- Koşullu katılık: local'de uyarı, CI'da hata → hız ve kalite aynı anda
- Container içinde `localhost` = o container; servisler **servis adıyla** konuşur
- Volume container'dan uzun yaşar: `down` korur, `down -v` siler
- `Up` ≠ hazır; healthcheck ve `RestartCount` gerçeği söyler

### Yöntem
- **Kurduğun kapıyı test etmeden güvenme.** Hiç tetiklendiğini görmediğin koruma,
  koruma değil — varsayımdır. Üç kez uygulandı: `main`'e boş commit (reddedildi),
  bilerek başarısız test (merge kilitlendi), volume kalıcılığı (veri kaldı).
- **CI'a koyacağın komutu önce local'de çalıştır.** Commit-push-bekle döngüsüyle
  ayıklamak 10 kat yavaş.
- **Doğrulamadığın iddiayı commit'lemeyin.** README "şu komut çalışır" derken o
  komut bir kez çalıştırılmış olmalı.

---

## Alınan kararlar

| Karar | Gerekçe |
|---|---|
| Public repo | Sınırsız Actions dakikası + ücretsiz CodeQL + portfolyo |
| HTTPS + Credential Manager (SSH değil) | En az sürtünme; SSH Faz 7'de EC2 için öğrenilecek |
| `.slnx` (klasik `.sln` değil) | GUID'siz, okunabilir, merge conflict üretmiyor |
| **xUnit v2** (v3 değil) | v2'nin dokümantasyonu ve örnekleri kat kat fazla; 1. haftada yeni test platformuyla boğuşmak öğrenme değil sürtünme. Faz 6'da tekrar bakılacak |
| `compose.yaml` kökte (`deploy/docker` değil) | `docker compose up` komutu `-f` olmadan çalışsın; README'deki söz doğru kalsın |
| Seq'te şifre (auth kapatmak değil) | "Sırlar `.env`'den gelir" kuralı tüm projede tek tip kalsın |
| `TreatWarningsAsErrors` sadece CI'da | Local'de katılık geliştirmeyi yavaşlatır ve insanı kuralı kapatmaya iter |
| Kod/commit İngilizce, doküman Türkçe | Kod portfolyonun en çok okunan kısmı; dokümanda öğrenme önce gelir |
| Mimari karar | → [ADR-0001](../adr/0001-modular-monolith-first.md) |

---

## Hâlâ net olmayanlar

Faz 1'e geçerken bilinçli olarak **yüzeysel** bırakılanlar:

- **MSBuild'in tamamı.** `Directory.Build.props` mekanizmasını biliyoruz ama
  MSBuild'in hedef/görev (target/task) modeli hiç ele alınmadı. Şimdilik gerek yok.
- **GitHub Actions'ın ileri özellikleri.** Matrix, path filter, reusable workflow,
  composite action → Faz 6'da 9 servis olduğunda gerekecek.
- **Docker networking detayı.** Servis adı çözümlemesini biliyoruz ama bridge
  ağları, port yayınlamanın altındaki iptables mekanizması ele alınmadı.
- **`Result` için fonksiyonel birleştiriciler** (`Map`, `Bind`, `Match`). Bilinçli
  olarak eklenmedi (YAGNI) — ihtiyaç doğduğunda eklenecek.

---

## Sonraki faza devreden

| # | İş | Not |
|---|---|---|
| 1 | **Collation kararı** | Sunucu varsayılanı `SQL_Latin1_General_CP1_CI_AS`. Türkçe'de `i`→`İ`, `I`→`ı`; Latin1 bunu bilmez → hasta adı arama/sıralamada hata. Faz 1'de EF Core şemayı oluştururken karar verilip ADR'ye yazılacak |
| 2 | **Assertion kütüphanesi** | Faz 0'da düz `Assert` kullanıldı (sıfır bağımlılık). FluentAssertions'ın 8.x sürümünde lisans modeli değişti — kullanmadan önce güncel şartlar kontrol edilmeli. Alternatifler: Shouldly, AwesomeAssertions, düz `Assert` |
| 3 | GitHub Student Developer Pack başvurusu | Ücretsiz; Copilot Pro, JetBrains, cloud kredileri |
| 4 | `deploy/docker/` hâlâ boş | Faz 7'de üretim compose dosyası gelecek |

---

## Kendi notların

*(Buraya kendi öznel gözlemlerini ekle — yukarıdaki kayıt olgusal, bu bölüm sana ait.)*

**En çok zorlandığım kavram:**

**Şaşırdığım şey:**

**Tekrar bakmam gereken konu:**
