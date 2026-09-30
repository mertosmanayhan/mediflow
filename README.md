# MediFlow

> Sağlık sektörü odaklı, .NET 10 tabanlı mikroservis platformu.
> Modüler monolitten mikroservise evrilen, AWS üzerinde canlı çalışan
> uçtan uca bir sistem.

[![CI](https://github.com/mertosmanayhan/mediflow/actions/workflows/ci.yml/badge.svg)](https://github.com/mertosmanayhan/mediflow/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**Durum:** 🚧 Geliştiriliyor — Faz 0 (temel kurulum)

---

## Ne yapar?

Hasta randevu alır → doktor muayene eder → tahlil ister → sonuç yüklenir →
kritik değer varsa doktora **anlık** alarm gider → yatan hastaların vital
verileri canlı nöbetçi ekranında akar.

## Neden bu proje?

Bu, gerçek bir ürün değil; **modern backend ekosistemini uçtan uca öğrenmek**
için tasarlanmış bir laboratuvar. Sağlık domaini bilinçli seçildi: aşağıdaki
teknolojileri *yapay olarak değil, zorunlu olarak* gerektiriyor.

| Gerçek problem | Zorunlu kıldığı çözüm |
|---|---|
| Aynı randevu slotuna iki kişi aynı anda talep atıyor | Redis distributed lock + optimistic concurrency |
| Randevu alındı → SMS + e-posta + takvim güncelle | RabbitMQ ile asenkron mesajlaşma |
| Tahlilde kritik değer → doktorun ekranında anında uyarı | SignalR |
| Randevu iptali → slot boşalt + ödeme iade + bildirim | Saga / compensation |
| Veritabanına yazdım ama event gitmedi | Outbox pattern |
| Doktor takvimi saniyede onlarca kez sorgulanıyor | CQRS + Redis cache |

## Teknoloji yığını

Aşağıdaki liste hedeftir; her biri ilgili fazda eklenir
(bkz. [ROADMAP.md](ROADMAP.md)).

- **Runtime:** .NET 10 (LTS), C# 14
- **API:** ASP.NET Core Minimal API, OpenAPI
- **Mimari:** Clean Architecture, CQRS + MediatR, DDD (seçili context'lerde)
- **Veri:** MSSQL + EF Core 10, MongoDB, Redis, Elasticsearch
- **Mesajlaşma:** RabbitMQ + MassTransit, Outbox, Saga
- **Gerçek zamanlı:** SignalR (+ Redis backplane)
- **Gateway:** YARP
- **Observability:** Serilog, OpenTelemetry, Prometheus, Grafana, Jaeger
- **Test:** xUnit, Testcontainers, k6, Stryker.NET
- **DevOps:** Docker, GitHub Actions, Terraform, AWS (EC2 → ECS Fargate)

## Nasıl çalıştırılır?

### Gereksinimler

| Araç | Sürüm | Not |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0.200+ | Sürüm `global.json` ile sabitlenmiştir |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | 24+ | Altyapı servisleri için; en az 4 GB RAM ayrılmış olmalı |

### Kurulum

```bash
# 1) Ortam değişkenlerini hazırla (gerçek .env repoda değildir)
cp .env.example .env        # Windows: Copy-Item .env.example .env

# 2) Altyapıyı başlat (MSSQL, Redis, RabbitMQ, Seq)
docker compose up -d

# 3) Veritabanı bağlantı dizesini kullanıcı sırlarına yaz.
#    Bu değer repoya GİRMEZ; %APPDATA%\Microsoft\UserSecrets altında durur.
#    Şifre .env dosyasındaki MSSQL_SA_PASSWORD ile aynı olmalı.
dotnet user-secrets set "ConnectionStrings:PatientProfile" \
  "Server=localhost,1433;Database=MediFlow.PatientProfile;User Id=sa;Password=<.env'deki şifre>;TrustServerCertificate=True;Encrypt=True" \
  --project src/Host/MediFlow.Api

# 4) Veritabanı şemasını oluştur
dotnet tool restore
dotnet dotnet-ef database update \
  --project src/Modules/PatientProfile/MediFlow.PatientProfile.Infrastructure \
  --startup-project src/Host/MediFlow.Api

# 5) Testleri çalıştır
dotnet test MediFlow.slnx

# 6) API'yi başlat
dotnet run --project src/Host/MediFlow.Api
```

Sonra tarayıcıda **http://localhost:5xxx/scalar** (port konsolda yazar).

### API

| Uç nokta | Ne yapar |
|---|---|
| `POST /patients` | Yeni hasta oluşturur → `201` + kimlik |
| `GET /patients/{id}` | Hastayı getirir → `200`, yoksa `404` |
| `GET /scalar` | Gezilebilir API arayüzü |
| `GET /openapi/v1.json` | Makine okunabilir API tanımı |

Hatalar `ProblemDetails` (RFC 9457) biçiminde döner ve makine tarafının
dallanacağı sabit bir `code` alanı taşır:

```json
{ "title": "Validation failed", "status": 400,
  "detail": "First name is required.", "code": "Patient.FirstNameEmpty" }
```

> ℹ️ Kullanıcı sırları **yalnızca `Development` ortamında** yüklenir. API'yi
> derlenmiş çıktıdan doğrudan çalıştırıyorsan `ASPNETCORE_ENVIRONMENT=Development`
> vermen gerekir; `dotnet run` bunu kendisi yapar.

### Altyapı servisleri

| Servis | Adres | Giriş | Ne için |
|---|---|---|---|
| MSSQL | `localhost:1433` | `sa` / `.env` içindeki şifre | Ana veritabanı |
| Redis | `localhost:6379` | — | Cache + distributed lock |
| RabbitMQ (AMQP) | `localhost:5672` | `.env` içindeki bilgiler | Mesajlaşma |
| RabbitMQ (arayüz) | http://localhost:15672 | `.env` içindeki bilgiler | Kuyruk yönetimi |
| Seq | http://localhost:5341 | — | Log arama |

### Faydalı komutlar

```bash
docker compose ps                 # durum ve healthcheck
docker compose logs -f mssql      # bir servisin loglarını izle
docker compose down               # durdur (veri korunur)
docker compose down -v            # durdur ve TÜM VERİYİ SİL
```

> ℹ️ Uygulama container içinden veritabanına bağlanırken `localhost` değil
> **servis adı** kullanılır (`Server=mssql,1433`). Compose'un özel ağında
> `localhost`, container'ın kendisi anlamına gelir.

## Dokümantasyon

| Dosya | İçerik |
|---|---|
| [ROADMAP.md](ROADMAP.md) | 16 haftalık faz planı ve öğrenme hedefleri |
| `docs/adr/` | Architecture Decision Records — alınan her mimari kararın gerekçesi |
| `docs/journal/` | Haftalık geliştirme günlüğü: ne yaptım, nerede tıkandım, ne öğrendim |

## Lisans

MIT
