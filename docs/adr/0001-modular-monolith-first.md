# ADR-0001: Modüler monolitle başla, mikroservise evril

- **Durum:** Accepted
- **Tarih:** 2026-09-28
- **Karar veren:** Mert Osman Ayhan
- **İlgili:** ROADMAP.md (Faz 1-3)

## Bağlam

MediFlow'un açık amacı **modern backend ekosistemini öğrenmek**: mikroservis
mimarisi, Docker, RabbitMQ, Redis, SignalR, CQRS, observability, CI/CD ve AWS.
Hedef mimari mikroservis.

Kısıtlar:

- **Tek geliştirici.** Mikroservisin en büyük faydası olan "ekiplerin bağımsız
  deploy edebilmesi" bu projede **hiç yok** — çözdüğü asıl örgütsel problem
  mevcut değil.
- **Domain bilgisi sıfır noktasında.** Sağlık domainini (randevu, muayene,
  tahlil, vital takip) henüz modellemedim. Servis sınırları domain sınırlarıdır;
  domaini bilmeden sınır çizmek tahmin yürütmektir.
- **Zaman:** haftada 15-20 saat, son sınıf öğrencisi.
- **Öğrenme hedefi, ürün hedefi değil.** Başarı ölçütü "çalışan sistem" değil,
  "anlaşılmış sistem".

Karar gerektiren soru: **gün 1'de 10 mikroservis mi yazmalı, yoksa tek uygulama
ile başlayıp sonra mı ayırmalı?**

Bu bir karar gerektiriyor çünkü seçim geri alması pahalı: yanlış çizilmiş servis
sınırlarını sonradan düzeltmek, veritabanı bölme ve veri taşıma işi demektir.

## Karar

**Faz 1-2'de modüler monolit ile başlayacağız.** Tek çalıştırılabilir uygulama,
ama içinde net modül sınırları ve **modül başına ayrı `DbContext`**.

Faz 2'den itibaren modüller **strangler fig** yaklaşımıyla tek tek ayrı servis
olarak çıkarılacak ve gateway'e yönlendirme eklenecek. Faz 3'te servisler arası
haberleşme RabbitMQ ile asenkron hale gelecek.

Her ayırma adımı kendi PR'ında ve kendi ADR'sinde gerekçelendirilecek.

## Gerekçe

1. **Servis sınırı, domain öğrenildikten sonra çizilir.** Modüler monolitte
   sınırı yanlış çizersen düzeltme maliyeti bir refactor; mikroserviste aynı
   hata veritabanı bölme, veri taşıma ve dağıtık işlem yönetimi demektir.

2. **Öğrenme yükü sıralanabilir hale gelir.** Faz 1'de yalnızca Clean
   Architecture, CQRS ve EF Core öğrenilir. Ağ hataları, eventual consistency
   ve distributed tracing aynı anda gelmez. Gün 1'de her şeyi birden öğrenmeye
   çalışmak, hiçbirini öğrenmemekle sonuçlanır.

3. **Mikroservisin acısı, faydası anlaşıldıktan sonra çekilirse öğreticidir.**
   Faz 2'de çifte rezervasyon hatasını bilerek üretip Redis lock ile çözeceğiz;
   Faz 3'te RabbitMQ'yu kapatıp Outbox'ın neden gerekli olduğunu göreceğiz. Bu
   sıra, "her şey baştan mikroservis" yaklaşımında mümkün değil — çünkü hangi
   aracın hangi problemi çözdüğü belirsiz kalır.

4. **Evrimin kendisi anlatılabilir bir hikâye.** "Monolitle başladım, şu
   problemle karşılaştım, şu sınırdan ayırdım" demek, "mikroservis yaptım"
   demekten teknik olarak daha güçlü bir ifade.

5. **Hedeften sapma yok.** Nihai mimari yine mikroservis; sadece oraya varma
   yolu kademeli. Öğrenilecek teknoloji listesinden hiçbir şey eksilmiyor.

## Değerlendirilen alternatifler

### Alternatif A: Gün 1'de tam mikroservis (9 servis)

- **Artı:** Hedef mimariye anında ulaşılır; "mikroservis yaptım" denebilir.
- **Artı:** Dağıtık sistem araçları (gateway, broker, tracing) hemen devreye girer.
- **Eksi:** Servis sınırları domain bilgisi olmadan tahminle çizilir. Sonradan
  düzeltmek en pahalı refactor türü.
- **Eksi:** 9 servis × (Dockerfile + CI + migration + config + healthcheck) =
  domaine hiç dokunmadan haftalarca altyapı işi.
- **Eksi:** İlk bug dağıtık bir bug olur. Yeni başlayan için ayıklanması
  neredeyse imkansız; motivasyonu kıran gerçek risk bu.
- **Eksi:** Araçların çözdüğü problem hiç yaşanmadığı için ezberlenir, öğrenilmez.
- **Neden reddedildi:** Öğrenme hedefine zarar veriyor ve en pahalı hata türünü
  (yanlış servis sınırı) en bilgisiz olduğumuz anda yaptırıyor.

### Alternatif B: Monolit olarak kal, hiç ayırma

- **Artı:** En basit, en hızlı geliştirme; tek deploy, tek log akışı.
- **Artı:** Bu ölçekte teknik olarak tamamen yeterli bir mimari.
- **Eksi:** Projenin **asıl amacı** (mikroservis, broker, gateway, dağıtık
  observability öğrenmek) gerçekleşmez.
- **Neden reddedildi:** Amaca aykırı. Bu proje bir ürün değil, bir laboratuvar.

### Alternatif C: İki servisle başla (Identity + geri kalan her şey)

- **Artı:** Orta yol; dağıtık sistemin tadı erken alınır.
- **Eksi:** "Geri kalan her şey" servisi bir çöp kutusu olur; net bir sınırı
  yoktur ve zamanla dağıtık monolite dönüşür.
- **Neden reddedildi:** Belirsiz sınır, yanlış sınırdan kötüdür. Ayrıca modüler
  monolitin sağladığı disiplini vermeden mikroservisin maliyetini getiriyor.

## Sonuçlar

### Olumlu

- Faz 1'de tek `dotnet run` ile çalışan sistem → hızlı geri bildirim döngüsü.
- Modül sınırları, domain öğrenildikçe **bedelsiz** düzeltilebilir.
- Her teknoloji, çözdüğü problem yaşandıktan sonra tanıtılır → kalıcı öğrenme.
- Ayırma adımlarının her biri ayrı bir öğrenme fırsatı ve ayrı bir PR.

### Olumsuz / kabul ettiğimiz bedeller

- **Sonradan ayırma işi gerçek bir maliyet.** Faz 2-3'te veritabanı bölme,
  bağlantı dizesi yönetimi ve senkron çağrıları event'e çevirme işi çıkacak.
  Bunu baştan mikroservis yazsak yapmayacaktık.
- **Modül sınırlarına disiplin gerekiyor.** Tek uygulama içinde bir modülden
  diğerinin sınıfını doğrudan çağırmak teknik olarak mümkün. Bu disiplin
  Faz 6'da NetArchTest ile otomatikleştirilecek; o zamana kadar elle korunacak.
- **"Gerçek mikroservis" deneyimi Faz 3'e kadar gecikiyor.** Dağıtık sistem
  problemlerini ~6 hafta boyunca görmeyeceğiz.
- Modül başına ayrı `DbContext` tutmak, tek veritabanına göre ek iş (paylaşılan
  tablo kolaylığından bilinçli olarak vazgeçiyoruz).

### Yeniden değerlendirme koşulu

Aşağıdakilerden biri olursa bu karar yeni bir ADR ile gözden geçirilir:

- Faz 2 sonunda modül sınırları hâlâ net değilse → ayırmayı ertele, domain
  modellemesine geri dön.
- Bir modül tek başına ölçeklenme ihtiyacı gösterirse (ör. VitalsMonitoring
  sürekli veri akışı nedeniyle) → o modülü sıradan bağımsız olarak öne al.
- Proje ekip çalışmasına dönüşürse → bağımsız deploy ihtiyacı doğar, ayırma
  hızlanır.
