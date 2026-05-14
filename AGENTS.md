# EShop Modular Monolith — AGENTS.md

## Project Overview

ASP.NET Core 8 Modular Monolith. Her modül bağımsız bir domain'dir; DDD + CQRS + VSA kombinasyonu kullanır.

**Solution:** `src/eshop-modular-monoliths.sln`

```
src/
├── Bootstrapper/Api/          # Entry point — Program.cs
├── Modules/
│   ├── Catalog/
│   │   ├── Catalog/           # Implementation
│   │   └── Catalog.Contracts/ # Public DTOs & events (cross-module)
│   ├── Basket/Basket/
│   └── Ordering/Ordering/
└── Shared/
    ├── Shared/                # DDD base, behaviors, interceptors, exceptions
    ├── Shared.Contracts/      # CQRS interfaces (ICommand, IQuery, ICommandHandler, IQueryHandler)
    └── Shared.Messaging/      # MassTransit setup, IntegrationEvent base
```

---

## Architecture Rules

### 1. Module Boundary — Katı İzolasyon

- Modüller **birbirini doğrudan referans alamaz**. Cross-module iletişim sadece:
  - `*.Contracts` projesi üzerinden (DTOs, query interfaces)
  - Integration events via MassTransit (RabbitMQ)
- Her modülün kendi `DbContext`'i ve kendi DB schema'sı vardır (`catalog`, `basket`, `ordering`)

### 2. Vertical Slice Architecture (VSA)

Her feature kendi slice'ında yaşar:

```
Module/[Domain]/Features/[FeatureName]/
├── [Feature]Endpoint.cs    # Carter ICarterModule — HTTP layer
└── [Feature]Handler.cs     # Command/Query + Validator + Handler — tek dosya
```

**Örnek:**
```
Catalog/Products/Features/CreateProduct/
├── CreateProductEndpoint.cs
└── CreateProductHandler.cs   ← Command + Validator + Handler burada
```

### 3. CQRS Pattern

`Shared.Contracts/CQRS/` içindeki interface'leri kullan:

```csharp
// Write: ICommand<TResult>
public record CreateProductCommand(ProductDto Product) : ICommand<CreateProductResult>;
public record CreateProductResult(Guid Id);

// Read: IQuery<TResult>
public record GetProductByIdQuery(Guid Id) : IQuery<GetProductByIdResult>;

// Handlers
internal class CreateProductHandler(CatalogDbContext dbContext)
    : ICommandHandler<CreateProductCommand, CreateProductResult> { ... }
    
internal class GetProductByIdHandler(CatalogDbContext dbContext)
    : IQueryHandler<GetProductByIdQuery, GetProductByIdResult> { ... }
```

- **Handler sınıfları `internal`** olur, Endpoint `public`
- Command/Query/Result `record` tipinde tanımlanır
- Validator ile Handler **aynı dosyada** yer alır

### 4. Carter Endpoint Pattern

```csharp
public record CreateProductRequest(ProductDto Product);
public record CreateProductResponse(Guid Id);

public class CreateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/products", async (CreateProductRequest request, ISender sender) =>
        {
            var command = request.Adapt<CreateProductCommand>();
            var result = await sender.Send(command);
            var response = result.Adapt<CreateProductResponse>();
            return Results.Created($"/products/{response.Id}", response);
        })
        .WithName("CreateProduct")
        .Produces<CreateProductResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create Product")
        .WithDescription("Create Product");
    }
}
```

- Mapping için **Mapster** (`.Adapt<T>()`) kullan, AutoMapper değil
- HTTP → Command mapping endpoint'te yapılır
- Endpoint hiçbir zaman doğrudan DbContext'e dokunmaz

### 5. Domain-Driven Design (DDD)

Aggregates:
```csharp
public class Product : Aggregate<Guid>
{
    public static Product Create(Guid id, string name, ...)
    {
        var product = new Product { Id = id, Name = name, ... };
        product.AddDomainEvent(new ProductCreatedEvent(product));
        return product;
    }
}
```

- Aggregate'ler **static factory method** ile oluşturulur (`Product.Create(...)`)
- Domain event `AddDomainEvent()` ile aggregate içinde raise edilir
- Value Object'ler immutable `record` veya `class` ile tanımlanır

### 6. FluentValidation

Command/Query ile **aynı dosyada**, aynı namespace:
```csharp
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Product.Name).NotEmpty().WithMessage("Name is required");
        RuleFor(x => x.Product.Price).GreaterThan(0).WithMessage("Price must be greater than 0");
    }
}
```

- Validation otomatik olarak `ValidationBehavior` MediatR pipeline'ı üzerinden çalışır
- `BadRequestException` veya `ValidationException` at — elle handle etme

### 7. EF Core — DbContext & Configuration

```csharp
// DbContext: kendi schema'sını kullanır
public class CatalogDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("catalog");
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

// Entity config: ayrı dosyada, Fluent API
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(50).IsRequired();
    }
}
```

- Her entity için `Data/Configurations/[Entity]Configuration.cs`
- Migration'lar `Data/Migrations/` altında

### 8. Module Registration — DI Extension Methods

Her modül iki extension method sağlar:

```csharp
public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        // DbContext, interceptors, seeders, module-specific services
        return services;
    }

    public static IApplicationBuilder UseCatalogModule(this IApplicationBuilder app)
    {
        // Migration, seeding
        app.UseMigration<CatalogDbContext>();
        return app;
    }
}
```

- `Program.cs` tüm modülleri birleştirir — modüller birbirinden habersizdir
- `Add*Module` → DI registrations
- `Use*Module` → middleware / startup actions

### 9. Exception Handling

Domain'e özel exception'lar at:
```csharp
throw new ProductNotFoundException(id);        // → HTTP 404
throw new BadRequestException("Invalid data"); // → HTTP 400
```

- `CustomExceptionHandler` (`Shared/Exceptions/Handler/`) tüm mapping'i yapar
- Endpoint'te try/catch kullanma
- Her modülün kendi `Exceptions/` klasörü vardır

### 10. Domain & Integration Events

**Domain Events** (modül içi, MediatR):
```csharp
// Aggregate'de raise et
product.AddDomainEvent(new ProductCreatedEvent(product));

// Handler — aynı modülde
public class ProductCreatedEventHandler : INotificationHandler<ProductCreatedEvent> { ... }
```

**Integration Events** (cross-module, MassTransit):
```csharp
// Publisher (Basket modülü — CheckoutBasket handler)
// Outbox pattern ile: OutboxMessage tablosuna yaz, OutboxProcessor publish eder

// Consumer (Ordering modülü)
public class BasketCheckoutIntegrationEventHandler : IConsumer<BasketCheckoutIntegrationEvent> { ... }
```

- Modüller arası iletişim için **her zaman integration event** kullan, doğrudan servis çağrısı değil
- Güvenilir delivery gereken durumlarda **Outbox Pattern** kullan

### 11. Caching (Repository Decorator)

```csharp
// Scrutor decorator — DI'da:
services.Decorate<IBasketRepository, CachedBasketRepository>();

// CachedBasketRepository Redis'i wrap eder, interface aynı kalır
```

### 12. Cross-Cutting Concerns (MediatR Pipeline Behaviors)

`Shared/Behaviors/` altında:
- `LoggingBehavior<TRequest, TResponse>` — tüm request'leri loglar, >3s için warning
- `ValidationBehavior<TRequest, TResponse>` — FluentValidation entegrasyonu

Yeni bir behavior eklemek istersen `Shared` projesine ekle, `AddMediatRWithAssemblies` ile otomatik register edilir.

---

## Naming Conventions

| Artefact | Pattern | Örnek |
|---|---|---|
| Feature folder | `[FeatureName]` | `CreateProduct` |
| Endpoint | `[Feature]Endpoint` | `CreateProductEndpoint` |
| Command | `[Feature]Command` | `CreateProductCommand` |
| Query | `[Feature]Query` | `GetProductByIdQuery` |
| Result | `[Feature]Result` | `CreateProductResult` |
| HTTP Request | `[Feature]Request` | `CreateProductRequest` |
| HTTP Response | `[Feature]Response` | `CreateProductResponse` |
| Handler | `[Feature]Handler` | `CreateProductHandler` |
| Validator | `[Feature]CommandValidator` | `CreateProductCommandValidator` |
| DTO | `[Entity]Dto` | `ProductDto` |
| Exception | `[Entity]NotFoundException` | `ProductNotFoundException` |
| Domain Event | `[Event]Event` | `ProductCreatedEvent` |
| Integration Event | `[Event]IntegrationEvent` | `BasketCheckoutIntegrationEvent` |
| DbContext | `[Module]DbContext` | `CatalogDbContext` |
| Module class | `[Module]Module` | `CatalogModule` |

---

## Key Libraries

| Library | Versiyon | Kullanım |
|---|---|---|
| MediatR | v12.4 | CQRS dispatcher |
| Carter | v8.1 | Minimal API routing |
| FluentValidation | v11.9 | Command/Query validation |
| Mapster | v7.4 | Object mapping (`.Adapt<T>()`) |
| EF Core + Npgsql | v8.0 | ORM + PostgreSQL |
| MassTransit + RabbitMQ | v8.2 | Integration events |
| Scrutor | v4.2 | DI decorator pattern |
| Serilog + Seq | v8.0 | Structured logging |
| Keycloak.AuthServices | v2.5 | JWT authentication |
| StackExchange.Redis | v8.0 | Distributed cache |

---

## Yeni Feature Ekleme Checklist

```
[ ] Feature klasörü oluştur: Module/Domain/Features/[FeatureName]/
[ ] Handler dosyası: Command/Query record, Validator class, Handler class (internal)
[ ] Endpoint dosyası: Request/Response record, ICarterModule implementation
[ ] Yeni entity gerekiyorsa: Model + IEntityTypeConfiguration + migration
[ ] Domain event gerekiyorsa: Event record (IDomainEvent), EventHandler
[ ] Cross-module ise: IntegrationEvent (Contracts'ta), Consumer handler
[ ] Exception gerekiyorsa: Module'ün Exceptions/ klasörüne ekle
```
