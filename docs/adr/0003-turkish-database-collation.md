# ADR-0003: Veritabanı collation'ı `Turkish_100_CI_AS_SC`

- **Durum:** Accepted
- **Tarih:** 2026-09-30
- **Karar veren:** Mert Osman Ayhan
- **İlgili:** Faz 0 journal (açık iş olarak kaydedilmişti), PR #12

## Bağlam

MSSQL container'ının sunucu varsayılanı `SQL_Latin1_General_CP1_CI_AS`. Faz 0'da
bu tespit edildi ve "EF Core şemayı oluşturmadan önce karar verilmeli" diye
açık iş olarak kaydedildi.

Sistem Türkçe konuşan bir sağlık kuruluşu için: hasta adları, soyadları ve
alerjen adları Türkçe. Türkçe'nin iki özel kuralı var:

1. **Harf eşleşmesi:** `i` harfinin büyüğü `İ`, `I` harfinin küçüğü `ı`'dır.
   Latin1 bunu bilmez ve `i` ↔ `I` eşler.
2. **Alfabe sırası:** `Ç` C'den sonra, `Ş` S'den sonra, `İ` I'dan sonra gelir.

Karar gerektiriyor çünkü **collation'ı sonradan değiştirmek pahalıdır**:
indekslerin yeniden kurulması ve metin sütunlarının dönüştürülmesi gerekir.
Veri girmeye başlamadan karar verilmesi gerekiyordu.

Sunucuda kullanılabilir Türkçe collation'lar ölçüldü (`sys.fn_helpcollations`):
`Turkish_CI_AS` (sürümsüz), `Turkish_100_CI_AS` ve bunların `_KS`, `_WS`, `_SC`,
`_UTF8` varyantları.

## Karar

Veritabanı seviyesinde **`Turkish_100_CI_AS_SC`** kullanılıyor.

EF Core tarafında `modelBuilder.UseCollation("Turkish_100_CI_AS_SC")` ile
tanımlandı; tüm `nvarchar` sütunlar bunu devralıyor.

Bileşenler:

| Parça | Anlamı | Gerekçe |
|---|---|---|
| `Turkish` | Türk alfabesi kuralları | Asıl sebep |
| `100` | Collation sürümü 100 | Sürümsüz olandan daha güncel Unicode kuralları |
| `CI` | Case-insensitive | `mehmet` arayan `Mehmet`'i bulsun |
| `AS` | Accent-**sensitive** | `Cetin` ≠ `Çetin` - iki farklı soyad, karıştırılamaz |
| `SC` | Supplementary character desteği | `LEN`/`SUBSTRING` temel düzlem dışı karakterlerde doğru çalışsın |

`_UTF8` varyantı **alınmadı**: UTF-8 depolama `varchar` için anlamlıdır, biz
`nvarchar` kullanıyoruz (EF Core'un metin varsayılanı). Fayda yok, davranış
sürprizi riski var.

## Gerekçe

Karar vermeden önce iki davranış SQL Server üzerinde ölçüldü:

**1. Harf eşleşmesi** - kullanıcı `ismail` yazıyor, kayıt `İSMAİL`:

| Collation | Sonuç |
|---|---|
| `Turkish_100_CI_AS_SC` | **eşleşti** |
| `SQL_Latin1_General_CP1_CI_AS` | **eşleşmedi** |

Yani Latin1'de kullanıcı kaydı **bulamıyor**. Somut, kullanıcıya yansıyan bir hata.

**2. Sıralama** - `Cebeci, Çelik, Demir, Doğan, Sarı, Şahin` listesi:

- Türkçe: `Cebeci, Çelik, Demir, Doğan, Sarı, Şahin` ✅
- Latin1: `Cebeci, Çelik, Demir, Doğan, Şahin, Sarı` ❌ (Ş, S'den önce)

Not: `Ç`/`C` sıralaması bu örnekte iki collation'da da aynı çıktı; beklenen fark
orada oluşmadı. Farkı `S`/`Ş` çifti gösterdi. Yani iddianın bir kısmı
doğrulandı, bir kısmı doğrulanmadı - ikisi de kayda geçti.

## Değerlendirilen alternatifler

### Alternatif A: Sunucu varsayılanında kal (`SQL_Latin1_General_CP1_CI_AS`)

- **Artı:** Hiç iş yok; sunucu, diğer veritabanları ve `tempdb` ile aynı
  collation - `tempdb`'deki geçici tablolarla karşılaştırmalarda çakışma olmaz.
- **Eksi:** Yukarıdaki iki ölçümde de yanlış davranıyor. Hasta arama, Türkçe
  bir sağlık sisteminin en temel işlevi.
- **Neden reddedildi:** Ölçülmüş, kullanıcıya yansıyan bir hata var.

### Alternatif B: Sadece isim sütunlarına sütun bazlı collation

- **Artı:** Veritabanının geri kalanı nötr kalır; `tempdb` çakışması riski daha az.
- **Eksi:** Yeni bir metin sütunu eklerken **unutmaya açık**. Bir sütunun
  Türkçe, diğerinin Latin1 olması, teşhisi zor tutarsızlıklar üretir.
- **Eksi:** Her sütunda tekrar - Faz 0'da merkezileştirme prensibine aykırı.
- **Neden reddedildi:** Unutulabilen bir kural, kural değildir. Yine de tam
  bir kaçış yolu olarak not edildi (aşağıya bakınız).

### Alternatif C: `Turkish_100_CI_AI_SC` (accent-insensitive)

- **Artı:** `Cetin` yazan kullanıcı `Çetin`'i bulur - klavye kolaylığı.
- **Eksi:** `Cetin` ve `Çetin` **farklı soyadlardır**. Sağlık verisinde iki
  farklı kişiyi aynı saymak kabul edilemez bir risk.
- **Neden reddedildi:** Hasta kimliği doğruluğu, arama kolaylığından önce gelir.
  Aksan duyarsız arama gerekirse sorgu bazında `COLLATE` ile yapılabilir.

## Sonuçlar

### Olumlu

- Türkçe arama ve sıralama doğru çalışıyor (ölçüldü).
- Tek yerde tanımlı; yeni sütunlar otomatik devralıyor.
- `Cetin`/`Çetin` ayrımı korunuyor - hasta kimliği güvenliği.

### Olumsuz / kabul ettiğimiz bedeller

- **Türkçe olmayan veri için kurallar şaşırtıcı olur.** Sistem bir gün
  İngilizce isimler de tutarsa, `i`/`I` eşleşmemesi (Türkçe'de doğru) o veri
  için yanlış davranış olur.
- **`tempdb` collation çakışması riski.** `tempdb` sunucu varsayılanında
  (Latin1); geçici tablolarla doğrudan metin karşılaştırması yapan bir sorgu
  `COLLATE` hatası verebilir. Karşılaşınca sorgu bazında `COLLATE` ile çözülür.
- **Collation'ı sonradan değiştirmek pahalı.** Bu karar pratikte kalıcı.
- **C# tarafını ÇÖZMEZ.** Veritabanı collation'ı yalnızca veritabanı içi
  karşılaştırmaları etkiler. `Patient.AddAllergy` içindeki
  `StringComparison.OrdinalIgnoreCase` Türkçe kurallarını uygulamıyor ve bu
  ayrı bir açık konu olarak duruyor.

### Yeniden değerlendirme koşulu

- Sistem Türkçe dışında bir dil için de kullanılacaksa: sütun bazlı collation
  veya dil başına ayrı alan yaklaşımına dönülür.
- `tempdb` çakışmaları sorgu bazında çözülemeyecek kadar sık hale gelirse.
- C# tarafındaki Türkçe metin karşılaştırması ayrı bir ADR ile ele alınacak.
