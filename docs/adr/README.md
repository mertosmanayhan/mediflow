# Architecture Decision Records (ADR)

Bu klasör, projede alınan **mimari kararları ve gerekçelerini** kaydeder.

## Neden?

Kod *ne* yaptığını anlatır; *neden* öyle yaptığını anlatmaz. Altı ay sonra
"neden mikroservis yerine monolitle başlamışım?" sorusunun cevabı burada olacak.

## En önemli kural: ADR değiştirilmez

> Kabul edilmiş bir ADR **asla düzenlenmez.** Karar değişirse **yeni** bir ADR
> yazılır ve eskisinin durumu `Superseded by ADR-XXXX` olarak işaretlenir.

Değerli olan şey sadece "şu an ne yapıyoruz" değil, **"nasıl buraya geldik"**.
Yanlış çıkan bir kararın kaydı, doğru kararın kaydından daha öğreticidir.

## Kurallar

- Dosya adı: `NNNN-kisa-baslik.md` (4 haneli, sıfır dolgulu, sırayla artan)
- Numara atlanmaz, silinmez, yeniden kullanılmaz
- Kısa tut: 1-2 sayfa. Uzun ADR okunmaz, okunmayan ADR yoktur
- `0000-template.md` dosyasını kopyalayarak başla

## Durum (Status) değerleri

| Status | Anlamı |
|---|---|
| `Proposed` | Tartışılıyor, henüz uygulanmıyor |
| `Accepted` | Yürürlükte |
| `Deprecated` | Artık geçerli değil, yerine yenisi yok |
| `Superseded by ADR-XXXX` | Yerine başka bir karar geçti |

## Ne zaman ADR yazılır?

Değiştirmesi **pahalı** olan kararlar için:

- ✅ Mimari desen seçimi (monolit / mikroservis, CQRS)
- ✅ Veri deposu seçimi (MSSQL / MongoDB / Redis)
- ✅ Servisler arası haberleşme biçimi (senkron HTTP / asenkron event)
- ✅ Bir kütüphane/framework seçimi ve alternatiflerinin reddi
- ✅ Bilinçli olarak devre dışı bırakılan bir kural (ör. bir analizör kuralı)
- ❌ "Bu metodun adı ne olsun" (geri alması bedava, PR'da tartışılır)

## Kayıtlar

| # | Başlık | Durum |
|---|---|---|
| [0001](0001-modular-monolith-first.md) | Modüler monolitle başla, mikroservise evril | Accepted |
