# ADR-0002: Modüller `src/Modules/`, ayrılan servisler `src/Services/`

- **Durum:** Accepted
- **Tarih:** 2026-09-30
- **Karar veren:** Mert Osman Ayhan
- **İlgili:** ADR-0001 (modüler monolitle başla), PR #10

## Bağlam

ADR-0001'de "tek çalıştırılabilir uygulama, ama içinde net modül sınırları ve
modül başına ayrı veri erişimi" kararı alındı. Faz 1'e başlarken bunun klasör
yapısına nasıl yansıyacağı belirsizdi.

ROADMAP.md'de `src/Services/<Ad>/<Ad>.Api|Application|Domain|Infrastructure`
şeklinde bir ağaç yazılıydı. Ama o ağaç **nihai** durumu (her modül kendi
servisine ayrılmış hali) anlatıyor; Faz 1'de henüz `.Api` projesi modül başına
yok, tek bir ortak host var.

Kısıt: yapı, ayırma işlemini **kolaylaştırmalı** ve o günkü değişikliğin
niteliğini **görünür** kılmalı. Ayrıca bugünkü durumu yanlış tanıtmamalı — bir
klasörün adı "Services" olup içindekiler servis değilse, okuyanı yanıltır.

## Karar

İki klasör, iki farklı anlam:

```
src/
├── Host/
│   └── MediFlow.Api                 tek çalıştırılabilir uygulama
├── Modules/                         tek uygulama İÇİNDE yaşayan modüller
│   └── PatientProfile/
│       ├── ...Domain
│       ├── ...Application
│       └── ...Infrastructure
├── Services/                        kendi başına çalışan servisler (Faz 2+)
└── BuildingBlocks/                  teknik altyapı (iş kuralı YOK)
```

Bir modül ayrı servise çıkarıldığında klasörü `Modules/` altından `Services/`
altına **taşınır** ve kendi `.Api` projesi eklenir.

Her modül kendi veritabanı **şemasında** çalışır (`PatientProfile` şeması).

## Gerekçe

1. **Ad, içeriği doğru tanıtır.** `Modules/PatientProfile` "bu tek uygulamanın
   bir parçası" der; `Services/Scheduling` "bu kendi başına çalışır" der.
   Tek klasör kullanıp adına "Services" demek, 6 hafta boyunca yanlış bilgi
   vermek olurdu.

2. **Taşıma işlemi evrimi görünür kılar.** Faz 2'de `git mv` ile yapılan bir
   taşıma, commit geçmişinde "şu modül şu tarihte servise dönüştü" olarak
   okunur. Aynı klasörde kalsa bu dönüşüm geçmişte hiç görünmezdi - ve bu
   projenin anlatacağı hikâyenin tam olarak o dönüşüm olduğu düşünülürse,
   izini bırakmak değerli.

3. **Şema ayrımı ayırmayı ucuzlaştırır.** Tablolar zaten `PatientProfile`
   şemasında toplandığı için, o modülü ayrı bir veritabanına taşımak şemayı
   taşımak demek. Ortak şemada iç içe geçmiş tablolardan ayıklamaya kıyasla
   çok daha küçük bir iş.

4. **Modül başına `.Api` projesi şimdi gereksiz.** Tek host var; her modüle boş
   bir web projesi eklemek YAGNI olurdu. Ayırma günü eklenecek.

## Değerlendirilen alternatifler

### Alternatif A: Baştan `src/Services/` kullan, taşıma yapma

- **Artı:** Tek klasör, taşıma işi yok, ROADMAP'teki ağaçla birebir uyumlu.
- **Eksi:** Klasör adı 6 hafta boyunca yalan söyler - içindekiler servis değil.
- **Eksi:** Modülden servise geçiş commit geçmişinde hiç görünmez.
- **Neden reddedildi:** Yapının okuyucuya doğru bilgi vermesi, taşıma
  zahmetinden daha değerli. Taşıma tek bir `git mv` commit'i.

### Alternatif B: Tek proje içinde klasör bazlı modüller (ayrı .csproj yok)

- **Artı:** En az proje sayısı, en hızlı derleme.
- **Eksi:** Modül sınırını hiçbir şey ZORLAMAZ. Bir modülden diğerinin
  sınıfını çağırmak derleyici açısından tamamen serbest olur.
- **Eksi:** Ayırma günü sınıfları elle ayıklamak gerekir.
- **Neden reddedildi:** Sınır, ancak derleyici tarafından uygulanıyorsa
  sınırdır. Ayrı projeler bunu bedava sağlıyor.

## Sonuçlar

### Olumlu

- Klasör yapısı bugünkü gerçeği doğru anlatıyor.
- Ayırma işlemi küçük ve görünür bir adım haline geliyor.
- Modül sınırları proje referanslarıyla derleyici tarafından uygulanıyor.

### Olumsuz / kabul ettiğimiz bedeller

- **ROADMAP.md'deki ağaçla geçici bir tutarsızlık var.** Orada nihai yapı
  yazılı; okuyan kişi Faz 1'de `Services/` klasörünü boş görüp şaşırabilir.
  (Bu ADR o boşluğun cevabı.)
- Faz 2'de taşıma sırasında proje referans yolları güncellenecek - küçük ama
  gerçek bir iş.
- Proje sayısı modül başına 3 (+ ayrılınca 4). Derleme süresi tek projeye göre
  daha uzun.
- `Services/` klasörü Faz 2'ye kadar boş duracak (`.gitkeep` ile tutuluyor).

### Yeniden değerlendirme koşulu

- Modül sayısı 3'ü geçip derleme süresi rahatsız edici hale gelirse proje
  granülerliği tekrar düşünülür.
- Faz 2'de ilk ayırma denemesi taşımanın beklenenden pahalı olduğunu
  gösterirse, kalan modüller için yaklaşım gözden geçirilir.
