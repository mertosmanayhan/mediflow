# ADR-0004: Yetkilendirme rol bazlı değil, izin bazlı yapılıyor

- **Durum:** Accepted
- **Tarih:** 2026-10-13
- **Karar veren:** Mert Osman Ayhan
- **İlgili:** Faz 1 Blok 4, PR #26

## Bağlam

Faz 1'de JWT kimlik doğrulama kuruldu: kullanıcı giriş yapıyor, jeton alıyor,
jetonun geçerli olduğu doğrulanıyor. Ama **kimlik doğrulama (authentication)
yetkilendirme (authorization) değildir**: jetonu olan herkes her şeyi
yapabiliyordu.

Sistemde dört rol var: `Patient`, `Nurse`, `Doctor`, `Administrator`.
Korunması gereken ilk iki işlem: hasta kaydı okumak ve hasta kaydı yazmak.

ASP.NET Core bunun için iki hazır yol sunuyor:

1. `[Authorize(Roles = "Doctor,Nurse,Administrator")]` — rol bazlı
2. `[Authorize(Policy = "...")]` — politika bazlı

Karar gerektiriyor çünkü bu seçim **endpoint sayısıyla çarpılarak büyüyor**:
sonradan dönmek, korunan her endpoint'e tek tek dokunmak anlamına gelir.

## Karar

Endpoint'ler **izin** (permission) istiyor, rol istemiyor:

```csharp
.RequirePermission(Permissions.PatientsWrite)   // "patients:write"
```

Rol → izin eşlemesi tek bir yerde, `Identity.Domain/RolePermissions.cs`'te:

| Rol | İzinler |
|---|---|
| `Patient` | `patients:read` |
| `Nurse` | `patients:read`, `patients:write` |
| `Doctor` | `patients:read`, `patients:write` |
| `Administrator` | hepsi + `users:read`, `users:manage-roles` |

Jeton üretilirken roller **izinlere çevrilip** `permission` claim'i olarak
jetona yazılıyor. Yetkilendirme anında veritabanına hiç gidilmiyor.

Politikalar `PermissionPolicyProvider` ile **ihtiyaç anında** üretiliyor
(`perm:` önekli politika adı görülünce), yani her yeni izin için başlangıçta
`AddPolicy` satırı yazmak gerekmiyor.

## Gerekçe

**1. Rol bazlıda kural endpoint'lere dağılır.**
`Roles = "Doctor,Nurse,Administrator"` yazarsak, "hangi roller hasta
yazabilir?" sorusunun cevabı tek bir yerde değil — korunan tüm endpoint'lere
serpilmiş olur. Yeni bir rol eklendiğinde (örneğin `Receptionist`) o rolün
yazabildiği her endpoint'i bulup listeye eklemek gerekir. **Unutulan bir
endpoint = güvenlik açığı veya bozuk özellik**, ve ikisi de derleme zamanında
görünmez.

İzin bazlıda aynı değişiklik `RolePermissions.cs`'te tek satır. Endpoint'lere
hiç dokunulmuyor.

**2. Endpoint'in ifade ettiği şey doğru hale geliyor.**
`POST /patients` endpoint'inin gerçek şartı "doktor ol" değil, "hasta yazma
yetkisi taşı". Rol, o yetkiye sahip olma *sebebidir* — şartın kendisi değil.
Kod da bunu böyle söylüyor.

**3. İzinler bileşiktir, roller değildir.**
Bir kullanıcı birden fazla rol taşıyabiliyor; `RolePermissions.For(roles)`
izinleri **birleştiriyor**. Rol bazlıda "iki rolden birine sahipse" mantığını
her endpoint'te elle kurmak gerekirdi.

**4. Jeton taşıdığı için her istekte sorgu yok.**
İzinler claim olarak jetonda. Yetki kontrolü tamamen bellekte, veritabanına
gitmeden yapılıyor. Yetkilendirme HER istekte çalışan bir şey — buradaki
maliyet doğrudan gecikmeye yansır.

## Değerlendirilen alternatifler

### Alternatif A: Rol bazlı (`[Authorize(Roles = "...")]`)

- **Artı:** Sıfır altyapı; framework'ün hazır yolu. Küçük sistemde yeterli.
- **Artı:** Jeton daha küçük (tek rol claim'i, 4-5 izin claim'i değil).
- **Eksi:** Kural endpoint'lere dağılır (yukarıdaki 1. gerekçe).
- **Eksi:** "Bu rol neler yapabilir?" sorusu kodu tarayarak cevaplanır.
- **Neden reddedildi:** Dağılan yetki kuralı, sistem büyüdükçe geri dönüşü
  en pahalı hatalardan biri. Mikroservislere geçerken (ADR-0001) bu kural
  servis sınırlarını da aşacak.

### Alternatif B: Her istekte veritabanından izin oku

- **Artı:** İzin değişikliği **anında** etkili olur. İzin bazlı jetonda,
  kullanıcının izni geri alınsa bile elindeki jeton süresi bitene kadar
  (15 dk) geçerli kalır.
- **Eksi:** Her korunan istekte en az bir veritabanı sorgusu. Yetkilendirme
  her istekte çalışıyor; bu doğrudan gecikme ve veritabanı yükü.
- **Eksi:** Kimlik servisi, yetki soran her servis için tek hata noktası
  haline gelir — mikroservis hedefiyle ters.
- **Neden reddedildi:** Erişim iptalinde 15 dakikaya kadar gecikme, kabul
  edilebilir bir bedel. Acil iptal gerekirse Faz 3'te Redis tabanlı jeton
  kara listesi eklenebilir (iptal edilenler azdır, yani sorgu da azdır).

### Alternatif C: Kaynak bazlı yetkilendirme (`IAuthorizationHandler<T>`)

"Bu doktor BU hastayı görebilir mi?" gibi, karar verilen nesneye bakan
yetkilendirme.

- **Artı:** Sağlık verisinde gerçekten gereken şey bu — bir doktorun kendi
  hastası olmayan birinin kaydını görmemesi gerekir.
- **Eksi:** Kaynağı yüklemeden karar verilemez; yetkilendirme veri erişimine
  karışır.
- **Neden şimdi değil:** İzin bazlı kontrolün **yerine** değil, **üstüne**
  gelen bir katman. İzin kaba filtre (bu kullanıcı hasta okuyabilir mi?),
  kaynak bazlı ince filtre (bu kaydı okuyabilir mi?). Faz 2'de hekim-hasta
  ilişkisi modellendiğinde eklenecek ve kendi ADR'sini alacak.

## Sonuçlar

### Olumlu

- Rol → izin eşlemesi tek dosyada; yeni rol eklemek endpoint'lere dokunmuyor.
- Endpoint'ler iş şartını (`patients:write`) ifade ediyor, organizasyon
  şemasını değil.
- Yetki kontrolü bellekte, veritabanı sorgusu yok.
- Politikalar ihtiyaç anında üretiliyor; başlangıçta kayıt listesi yok.
- 401/403 ayrımı doğru: kimliği bilinmeyen 401, yetkisi olmayan 403 alıyor.
  Entegrasyon testiyle doğrulandı (`AuthorizationTests`).

### Olumsuz / kabul ettiğimiz bedeller

- **İzin iptali 15 dakikaya kadar gecikir.** Jeton süresi bitene kadar eski
  izinler geçerli. Kısa jeton süresi bunu sınırlıyor, ama sıfırlamıyor.
- **Jeton büyüyor.** Her izin bir claim; Administrator'ın jetonu Patient'ın
  jetonundan belirgin şekilde büyük. Jeton her istekte başlıkta gidiyor, yani
  bu bir bant genişliği maliyeti. İzin sayısı yüzlere çıkarsa izinleri
  gruplamak veya jetonda referans tutmak gerekir.
- **İzinler derleme zamanında denetlenmiyor.** `Permissions` sabitlerini
  kullanıyoruz ama `RequirePermission("patinets:write")` yazsak (yazım hatası)
  derleme geçer ve endpoint fiilen **kimse tarafından** erişilemez hale gelir.
  Sabit kullanımı disiplini bunu önlüyor; test de yazıldı.
- **Rol bazlıya göre daha fazla kod.** `PermissionRequirement`,
  `PermissionAuthorizationHandler`, `PermissionPolicyProvider` — üç sınıf,
  sıfır yerine. Küçük bir sistemde bu gereksiz karmaşıklık olurdu.

### Yeniden değerlendirme koşulu

- İzin sayısı yüzleri aşarsa: izin gruplama veya jetonda referans tutma.
- Anında iptal şart olursa: Redis tabanlı jeton kara listesi (Faz 3).
- Kaynak bazlı yetkilendirme gerektiğinde: bu kararın üstüne ek katman,
  ayrı ADR.
