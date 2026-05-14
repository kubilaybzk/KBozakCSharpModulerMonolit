**UDEMY COURSE WITH DISCOUNTED - Step by Step Development of this Repository -> https://www.udemy.com/course/net-backend-bootcamp-modulith-vsa-ddd-cqrs-and-outbox/?couponCode=MARC26**

See the overall picture of **Modular Monoliths (Modulith) architecture on .NET** in real-world **KozakCSharpModularMonolith** project;

![0modulith](https://github.com/user-attachments/assets/0f1f340e-6cb1-4bfd-aa05-f54109e5b865)

There is a couple of modules implemented **KozakCSharpModularMonolith** domain over **Catalog, Basket, Identity** and **Ordering** modules with **Cloud-native Backing services (Redis, RabbitMQ, Keycloak)** and **Relational PostgreSQL databases isolated db schemas**, communicating over **RabbitMQ Event Driven Communication** and following **VSA, DDD, CQRS and Outbox Patterns**.

### Check Explanation of this Repository on Medium
* [.NET Backend Bootcamp: Modular Monoliths, VSA, DDD, CQRS and Outbox](https://mehmetozkaya.medium.com/net-backend-bootcamp-modular-monoliths-vsa-ddd-cqrs-and-outbox-b6332b272209)


## Whats Including In This Repository
We have implemented below **architectural patterns in this repository**.
* Modular Monoliths (Modulith) Architecture
* Vertical Slice Architecture (VSA)
* Domain-Driven Design (DDD)
* Command Query Responsibility Segregation (CQRS)
* Outbox Pattern for Reliable Messaging

#### Catalog module which includes; 
* ASP.NET Core Minimal APIs and latest features of .NET8 and C# 12
* **Vertical Slice Architecture** implementation with Feature folders and single .cs file includes different classes in one file
* CQRS implementation using MediatR library
* CQRS Validation Pipeline Behaviors with MediatR and FluentValidation
* Use Entity Framework Core Code-First Approach and Migrations on PostgreSQL Database
* Use Carter for Minimal API endpoint definition
* Cross-cutting concerns Logging, Global Exception Handling and Health Checks

#### Basket module which includes; 
* Using Redis as a Distributed Cache over PostgreSQL database
* Implements Proxy, Decorator and Cache-aside patterns
* Publish BasketCheckoutEvent to RabbitMQ via MassTransit library
* Implement Outbox Pattern For Reliable Messaging w/ BasketCheckout Use Case

#### Module Communications; 
* Sync Communications between Catalog and Basket Modules with In-process Method Calls (Public APIs)
* Async Communications between Modules w/ RabbitMQ & MassTransit for UpdatePrice Between Catalog-Basket Modules

#### Identity module which includes; 
* OAuth2 + OpenID Connect Flows with Keycloak
* Setup Keycloak into Docker-compose file for Identity Provider as a Backing Service
* JwtBearer token for OpenID Connect with Keycloak Identity

#### Ordering module which includes; 
* Implementing DDD, CQRS, and Clean Architecture with using Best Practices
* Implement Outbox Pattern For Reliable Messaging w/ BasketCheckout Use Case

#### Migrate to Microservices; 
* KozakCSharpModularMonolith Modules to Microservices w/ Stranger Fig Pattern


## Run The Project
You will need the following tools:

* [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/)
* [.Net Core 8 or later](https://dotnet.microsoft.com/download/dotnet-core/8)
* [Docker Desktop](https://www.docker.com/products/docker-desktop)

### Installing
Follow these steps to get your development environment set up: (Before Run Start the Docker Desktop)
1. Clone the repository
2. Once Docker for Windows is installed, go to the **Settings > Advanced option**, from the Docker icon in the system tray, to configure the minimum amount of memory and CPU like so:
* **Memory: 4 GB**
* CPU: 2
3. At the root directory of solution, select **docker-compose** and **Set a startup project**. **Run docker-compose without debugging on visual studio**.
  Or you can go to root directory which include **docker-compose.yml** files, run below command:
```csharp
docker-compose -f docker-compose.yml -f docker-compose.override.yml up -d
```

4. Wait for docker compose all services. That’s it! (some microservices need extra time to work so please wait if not worked in first shut)

5. Launch **Shopping Web Api -> https://localhost:6060** in postman and send api request to internal modules. You can import postman collection in your local environment.

## Technology Stack & Libraries

| Katman | Teknoloji | Versiyon |
|--------|-----------|----------|
| Framework | ASP.NET Core | 8.0 |
| Language | C# | 12 |
| Endpoint definition | [Carter](https://github.com/CarterCommunity/Carter) | 8.1 |
| Mediator / CQRS | [MediatR](https://github.com/jbogard/MediatR) | 12.4 |
| Validation | [FluentValidation](https://docs.fluentvalidation.net) | 11.9 |
| Object mapping | [Mapster](https://github.com/MapsterMapper/Mapster) | 7.4 |
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | — |
| Message broker | RabbitMQ | — |
| Messaging library | [MassTransit](https://masstransit.io) | 8.2 |
| Distributed cache | Redis | — |
| Identity provider | Keycloak | — |
| DI decoration | [Scrutor](https://github.com/khellang/Scrutor) | 4.2 |
| Logging | Serilog + Seq | — |

### Architecture Patterns

| Pattern | Ne İşe Yarar |
|---------|--------------|
| **Modular Monolith** | Her business domain kendi modülü — tek deployable, izole geliştirme |
| **Vertical Slice Architecture** | Her use-case kendi klasöründe, Handler + Endpoint çifti |
| **CQRS** | `ICommand` / `IQuery` ile okuma ve yazma yolları ayrılır |
| **DDD** | Aggregate, Value Object, Domain Event ile iş mantığı domainler |
| **Outbox Pattern** | DB transaction ve message publish atomik — güvenilir mesajlaşma |
| **Decorator / Cache-aside** | Repository decorator ile Redis cache katmanı |

---

## Claude Code Skills

Bu repo Claude Code ile aktif olarak çalışmak üzere yapılandırılmıştır. `.claude/skills/` altındaki skill'ler Claude'u projenin mimari kurallarına göre yönlendirir.

### backend-feature

**Ne zaman:** Bir modüle yeni endpoint / feature eklerken veya mevcut bir feature'ı güncellerken.

Vertical Slice pattern'e uygun `[Feature]Handler.cs` (command/query + validator + handler) ve `[Feature]Endpoint.cs` (Carter routing) çiftini doğru yapıyla oluşturur. Handler'ı `internal` yapar, `AsNoTracking()` hatırlatır, `ICommand` vs `IQuery` ayrımını uygular.

```
"Notification modülüne GetNotificationById endpoint'i ekle"
→ Handler.cs + Endpoint.cs şablonunu uygular
```

---

### new-module

**Ne zaman:** Yeni bir business domain (modül) eklenirken.

Implementation projesi + Contracts projesi klasör yapısını, DbContext'i (izole schema), DI registration'ı (`Add[Module]Module` / `Use[Module]Module`) ve Program.cs entegrasyonunu adım adım üretir.

```
"Payment adında yeni bir modül ekleyeyim"
→ Tam proje scaffold + ilk migration komutu
```

---

### ef-migration

**Ne zaman:** Entity veya property değişikliği olduğunda, seed data eklenirken.

Modül bazında migration oluşturma komutlarını, schema sahipliği kurallarını, iyi migration isimlendirmesini ve idempotent seed pattern'ini gösterir. Her modülün migration'ının kendi projesinde yaşadığını uygular.

```
"Product'a Category kolonu ekledim, migration lazım"
→ Doğru --project ve --context argümanlarıyla dotnet ef komutu
```

---

### integration-design

**Ne zaman:** Bir modülün başka bir modülle konuşması gerektiğinde.

Sync (Contract + IQuery) vs async (Integration Event + Consumer + Command) kararını verir. Event payload completeness kontrolü, outbox gerekliliği testi ve consumer delegation pattern'ini uygular.

```
"Basket checkout olunca Ordering yeni sipariş oluştursun"
→ Async: BasketCheckoutEvent + Consumer + CreateOrderCommand şablonu
```

---

### code-review

**Ne zaman:** Kod incelemesi, PR review veya mimari denetim yapılırken.

P0 (runtime) → P1 (mimari) → P2 (convention) öncelik sırasıyla bulguları listeler. Status code tutarsızlığı, blanket catch, cross-module referans, DbContext endpoint'te gibi hotspot'ları kontrol eder.

```
"Bu PR'ı review et, sorun var mı?"
→ Seviyeli bulgular: [P0-RUNTIME], [P1-ARCH], [P2-CONV]
```

---

### refactor

**Ne zaman:** Davranış değiştirmeden kod temizlenirken, isimlendirme düzeltilirken.

Başlamadan önce 4 güvenlik sorusu sorar. Güvenli alanları (naming, metadata) ve riskli alanları (EF mapping, auth, event payload) ayırt eder. Naming quick reference tablosu içerir.

```
"CheckoutBasketEndpoints class ismini düzelt"
→ Güvenli: CheckoutBasketEndpoints → CheckoutBasketEndpoint
```

---

### drift-fix

**Ne zaman:** Projede biriken mimari tutarsızlıklar düzeltilirken.

5 drift tipi için tam fix kodu içerir: status code tutarsızlığı, blanket catch, hardcoded consumer data, route büyük harf, naming typo. Her fix için risk seviyesi ve davranış değişikliği uyarısı verir.

```
"CheckoutBasket handler'daki catch bloğunu düzelt"
→ Risk analizi + davranış değişikliği uyarısı + fix kodu
```

---

## Authors
* **Mehmet Ozkaya** - *Initial work* - [mehmetozkaya](https://github.com/mehmetozkaya)
# KBozakCSharpModulerMonolit
