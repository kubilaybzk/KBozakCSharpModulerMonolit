---
name: new-module
description: >
  Modüler monolith projesine yeni bir business modülü (domain) eklerken kullan.
  "Yeni modül ekle", "yeni servis oluştur", "[X] modülü yaz", "yeni domain ekleyelim",
  "modül scaffold", "yeni bir bounded context" gibi ifadeler bu skill'i tetiklemeli.
  Tam klasör yapısı, DbContext, DI registration, Program.cs entegrasyonu dahil step-by-step rehber.
---

# New Module Skill

## Amaç

Modüler monolith'te her business domain izole bir modüldür. Bu skill sıfırdan tam çalışan, mimariyle uyumlu bir modül oluşturur. Başka modüllerin implementation koduna dokunmaz, onlarla sadece contracts veya integration event'ler üzerinden konuşur.

## Proje Yapısı

```
src/Modules/[Module]/
├── [Module]/                          ← Implementation (private)
│   ├── Data/
│   │   ├── Configurations/            ← EF Fluent API konfigürasyonları
│   │   ├── Migrations/                ← Bu modüle ait migration'lar
│   │   └── [Module]DbContext.cs
│   ├── [Domain]/                      ← Entity + feature'lar
│   │   ├── [Entity].cs
│   │   └── Features/
│   │       └── [FeatureName]/
│   │           ├── [Feature]Handler.cs
│   │           └── [Feature]Endpoint.cs
│   ├── Extensions/
│   │   └── ServiceCollectionExtensions.cs
│   └── GlobalUsings.cs
└── [Module].Contracts/                ← Sync cross-module interface (public)
    └── [Domain]/
        └── Features/
            └── [FeatureName]/
                └── [Feature]Query.cs
```

**Neden iki proje?** Implementation gizli kalır — başka modüller sadece Contracts projesine referans verir, implementation'a değil. Bu mimari sınırı projenin kendisi uygular.

## Adım 1: Implementation Projesi

```xml
<!-- [Module]/[Module].csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\Shared\Shared\Shared.csproj" />
    <ProjectReference Include="..\..\..\Shared\Shared.Messaging\Shared.Messaging.csproj" />
    <ProjectReference Include="..\[Module].Contracts\[Module].Contracts.csproj" />
  </ItemGroup>
</Project>
```

## Adım 2: Contracts Projesi

```xml
<!-- [Module].Contracts/[Module].Contracts.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <!-- Sadece Shared — başka modüle bağımlılık yok -->
    <ProjectReference Include="..\..\..\Shared\Shared\Shared.csproj" />
  </ItemGroup>
</Project>
```

## Adım 3: DbContext

```csharp
// Data/[Module]DbContext.cs
namespace [Module].Data;

public class [Module]DbContext(DbContextOptions<[Module]DbContext> options)
    : DbContext(options)
{
    public DbSet<[Entity]> [Entities] => Set<[Entity]>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Her modülün kendi schema'sı — tablo çakışmalarını önler
        modelBuilder.HasDefaultSchema("[module_lowercase]");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
```

## Adım 4: DI Registration

```csharp
// Extensions/ServiceCollectionExtensions.cs
namespace [Module].Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection Add[Module]Module(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<[Module]DbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Database")));

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        services.AddCarter();

        return services;
    }

    public static IApplicationBuilder Use[Module]Module(
        this IApplicationBuilder app)
    {
        // Migration modül başlatılırken otomatik çalışır
        app.UseMigration<[Module]DbContext>();
        return app;
    }
}
```

## Adım 5: GlobalUsings.cs

```csharp
global using Carter;
global using FluentValidation;
global using Mapster;
global using MediatR;
global using Microsoft.EntityFrameworkCore;
global using [Module].Data;
global using Shared.CQRS;
global using Shared.DDD;
global using Shared.Exceptions;
```

## Adım 6: Program.cs Entegrasyonu

```csharp
// src/Bootstrapper/Api/Program.cs
// Services bölümüne ekle:
builder.Services.Add[Module]Module(builder.Configuration);

// Middleware bölümüne ekle:
app.Use[Module]Module();
```

## Adım 7: Solution'a Ekle

```bash
dotnet sln add src/Modules/[Module]/[Module]/[Module].csproj
dotnet sln add src/Modules/[Module]/[Module].Contracts/[Module].Contracts.csproj

# Bootstrapper'ın implementation'a referansı olmalı
# (Bootstrapper diğer modüllere referans vermez — sadece o modülün registration'ını çağırır)
```

## İlk Migration

```bash
dotnet ef migrations add InitialCreate \
  --project src/Modules/[Module]/[Module] \
  --startup-project src/Bootstrapper/Api \
  --context [Module]DbContext \
  --output-dir Data/Migrations
```

## Checklist

- [ ] İki proje var: Implementation + Contracts
- [ ] `HasDefaultSchema("[module]")` ayarlandı
- [ ] `Add[Module]Module()` ve `Use[Module]Module()` Program.cs'e eklendi
- [ ] Başka modüllerin implementation'ına proje referansı verilmedi
- [ ] İlk migration oluşturuldu
- [ ] GlobalUsings.cs oluşturuldu
- [ ] Solution dosyasına eklendi
