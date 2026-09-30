# Geliştirme Günlüğü

Her fazın sonunda ne yapıldığı, **nerede tıkanıldığı** ve ne öğrenildiği buraya
kaydedilir.

## Neden tutuyoruz?

1. **Faz 10'un hammaddesi.** Portfolyo yazıları ve `docs/lessons-learned.md` bu
   dosyalardan üretilecek. *"Outbox pattern öğrendim"* kimseyi ilgilendirmez;
   *"Outbox'ın neden gerektiğini anlamam 2 gün sürdü, şu deney açıkladı"*
   okunur bir yazıdır.
2. **Mülakat malzemesi.** "Bu projede en çok neyle boğuştun?" sorusunun cevabı
   burada yazılı olur.
3. **Kendine geri bildirim.** "Hâlâ net olmayanlar" bölümü, bir sonraki faza
   eksik temelle geçmeni engeller.

## ADR'den farkı ne?

| | `docs/adr/` | `docs/journal/` |
|---|---|---|
| Konu | Tek bir **karar** ve gerekçesi | Bir **dönem** ne oldu |
| Kalıcılık | Değiştirilemez (yenisi yazılır) | Serbestçe güncellenebilir |
| Soru | "Neden böyle yaptık?" | "Ne yaşadık, ne öğrendik?" |

## Adlandırma

```
faz-NN-kisa-ad.md
```

`NN` iki haneli ve sıfır dolgulu (`00`, `01`, ... `10`). Neden sıfır dolgulu:
dosyalar alfabetik sıralandığında `faz-10` ile `faz-2` karışmasın.

## Kayıtlar

| Faz | Dosya | Konu |
|---|---|---|
| 0 | [faz-00-temel.md](faz-00-temel.md) | Git/GitHub disiplini, derleme altyapısı, Docker |
| 1 | *(yazılacak)* | Modüler monolit: Identity + PatientProfile |

## Şablon

Yeni faz için [`faz-NN-template.md`](faz-NN-template.md) dosyasını kopyala.
