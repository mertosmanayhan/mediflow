# MediFlow — Yol Haritası

> Amaç: Tek bir gerçekçi proje üzerinden modern backend ekosistemini (ASP.NET Core,
> mikroservis, Docker, RabbitMQ, Redis, SignalR, CQRS, observability, CI/CD, AWS)
> **uçtan uca, canlıda çalışan** halde öğrenmek ve portfolyoya koymak.

---

## ⚠️ Bu dokümanı nasıl okumalı (önce bunu oku)

Bu bir **harita**, yapılacaklar listesi değil. Tamamını anlamaya çalışmak
gereksiz ve moral bozucu — haritayı sürekli okumazsın, yolu kaybettiğinde
okursun.

**Kurallar:**

1. **Sadece içinde bulunduğun fazı oku.** Diğer fazlar şu an senin problemin değil.
   Faz 3'teki "Outbox pattern" bugün anlamsız gelmeli — 6 hafta sonra ihtiyacın
   olduğunda 20 dakikada öğreneceksin.
2. **Hiçbir an 2'den fazla yeni kavramla uğraşmayacaksın.** Faz 1'in tamamı tek
   bir fikirden oluşuyor: "istek gelir → handler çalışır → veritabanına yazar."
3. **Listedeki her şeyi aynı anda bilen kimse yok** — 10 yıllık geliştirici de
   Terraform'u ilk kullanırken dokümana bakar. Amaç bilmek değil, *bilmediğinde
   nasıl öğreneceğini* bilmek.
4. **Takvim kayarsa sorun değil.** Süreler tahmin; önemli olan **sıra**. Bir faz
   3 hafta sürerse o fazı 3 haftada yaparsın, atlamazsın.
5. **Her fazın sonunda çalışan bir şey olacak.** Yani 16 hafta bekleyip sonunda
   bir şey görmeyeceksin — 1. haftanın sonunda bile Swagger'dan çalışan bir API'n olacak.

**Panikleyince yapılacak:** Bu bölümü tekrar oku, içinde bulunduğun fazın
başlığına dön, oradaki ilk işaretlenmemiş `[ ]` maddesini yap. Hepsi bu.

---

## 1. Proje: MediFlow

**Bir klinik zinciri için dijital sağlık platformu.**

Hasta randevu alır → doktor muayene eder → tahlil ister → sonuç yüklenir →
kritik değer varsa doktora **anlık** alarm gider → yatan hastaların vital
verileri canlı nöbetçi ekranında akar.

### Neden bu domain?

Bu akışlar öğrenmek istediğin teknolojileri **yapay olarak değil, zorunlu olarak** gerektiriyor:

| İhtiyaç | Teknolojiyi zorunlu kılan gerçek problem |
|---|---|
| Aynı slota 2 kişi aynı anda randevu alıyor | Redis distributed lock + optimistic concurrency |
| Randevu alındı → SMS + e-posta + takvim güncelle | RabbitMQ (async, servisler birbirini beklemesin) |
| Tahlilde kritik değer → doktor ekranında anında uyarı | SignalR |
| "Randevumu iptal et" → slot boşalt + ön ödeme iade + bildirim | Saga / compensation |
| DB'ye yazdım ama event gitmedi | Outbox pattern |
| Doktor takvimi saniyede 50 kez sorgulanıyor | CQRS + Redis cache (read/write ayrımı) |
| Hasta adına göre fuzzy arama, istatistik ekranı | Elasticsearch read model |
| KVKK: kişisel sağlık verisi | Şifreleme, audit log, permission-based authz |

### Bounded Context'ler (servisler)

| # | Servis | Sorumluluk | Veritabanı |
|---|---|---|---|
| 1 | **Identity** | Kayıt, login, JWT + refresh token, rol & permission, 2FA | MSSQL |
| 2 | **PatientProfile** | Demografi, kronik hastalık, alerji, ilaç listesi (DDD aggregate) | MSSQL |
| 3 | **Scheduling** | Doktor müsaitliği, slot üretimi, randevu al/iptal/ertele | MSSQL + Redis |
| 4 | **Encounter** | Muayene kaydı, ICD-10 tanı, reçete, doktor notu | MSSQL |
| 5 | **Laboratory** | Tahlil isteği, sonuç (PDF → S3), kritik değer tespiti | MSSQL + S3 |
| 6 | **Notification** | E-posta/SMS/in-app, şablonlar, SignalR hub | MongoDB |
| 7 | **VitalsMonitoring** | Simüle cihazlardan vital akışı, eşik alarmı | Redis Stream + MSSQL |
| 8 | **Reporting** | Arama + istatistik read model, Kibana dashboard | Elasticsearch |
| 9 | **Payment** (mock) | Ön ödeme al/iade — sadece saga öğrenmek için | MSSQL |
| 10 | **Gateway** | YARP: routing, auth, rate limit, aggregation | — |

> **Polyglot persistence bilinçli bir tercih:** MSSQL (transactional), MongoDB
> (şemasız log/şablon), Redis (cache + lock + stream), Elasticsearch (arama),
> S3 (dosya). Her birini "neden burada?" sorusunu cevaplayarak kullanacaksın.

---

## 2. Mimari Karar: Modüler Monolit → Mikroservis

**Gün 1'de 10 mikroservis yazmayacaksın.** Bu en yaygın öğrenci hatası: dağıtık
sistemin acısını (network, eventual consistency, distributed tracing) domain'i
öğrenmeden çekmek.

```
Faz 1-2:  Modüler Monolit (tek API, modül klasörleri, ayrı DbContext'ler)
             ↓ strangler fig: modülü çıkar, gateway'e route ekle
Faz 3-6:  Mikroservisler (RabbitMQ ile async, Outbox, Saga)
Faz 7-8:  AWS'de canlı (EC2 Compose → ECS Fargate / k3s)
```

Bu evrimin kendisi mülakatta anlatılacak en değerli hikâye. Her adımda
`docs/adr/` altına **ADR** (Architecture Decision Record) yazacaksın.

### Her servisin iç mimarisi (Clean Architecture)

```
src/Services/Scheduling/
├── Scheduling.Api/              # Minimal API endpoints, DI, middleware
├── Scheduling.Application/      # CQRS: Commands, Queries, Handlers, Validators, DTO
├── Scheduling.Domain/           # Entity, Aggregate, ValueObject, DomainEvent (0 bağımlılık)
└── Scheduling.Infrastructure/   # EF Core, Repository, Redis, MassTransit, Outbox
tests/Services/Scheduling/
├── Scheduling.UnitTests/
└── Scheduling.IntegrationTests/ # Testcontainers
```

Bağımlılık yönü: `Api → Application → Domain ← Infrastructure`
Domain katmanı hiçbir NuGet paketine bağlı olmaz (MediatR hariç, o da opsiyonel).

---

## 3. Teknoloji Haritası

| Katman | Teknoloji | Hangi fazda |
|---|---|---|
| Runtime | **.NET 10 (LTS)**, C# 14 | Faz 0 |
| API | ASP.NET Core Minimal API, OpenAPI/Scalar | Faz 1 |
| Pattern | **MediatR**, CQRS, FluentValidation, Result pattern, Specification | Faz 1 |
| ORM | EF Core 10, Migrations, Dapper (ağır query'ler için) | Faz 1 |
| Auth | JWT + Refresh Token rotation, ASP.NET Identity, permission-based policy | Faz 1 |
| Cache/Lock | **Redis** (StackExchange.Redis), HybridCache, RedLock.net | Faz 2 |
| Gateway | **YARP** (Ocelot alternatifi — YARP daha güncel) | Faz 2 |
| Messaging | **RabbitMQ** + **MassTransit**, Outbox, Saga, DLQ, retry | Faz 3-4 |
| Realtime | **SignalR** + Redis backplane (ölçeklenebilir hub) | Faz 3 |
| Resilience | Polly v8 / Microsoft.Extensions.Resilience, circuit breaker | Faz 4 |
| Logging | Serilog → **Seq** (dev) / Elasticsearch+Kibana (prod), correlation ID | Faz 5 |
| Tracing | **OpenTelemetry** → Jaeger / Grafana Tempo | Faz 5 |
| Metrics | Prometheus + **Grafana**, custom metrics, HealthChecks UI | Faz 5 |
| Test | xUnit v3, FluentAssertions, NSubstitute, **Testcontainers**, Stryker.NET, k6 | Faz 6 |
| Container | **Docker**, multi-stage build, Compose profiles, **Portainer** | Faz 0-2 |
| CI/CD | **GitHub Actions**: matrix, path filter, reusable workflow, GHCR, OIDC | Faz 0 → sürekli |
| Kalite | CodeQL, Trivy, Dependabot, SonarCloud, coverage gate | Faz 6 |
| IaC | **Terraform** (AWS) | Faz 7 |
| Cloud | EC2, RDS, ElastiCache, Amazon MQ, S3, ALB, ECS Fargate, Secrets Manager | Faz 7-8 |
| Frontend (ops.) | React 19 + TypeScript + Vite + SignalR client (canlı nöbetçi ekranı) | Faz 9 |

---

## 4. Repo & Git Stratejisi

### Monorepo
Tek repo, `dotnet` solution + path-filtered CI. Sebep: cross-service refactor tek
PR'da yapılır, CI sadece değişen servisi build eder. Çoklu repo'nun acısı bu
ölçekte gereksiz.

### Repo yapısı
```
mediflow/
├── .github/
│   ├── workflows/          # ci.yml, cd-staging.yml, cd-prod.yml, reusable-service-ci.yml
│   ├── ISSUE_TEMPLATE/
│   ├── PULL_REQUEST_TEMPLATE.md
│   ├── CODEOWNERS
│   └── dependabot.yml
├── docs/
│   ├── adr/                # 0001-modular-monolith-first.md ...
│   ├── architecture/       # C4 diyagramları (Structurizr / Mermaid)
│   └── runbook/            # "RabbitMQ doldu ne yapmalı" tipi operasyon notları
├── src/
│   ├── Gateway/
│   ├── Services/
│   ├── BuildingBlocks/     # Shared kernel: Common, EventBus, Observability, Auth
│   └── Web/                # React dashboard
├── tests/
├── deploy/
│   ├── docker/             # compose.yml, compose.override.yml, compose.prod.yml
│   ├── terraform/          # modules/, envs/dev, envs/prod
│   └── k8s/                # Helm chart (Faz 8)
├── tools/                  # seed data, load test scripts (k6), device simulator
├── Directory.Build.props   # merkezi TargetFramework, Nullable, analyzer, warnaserror
├── Directory.Packages.props # Central Package Management (tek yerden versiyon)
├── .editorconfig
└── MediFlow.slnx
```

### Branş modeli: Trunk-based + kısa ömürlü dallar
```
main  ← korumalı, her merge otomatik staging'e deploy
  ├── feat/scheduling-slot-locking
  ├── fix/jwt-refresh-race
  └── chore/upgrade-masstransit
```
Kural: bir dal **max 2 gün** yaşar, **max ~400 satır** diff. Uzun sürecek işi
feature flag arkasına al, yarım da olsa merge et.

### "Paralel yürütmek" ne demek (istediğin şey bu)
- **`git worktree`**: aynı repo'yu 2-3 klasörde aynı anda açık tut. Bir dalda
  Scheduling yazarken, CI kırıldığında `git worktree add ../mediflow-hotfix main`
  ile stash'lemeden hotfix çıkar.
- **Conventional Commits** (`feat(scheduling): add slot locking`) → otomatik
  CHANGELOG + semantic version.
- **Branch protection**: PR zorunlu, CI yeşil olmadan merge yok, squash merge.
- **GitHub Projects** board: Backlog → In Progress → In Review → Done. Her
  issue bir dal, her dal bir PR. Kendine sprint kur (1 hafta).
- **Kendi PR'ını incele**: PR açtıktan sonra diff'i satır satır oku, kendine
  yorum yaz. Bu alışkanlık mülakatta fark yaratır.
- Paralel CI: matrix strategy ile 9 servisi aynı anda build/test et.

---

## 5. 16 Haftalık Faz Planı

Varsayım: haftada **15-20 saat**. Son sınıfsın, dersler var — takvim kayarsa
sorun değil, **faz sırası** önemli.

---

### Faz 0 — Temel (3-4 gün)

**Öğrenilen:** Solution mimarisi, Docker Compose, GitHub Actions temeli, Git disiplini.

- [ ] GitHub'da **public** repo (`mediflow`) — public çünkü: sınırsız Actions dakikası + portfolyo
- [ ] `GitHub Student Developer Pack` başvurusu (Copilot Pro, JetBrains, cloud kredileri ücretsiz)
- [ ] Solution iskeleti, `Directory.Build.props` + Central Package Management
- [ ] `.editorconfig` + analyzer'lar + `TreatWarningsAsErrors`
- [ ] `docker compose up` ile MSSQL + Redis + RabbitMQ + Seq ayağa kalkıyor
- [ ] `ci.yml`: build + test + format check
- [ ] Branch protection, PR template, CODEOWNERS, Dependabot
- [ ] ADR-0001: "Neden modüler monolitle başlıyorum"

**DoD:** Repo'yu sıfırdan klonlayan biri `docker compose up` + `dotnet run` ile
çalıştırabiliyor. README'de bu 2 komut yazıyor.

---

### Faz 1 — Modüler Monolit: Identity + PatientProfile (2 hafta)

**Öğrenilen:** Clean Architecture, CQRS + MediatR, EF Core, JWT, FluentValidation, unit test.

- [ ] Domain modelleme: `Patient` aggregate, `Allergy`/`ChronicCondition` value object'leri
- [ ] CQRS: `CreatePatientCommand`, `GetPatientByIdQuery` + handler'lar
- [ ] MediatR pipeline behavior'ları: **Validation, Logging, Transaction, Performance**
- [ ] Result pattern (exception'la flow control yapma)
- [ ] EF Core: migration, seed, global query filter (soft delete), audit alanları
- [ ] JWT: access (15 dk) + refresh token (7 gün, **rotation + reuse detection**)
- [ ] Role + **permission-based authorization** (`[HasPermission("patient.read")]`)
- [ ] Serilog + correlation ID middleware, global exception handler (ProblemDetails)
- [ ] Unit testler: domain kuralları %100, handler'lar
- [ ] CI'a coverage raporu ekle

**DoD:** Swagger/Scalar'dan kayıt ol → login → token ile hasta oluştur akışı çalışıyor.
25+ unit test yeşil.

**Tuzak:** Anemic domain model. `Patient.SetName(x)` yerine iş kuralını içeren
`Patient.UpdateDemographics(...)` yaz; setter'lar `private`.

---

### Faz 2 — İlk Ayrıştırma: Scheduling + Gateway + Redis (2 hafta)

**Öğrenilen:** Servis ayırma, YARP, Redis cache & distributed lock, concurrency, idempotency.

- [ ] `Scheduling` servisini ayrı proje + ayrı DB + ayrı container olarak çıkar
- [ ] **YARP Gateway**: route config, JWT doğrulama gateway'de, rate limiting
- [ ] Doktor müsaitlik kuralları → slot üretimi (recurring schedule)
- [ ] **Concurrency problemi**: 2 istek aynı slota → `RowVersion` + Redis lock ile çöz.
      Önce **bilinçli olarak bug'ı üret**, k6 ile 100 paralel istek at, çifte
      rezervasyonu gör, sonra düzelt. (En öğretici adım.)
- [ ] Redis cache: takvim query'si, cache invalidation stratejisi, HybridCache
- [ ] **Idempotency key** middleware (aynı POST 2 kez gelirse tek randevu)
- [ ] Servisler arası senkron çağrı: Scheduling → Patient (HTTP + typed client)
- [ ] **Portainer** ekle, container'ları oradan izle
- [ ] CI: path filter (`src/Services/Scheduling/**` değişince sadece o build olsun)

**DoD:** Gateway üzerinden randevu alınıyor. k6 load testinde 200 eşzamanlı
istekte **hiç** çifte rezervasyon yok.

---

### Faz 3 — Asenkron: RabbitMQ + Outbox + SignalR (2 hafta)

**Öğrenilen:** Event-driven mimari, MassTransit, Outbox, idempotent consumer, SignalR.

- [ ] RabbitMQ kavramları: exchange, queue, binding, routing key, prefetch, ack
      (Management UI'da elle deneyerek öğren, sonra koda geç)
- [ ] MassTransit kurulumu, `IPublishEndpoint`, consumer, retry/redelivery politikası
- [ ] **Integration event sözleşmeleri** `BuildingBlocks.EventBus`'ta (versiyonlama!)
- [ ] **Outbox pattern**: `AppointmentCreated` event'i DB transaction'ıyla atomik yazılıyor
- [ ] **Idempotent consumer**: aynı event 2 kez gelirse 2 SMS gitmiyor (inbox tablosu)
- [ ] `Notification` servisi: MongoDB, şablon motoru, MailHog ile e-posta testi
- [ ] **SignalR hub**: hasta randevu alınca doktorun ekranında anlık kart düşüyor
- [ ] SignalR + **Redis backplane** (2 instance çalıştır, mesajın ikisine de gittiğini kanıtla)
- [ ] `Laboratory` servisi: sonuç yükle → S3/MinIO → `LabResultReady` event
- [ ] Kritik değer → doktora **anlık** SignalR alarmı

**DoD:** Randevu al → 2 saniye içinde e-posta MailHog'da + bildirim MongoDB'de +
kart tarayıcıda göründü. RabbitMQ'yu `docker stop` et, API hâlâ 200 dönüyor
(Outbox sayesinde), RabbitMQ açılınca event'ler akıyor.

---

### Faz 4 — Dağıtık Sistem Gerçekleri: Saga + Resilience (1 hafta)

**Öğrenilen:** Eventual consistency, saga/compensation, circuit breaker, DLQ.

- [ ] Randevu iptal akışı → **MassTransit Saga** (state machine):
      `SlotReleased` → `PaymentRefunded` → `NotificationSent`; adım patlarsa **compensation**
- [ ] `Payment` mock servisi (bilinçli olarak %20 hata fırlatsın)
- [ ] Polly: retry + exponential backoff + jitter, circuit breaker, timeout, fallback
- [ ] **Dead Letter Queue** yönetimi + DLQ'dan replay endpoint'i
- [ ] Chaos testi: bir servisi öldür, sistemin davranışını dokümante et
- [ ] ADR: "Neden eventual consistency kabul edilebilir, nerede edilemez"

**DoD:** Payment servisi kapalıyken iptal isteği → saga bekliyor, servis açılınca
tamamlanıyor. Hiçbir para/slot tutarsızlığı yok.

---

### Faz 5 — Observability (2 hafta)

**Öğrenilen:** "Prod'da ne oluyor?" sorusunun cevabı. Mülakatta en az sorulan, en çok fark yaratan konu.

- [ ] Structured logging her yerde, log seviyesi disiplini, PII maskeleme (KVKK)
- [ ] **OpenTelemetry**: distributed tracing — gateway'den DB'ye tek trace ID
- [ ] Jaeger'da bir randevu isteğinin 6 servisten geçişini **gör**
- [ ] Prometheus metrikleri: RED (Rate/Errors/Duration) + custom business metric
      (`mediflow_appointments_created_total`)
- [ ] **Grafana dashboard**: servis sağlığı, kuyruk derinliği, p95 latency, hata oranı
- [ ] `HealthChecks` + HealthChecksUI (liveness vs readiness ayrımı)
- [ ] Alerting: kuyruk 1000'i geçerse / hata oranı %5'i geçerse alarm
- [ ] ELK: log aggregation, Kibana'da correlation ID ile arama

**DoD:** Bilerek bir bug ekle, sadece Grafana + Jaeger + Kibana'ya bakarak
15 dakikada kök nedeni bul. Bu süreci `docs/runbook/` altına yaz.

---

### Faz 6 — Test Derinliği & Kalite Kapıları (1.5 hafta)

**Öğrenilen:** Testcontainers, contract test, mutation test, load test, CI kalite kapıları.

- [ ] **Testcontainers**: gerçek MSSQL + Redis + RabbitMQ container'ında integration test
- [ ] `WebApplicationFactory` ile in-memory API testleri (test için auth bypass)
- [ ] Respawn ile testler arası DB temizliği
- [ ] Architecture testleri (NetArchTest): "Domain, Infrastructure'a referans veremez"
- [ ] **Mutation testing** (Stryker.NET) — testlerinin gerçekten test ettiğini kanıtla
- [ ] **k6** load test senaryoları + sonuç raporu (`docs/performance/`)
- [ ] CI kalite kapıları: coverage < %70 → fail, CodeQL, **Trivy** image scan, SonarCloud
- [ ] Reusable workflow'a refactor: 9 servis aynı workflow'u parametreyle çağırıyor

**DoD:** `main`'e coverage düşüren veya CVE içeren image ile merge edilemiyor.
CI tam paralel çalışıyor, süre < 6 dk.

---

### Faz 7 — AWS'de Canlı v1 (2 hafta) 🚀

**Öğrenilen:** Terraform, EC2, güvenlik grupları, secrets, HTTPS, GitHub Actions → AWS OIDC.

- [ ] AWS hesabı: **IAM user değil**, root'u kilitle, MFA, **Budgets alarm $10**
- [ ] `aws` CLI + `terraform` + `gh` CLI kur
- [ ] Terraform: VPC, subnet, security group, EC2 (t3.medium), Elastic IP, S3, IAM role
- [ ] `terraform fmt/validate/plan` CI'da; `apply` manuel approval'lı
- [ ] EC2'de production Compose: 6 servis + MSSQL Express + Redis + RabbitMQ
- [ ] **Caddy** reverse proxy → otomatik Let's Encrypt HTTPS (nginx'ten çok daha kolay)
- [ ] Domain: ~$10/yıl `.com` veya ücretsiz DuckDNS; DNS'i Cloudflare'de tut
- [ ] Secrets: AWS Secrets Manager / SSM Parameter Store (asla `.env` commit yok)
- [ ] **GitHub Actions → AWS OIDC** (uzun ömürlü access key YOK)
- [ ] `cd-staging.yml`: main'e merge → image GHCR'a push → EC2'de zero-downtime deploy
- [ ] GitHub Environments: `production` için manuel approval
- [ ] DB migration stratejisi: deploy'da otomatik migration + geri alma planı
- [ ] Yedekleme: RDS snapshot / S3'e dump + cron

**DoD:** `https://mediflow.senindomainin.com` üzerinden internet üzerinden
çalışıyor. `git push` → 5 dakikada canlıda.

> **Maliyet kontrolü (önemli):** t3.medium ~$30/ay. Öğrenci için çok. Çözüm:
> (1) Spot instance (~%70 indirim), (2) EventBridge + Lambda ile gece 00:00'da
> `stop`, sabah 09:00'da `start` → maliyet yarıya iner, (3) demo yapmadığın
> dönemlerde `terraform destroy` — altyapı kodda olduğu için 10 dk'da geri gelir.
> Free tier'da RDS SQL Server Express (db.t3.micro, 12 ay) ve Amazon MQ
> (mq.t3.micro, 12 ay) bedava — kullan.

---

### Faz 8 — Orkestrasyon & Ölçekleme (2 hafta)

**Öğrenilen:** Container orkestrasyonu, autoscaling, blue/green deploy.

İki yol var, **ikisini de yapmanı öneririm** (sırayla):

**8a. ECS Fargate (1 hafta)** — AWS'in yönettiği, sunucusuz container
- [ ] Terraform: ECR, ECS cluster, task definition, service, ALB + target group
- [ ] Service discovery (Cloud Map), autoscaling (CPU + kuyruk derinliğine göre)
- [ ] Blue/green deploy (CodeDeploy), rolling update, health check ile otomatik rollback
- [ ] CloudWatch Logs + Container Insights

**8b. Kubernetes (1 hafta)** — mülakatlarda en çok sorulan
- [ ] Önce **local**: Docker Desktop K8s veya kind
- [ ] Deployment, Service, Ingress, ConfigMap, Secret, HPA, liveness/readiness probe
- [ ] **Helm chart** yaz (9 servis için tek chart, values ile ortam ayrımı)
- [ ] EC2 üzerinde **k3s** kur → gerçek K8s deneyimi, EKS'in $73/ay control plane
      ücreti olmadan. (EKS'i sadece 2-3 gün açıp kapat, görmüş ol.)
- [ ] ArgoCD ile GitOps (opsiyonel ama çok etkileyici)

**DoD:** Trafik artınca pod/task sayısı otomatik artıyor. Bozuk bir image deploy
et → otomatik rollback olduğunu göster.

---

### Faz 9 — İleri Seviye + Frontend (1.5 hafta)

**Öğrenilen:** Read model, arama, gRPC, background job, gerçek zamanlı UI.

- [ ] **Elasticsearch read model**: event'lerden beslenen projeksiyon, fuzzy arama, Türkçe analyzer
- [ ] `Reporting` servisi: istatistik API + PDF rapor (QuestPDF)
- [ ] **gRPC**: iki servis arası yüksek performanslı senkron çağrı (HTTP ile benchmark karşılaştır)
- [ ] **Hangfire/Quartz**: randevu hatırlatma job'ı, gece raporu, temizlik job'ı
- [ ] `VitalsMonitoring`: cihaz simülatörü (`tools/device-simulator`) → Redis Stream
      → SignalR → **canlı nöbetçi ekranı**
- [ ] React 19 + TS + Vite dashboard: login, randevu takvimi, canlı vital grafiği, bildirim zili
- [ ] Playwright E2E test + CI'da headless çalıştır
- [ ] Opsiyonel: .NET Aspire ile local orkestrasyonu modernize et

**DoD:** Tarayıcıda canlı vital grafiği akıyor, eşik aşılınca kırmızı alarm
düşüyor. E2E testler CI'da yeşil.

---

### Faz 10 — Portfolyo & Cilalama (1 hafta)

Bu fazı **atlama** — projeyi iş bulmaya çeviren kısım burası.

- [ ] README: mimari diyagram, GIF demo, tech stack, "2 komutla çalıştır", canlı link
- [ ] C4 diyagramları (Mermaid) — context, container, component
- [ ] Tüm ADR'ler derli toplu (en az 10 tane olmalı)
- [ ] `docs/lessons-learned.md`: "neyi yanlış yaptım, nasıl düzelttim" — **en değerli dosya**
- [ ] 5 dakikalık demo videosu (YouTube unlisted) → README'ye göm
- [ ] Performans raporu: k6 sonuçları, p95 latency, optimizasyon öncesi/sonrası
- [ ] Security review: OWASP API Top 10 checklist, `dotnet list package --vulnerable`
- [ ] CV + LinkedIn'e ekle; 3-4 teknik blog yazısı (Medium/dev.to):
      "Outbox Pattern'i .NET'te Neden ve Nasıl Uyguladım" gibi
- [ ] GitHub repo: topics, description, pinned, güzel bir social preview image

---

## 6. Test Stratejisi (piramit)

```
        /\       E2E (Playwright)         ~10 test   — kritik kullanıcı akışları
       /  \      Integration (Testcontainers) ~60    — API + gerçek DB/broker
      /    \     Contract                  ~15       — event şeması uyumu
     /______\    Unit                     ~250       — domain kuralları, handler'lar
```
+ Load (k6), Chaos (servis öldürme), Mutation (Stryker), Architecture (NetArchTest).

**Kural:** Her bug fix PR'ında önce bug'ı yakalayan test → sonra fix.

---

## 7. Güvenlik Checklist

- JWT: kısa ömür, refresh rotation + reuse detection, `kid` ile key rotation
- Permission-based authz (rol yeterli değil), gateway'de + servis içinde çift kontrol
- Rate limiting (gateway), IP bazlı + kullanıcı bazlı
- KVKK: kişisel sağlık verisi şifreleme (EF Core value converter / Always Encrypted),
  audit log (kim hangi hastanın kaydını ne zaman açtı), veri saklama süresi
- Secrets: hiçbir sır repo'da yok → `git-secrets` / Gitleaks pre-commit hook
- Container: non-root user, distroless/chiseled base image, read-only filesystem
- Trivy + CodeQL + Dependabot CI'da zorunlu
- SQL injection (parametreli sorgu), mass assignment, IDOR testi (başka hastanın ID'si)
- CORS, security headers, HTTPS zorunlu, HSTS

---

## 8. Öğrenme Yöntemi (kritik)

1. **Önce problemi yaşa, sonra teknolojiyi öğren.** "Redis lock öğreneyim" değil,
   "çifte rezervasyon bug'ım var, nasıl çözülür?" Bu sırayla öğrendiğin bilgi kalıcı olur.
2. **Her teknoloji için 3 soruyu cevapla** ve ADR'ye yaz: Hangi problemi çözüyor?
   Alternatifleri neydi, neden bunu seçtim? Maliyeti/dezavantajı ne?
3. **Docs > kurs.** Microsoft Learn, MassTransit docs, Terraform registry. Kurs
   sadece ilk temas için.
4. **Haftalık retro:** `docs/journal/2026-W40.md` — ne yaptım, nerede tıkandım,
   ne öğrendim. Faz 10'da bu dosyalar blog yazılarına dönüşecek.
5. **Kopyalama yasağı:** Her satırın ne yaptığını açıklayamıyorsan sil, yeniden yaz.

### Kaynaklar
- **Kod referansı:** `dotnet/eShop` (resmi mikroservis örneği), `ardalis/CleanArchitecture`,
  `jasontaylordev/CleanArchitecture`, `MassTransit` sample'ları
- **Kitap:** *Microservices Patterns* (Chris Richardson), *Designing Data-Intensive
  Applications* (Kleppmann), *Implementing DDD* (Vernon)
- **Docs:** Microsoft Learn (.NET Microservices e-book — ücretsiz PDF), MassTransit,
  Terraform AWS Provider, OpenTelemetry .NET

---

## 9. Sık Yapılan Hatalar (bunlardan kaçın)

| Hata | Doğrusu |
|---|---|
| Gün 1'de 10 mikroservis | Modüler monolitle başla, ihtiyaç doğunca ayır |
| Shared database | Her servisin kendi DB'si, paylaşım event ile |
| Shared "Common" DLL'de domain tipleri | BuildingBlocks sadece teknik altyapı içerir |
| Her yerde senkron HTTP zinciri | Async event tercih et; senkron zincir = dağıtık monolit |
| Test yazmayı sona bırakmak | Faz 1'den itibaren her PR'da test |
| CI'ı en sona bırakmak | Faz 0'da kur, boş da olsa çalışsın |
| AWS'de unutulan kaynaklar | Budgets alarm + `terraform destroy` disiplini |
| CQRS'i "her şeye" uygulamak | Sadece read/write asimetrisi olan yerde (Scheduling) |
| Repository pattern'i EF üstüne gereksiz sarmak | EF zaten UoW; sadece gerçekten gerekliyse |
| README'siz repo | README projenin yüzü, 1 saat harca |

---

## 10. Hemen Şimdi: İlk 3 Gün

**Gün 1**
1. GitHub'da `mediflow` public repo + local `git init` + ilk commit
2. GitHub Student Developer Pack başvurusu
3. Solution + 4 katmanlı `Identity` servisi iskeleti, .NET 10
4. `docker compose up` → MSSQL + Redis + RabbitMQ + Seq ayağa kalksın

**Gün 2**
5. `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, analyzer'lar
6. `ci.yml`: restore → build → test → format check; branch protection aç
7. PR template, CODEOWNERS, Dependabot, ADR-0001

**Gün 3**
8. `Patient` aggregate'i TDD ile yaz (önce test)
9. İlk CQRS akışı: `CreatePatientCommand` + validator + handler + endpoint
10. İlk PR'ı aç, kendi kodunu incele, squash merge et 🎉

---

## 11. Zaman/Kazanım Özeti

| Faz | Süre | Ana kazanım |
|---|---|---|
| 0 | 4 gün | Proje hijyeni, Docker, CI |
| 1 | 2 hafta | Clean Arch, CQRS, EF Core, JWT |
| 2 | 2 hafta | Servis ayırma, Gateway, Redis, concurrency |
| 3 | 2 hafta | RabbitMQ, Outbox, SignalR |
| 4 | 1 hafta | Saga, resilience, DLQ |
| 5 | 2 hafta | Observability (OTel, Grafana, ELK) |
| 6 | 1.5 hafta | Testcontainers, kalite kapıları |
| 7 | 2 hafta | **AWS'de canlı**, Terraform, CD |
| 8 | 2 hafta | ECS Fargate + Kubernetes |
| 9 | 1.5 hafta | Elasticsearch, gRPC, React realtime |
| 10 | 1 hafta | Portfolyo, blog, demo |
| | **~16 hafta** | |

Yetişmezse **kısaltma sırası:** 8b (K8s) → 9 (frontend) → 8a. Faz 0-7 pazarlıksız.

---

*Son güncelleme: 2026-09-28*
