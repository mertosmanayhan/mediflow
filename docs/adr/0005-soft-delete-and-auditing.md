# ADR-0005: Silme işlemi "soft delete", audit alanları interceptor ile

- **Durum:** Accepted
- **Tarih:** 2026-10-13
- **Karar veren:** Mert Osman Ayhan
- **İlgili:** Faz 1 Blok 5, PR #26, ADR-0001

## Bağlam

Faz 1'in bitmiş sayılma şartında hasta kaydı silme vardı. Silmenin ne anlama
geldiğine karar verilmesi gerekiyordu, çünkü iki ayrı baskı var:

**Sağlık verisi silinmez.** Bir hasta kaydının hangi tedaviye, hangi reçeteye,
hangi randevuya bağlı olduğu bilinmiyor; satırı fiziksel olarak silmek bu
bağları kopartır ve geri getirmenin yolu yoktur. Tıbbi kayıtlar için yasal
saklama süreleri de var.

**KVKK silme hakkı var.** Kişi verisinin silinmesini talep edebiliyor.

Ayrıca her tabloda tekrar eden bir soru vardı: bir kaydın ne zaman
oluşturulduğu ve ne zaman güncellendiği nerede tutulacak, kim yazacak?

## Karar

**1. Silme "soft delete".** `DELETE /patients/{id}` satırı silmiyor;
`IsDeleted = 1` ve `DeletedAtUtc` yazıyor. Satır veritabanında kalıyor.

**2. Uygulama kodu bunu bilmiyor.** İki EF Core mekanizması bunu görünmez
kılıyor:

- **`SoftDeleteInterceptor`** — `SaveChanges` anında `Deleted` durumundaki
  entity'leri yakalayıp `Modified`'a çeviriyor ve bayrakları yazıyor.
  Repository `context.Patients.Remove(patient)` yazmaya devam ediyor.
- **Global query filter** — `ApplySoftDeleteFilter()` tüm `ISoftDeletable`
  entity'lere `IsDeleted == false` filtresi ekliyor. Her sorguya otomatik
  giriyor; tek tek `Where` yazılmıyor.

**3. Audit alanları da interceptor ile.** `AuditingInterceptor`, `IAuditable`
uygulayan entity'ler için `Added` durumunda `CreatedAtUtc`, `Modified`
durumunda `UpdatedAtUtc` yazıyor. `TimeProvider` ile enjekte ediliyor (test
edilebilirlik). `CreatedAtUtc` güncellemede `IsModified = false` ile korunuyor.

**4. Alanlar domain'de `private set`.** Entity'nin kendisi bile bunları
değiştiremiyor; yazan tek şey interceptor.

## Gerekçe

**1. Soft delete bir iş kuralı, teknik detay değil.**
"Hasta kaydı silinmez, pasifleştirilir" cümlesi bir sağlık sistemi için
iş gerçeği. Hard delete bu gerçeği baştan imkânsız kılar.

**2. Interceptor, "unutma" ihtimalini ortadan kaldırıyor.**
Alternatif, her handler'da `patient.MarkDeleted()` çağırmak ve her sorguda
`Where(p => !p.IsDeleted)` yazmaktı. Her ikisi de **unutulabilir**, ve
unutulan filtre silinmiş hastayı canlıda geri getirir. Interceptor + query
filter ile doğru davranış **varsayılan** hale geliyor; yanlış davranış için
açıkça `IgnoreQueryFilters()` yazmak gerekiyor.

**3. Audit alanları çapraz kesen (cross-cutting) bir ihtiyaç.**
Her entity'de aynı iki alan, her handler'da aynı iki satır. Bu tanım gereği
tekrardır. Interceptor onu **tek yere** indiriyor ve yeni bir entity'nin
sadece `IAuditable` uygulaması yeterli hale geliyor.

**4. `TimeProvider` yoksa zaman test edilemez.**
`DateTimeOffset.UtcNow` doğrudan çağrılsa "oluşturulma zamanı doğru mu"
testi yazılamazdı. `TimeProvider` enjekte edilince test sahte saat
verebiliyor.

## Değerlendirilen alternatifler

### Alternatif A: Hard delete

- **Artı:** Basit; veritabanı küçük kalır, sorgular filtre taşımaz, unique
  indeksler sorunsuz çalışır.
- **Artı:** KVKK silme hakkı doğrudan karşılanır.
- **Eksi:** Geri dönüşü yok. Yanlışlıkla silinen hasta kaydı yedekten
  dönülmeden kurtarılamaz.
- **Eksi:** İlişkili kayıtlar (tedavi, reçete) ya cascade ile silinir ya da
  kırılır. İkisi de sağlık verisinde kabul edilemez.
- **Neden reddedildi:** Tıbbi kayıt bütünlüğü, basitlikten önce geliyor.

### Alternatif B: Arşiv tablosuna taşı

Silinen satırı `PatientsArchive` tablosuna kopyala, aslını sil.

- **Artı:** Canlı tablo temiz ve küçük kalır; sorgular filtre taşımaz;
  unique indeksler sorunsuz.
- **Artı:** Arşiv verisi farklı (daha ucuz) depolamaya taşınabilir.
- **Eksi:** Şema iki yerde; her alan değişikliği iki tabloda yapılır ve
  ikisinin ayrışması zamanla kaçınılmazdır.
- **Eksi:** Geri getirme kodu elle yazılır; yabancı anahtar ilişkileri
  arşivde korunamaz.
- **Neden reddedildi:** Bakım maliyeti, soft delete'in bedellerinden yüksek.
  Tablo gerçekten büyüyene kadar (milyonlarca satır) gerekçesi yok.

### Alternatif C: Geçici tablolar / temporal tables (SQL Server)

SQL Server'ın `SYSTEM_VERSIONING` özelliği, satırın tüm geçmişini otomatik
tutuyor.

- **Artı:** Sadece silmeyi değil **her değişikliği** kaydeder; "bu kaydı kim
  ne zaman neye çevirdi" sorusuna tam cevap verir. Denetim için en güçlü yol.
- **Eksi:** SQL Server'a sıkı bağlanma — ADR-0001'deki mikroservis hedefinde
  bazı servislerin başka veritabanı kullanması muhtemel.
- **Eksi:** Geçmiş tablosu hızlı büyür; saklama politikası ayrı bir iş.
- **Eksi:** "Kim" bilgisini kendi başına tutmaz; kullanıcı kimliği için yine
  uygulama katmanı gerekir.
- **Neden şimdi değil:** Denetim izi Faz 5'in konusu (sağlık verisinde kim
  neye baktı kaydı). O zaman temporal tables veya ayrı denetim günlüğü
  kararı ayrı ADR olarak verilecek.

### Alternatif D: Audit alanlarını handler'larda yaz

- **Artı:** Açık ve görünür; "sihir" yok, kodu okuyan ne olduğunu görür.
- **Eksi:** Her komutta tekrar; unutulursa alan boş kalır ve bu derleme
  zamanında yakalanmaz.
- **Neden reddedildi:** Çapraz kesen ihtiyaç için tekrar, doğru cevap değil.
  "Görünmezlik" bedeli ADR ve kod yorumlarıyla karşılanıyor.

## Sonuçlar

### Olumlu

- Yanlış silinen kayıt geri getirilebilir; ilişkili veri kopmaz.
- Doğru davranış varsayılan: filtre unutulamıyor, audit alanı unutulamıyor.
- Yeni entity için maliyet tek satır: `IAuditable, ISoftDeletable`.
- Zaman enjekte edilebilir, dolayısıyla test edilebilir.
- Entegrasyon testiyle doğrulandı: silinen kayıt API'de 404 veriyor ama
  `IgnoreQueryFilters()` ile satır `IsDeleted = true` olarak bulunuyor.
- Filtrelenmiş indeks (`WHERE IsDeleted = 0`) indeksleri silinmiş satırlarla
  şişirmiyor.

### Olumsuz / kabul ettiğimiz bedeller

- **Unique indeksler artık "unique" değil.** Silinmiş `ahmet@x.com` varsa
  aynı e-posta yeniden kaydedilemez — indeks silinmiş satırı da görür. Çözüm
  filtrelenmiş indeks (`WHERE IsDeleted = 0`), ve bu **her** unique indeks
  için ayrı ayrı düşünülmek zorunda. Unutulursa kullanıcıya anlamsız bir
  "zaten kayıtlı" hatası olarak çıkar.
- **Veritabanı sürekli büyüyor.** Hiçbir satır gitmiyor. Temizlik politikası
  (yasal saklama süresi sonunda gerçek silme) ayrı bir iş olarak duruyor.
- **Her sorguya ek koşul giriyor.** Query filter her `SELECT`'e
  `IsDeleted = 0` ekliyor; indeks desteklemiyorsa sorgu planı bozulabilir.
- **Query filter sessizce atlanabilir.** `FromSqlRaw` ile yazılan ham SQL
  filtreyi uygulamaz — silinmiş kayıtlar sızar. Ham SQL yazılan her yerde
  elle filtre eklemek gerekiyor.
- **Navigation'larda beklenmedik davranış.** Bir hastanın silinmiş ilişkili
  kayıtları varsa `Include` onları getirmez; koleksiyon "eksik" görünür ve
  sebebi görünmez.
- **KVKK silme hakkını KARŞILAMIYOR.** Soft delete veriyi *saklıyor*. Gerçek
  silme talebi geldiğinde ayrı bir "anonimleştirme" veya "kalıcı silme" akışı
  gerekecek. Bu bilinen bir açık iş.
- **Cross-modül atomiklik yok.** `UnitOfWorkBehavior` her `IUnitOfWork` için
  ayrı `SaveChanges` çağırıyor; iki modül aynı istekte yazarsa biri başarılı
  diğeri başarısız olabilir. Bilinçli tercih — açık transaction,
  `EnableRetryOnFailure` ile birlikte execution strategy tuzağına düşüyor.
  Faz 3'te Outbox pattern ile çözülecek.

### Yeniden değerlendirme koşulu

- Tablo boyutu sorgu performansını etkilemeye başlarsa: Alternatif B (arşiv).
- Tam denetim izi gerektiğinde (Faz 5): temporal tables veya denetim günlüğü.
- KVKK kalıcı silme talebi geldiğinde: anonimleştirme akışı, ayrı ADR.
