<!--
  Bu dosya bir ŞABLONDUR. GitHub'da her yeni PR açtığında içeriği
  otomatik olarak PR açıklamasına yüklenir.
  Amaç: "ne yaptım / neden yaptım / nasıl doğruladım" sorularını
  atlamamak. Doldurulmayan bölümleri sil, boş bırakma.
-->

## What

<!-- Bir veya iki cümle. Ne değişti? -->

## Why

<!--
  En önemli bölüm. Kod NE yaptığını zaten anlatır; NEDEN yaptığını
  sadece sen bilirsin. 6 ay sonra bu satırı okuyan kişi sen olacaksın.
  Hangi problemi çözüyor? Alternatifi neydi, neden onu seçmedim?
-->

## How I verified

<!--
  Kanıt olmadan "çalışıyor" demek yok.
  Örnek: "dotnet test -> 42 passed", "curl ile POST /patients -> 201",
  "k6 ile 200 eşzamanlı istek -> çifte rezervasyon yok"
-->

## What I learned

<!--
  Bu proje bir öğrenme projesi. Bu PR'da öğrendiğin kavramı buraya yaz.
  Faz 10'da bu notlar blog yazılarına ve docs/lessons-learned.md'ye dönüşecek.
  Tıkandığın ve nasıl çıktığın yeri de yaz - en değerli kısım orası.
-->

## Checklist

- [ ] Commit mesajları Conventional Commits formatında (`feat(scope): ...`)
- [ ] Yeni kod için test yazıldı (veya neden gerekmediği `Why` bölümünde açıklandı)
- [ ] `dotnet build` uyarı üretmiyor
- [ ] Sır / bağlantı dizesi / token eklenmedi
- [ ] Mimari bir karar verildiyse `docs/adr/` altına ADR yazıldı
- [ ] README veya ROADMAP güncellenmesi gerekiyorsa güncellendi

## Related

<!--
  Varsa issue bağla: "Closes #12"
  Bu yazım şekli, PR merge edilince issue'yu OTOMATİK kapatır.
  Anahtar kelimeler: Closes, Fixes, Resolves
-->
