---
name: dependency-injection-patterns
description: Organize DI registrations using IServiceCollection extension methods. Group related services into composable Add* methods for clean Program.cs and reusable configuration in tests.
invocable: false
---

# Dependency Injection Patterns

## When to Use This Skill

Use this skill when:
- Organizing service registrations in ASP.NET Core applications
- Avoiding massive Program.cs files with hundreds of registrations
- Making service configuration reusable between production and tests
- Debugging captive dependency issues (scoped in singleton)

---

## The Problem

Without organization, Program.cs becomes unmanageable:

```csharp
// BAD: 200+ lines of unorganized registrations
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
// ... 150 more lines — hard to find, no clear boundaries, merge conflicts
```

---

## The Solution: Extension Method Composition

```csharp
// GOOD: Clean, composable Program.cs
builder.Services
    .AddCatalogModule(builder.Configuration)
    .AddBasketModule(builder.Configuration)
    .AddOrderingModule(builder.Configuration);
```

---

## Extension Method Pattern

```csharp
namespace MyApp.Catalog;

public static class CatalogServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CatalogSettings>()
            .BindConfiguration(CatalogSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("catalog")));

        services.AddScoped<ICatalogRepository, CatalogRepository>();

        return services;
    }
}
```

**Convention:** `Add{Module}Module` for modules, `Add{Feature}Services` for feature groups.

---

## File Organization

Place extension methods near the services they register:

```
src/
  Bootstrapper/Api/
    Program.cs                    # Composes all Add* methods
  Modules/Catalog/
    CatalogModule.cs              # AddCatalogModule() extension
  Modules/Basket/
    BasketModule.cs               # AddBasketModule() extension
```

---

## Testing Benefits

The `Add*` pattern lets you reuse production configuration in tests and only override what's different:

```csharp
// Integration test: reuse production registration, swap DB
public class CatalogModuleTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CatalogModuleTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove production DbContext, add test one
                var descriptor = services.Single(
                    d => d.ServiceType == typeof(DbContextOptions<CatalogDbContext>));
                services.Remove(descriptor);

                services.AddDbContext<CatalogDbContext>(options =>
                    options.UseNpgsql(testConnectionString));
            });
        });
    }
}
```

---

## Lifetime Management

| Lifetime | Use When | Examples |
|----------|----------|----------|
| **Singleton** | Stateless, thread-safe, expensive to create | Config, HttpClient factories, caches |
| **Scoped** | Stateful per-request | DbContext, repositories |
| **Transient** | Lightweight, stateful, cheap | Validators, short-lived helpers |

```csharp
services.AddSingleton<ICacheService, RedisCacheService>();  // Stateless
services.AddScoped<IBasketRepository, BasketRepository>();  // Per-request
services.AddTransient<CreateOrderValidator>();              // Cheap, short-lived
```

---

## Captive Dependency — Most Common Bug

**Scoped service injected into singleton** = stale state, connection leaks, data corruption.

```csharp
// BAD: Singleton captures scoped DbContext — stale context used forever!
public class CacheService  // Registered as Singleton
{
    private readonly IBasketRepository _repo;  // Scoped - captured at startup!
}

// GOOD: Inject IServiceProvider, create scope per operation
public class CacheService
{
    private readonly IServiceProvider _serviceProvider;

    public async Task<Basket?> GetBasketAsync(string userName)
    {
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBasketRepository>();
        return await repo.GetBasketAsync(userName);
    }
}
```

**This exact risk exists in this project:** `BasketRepository` is scoped; if any singleton wraps it (Redis decorator, background outbox worker), it becomes a captive dependency.

---

## Background Service Scope Pattern

```csharp
// BAD: No scope for scoped services
public class OutboxWorker : BackgroundService
{
    private readonly IOrderRepository _repo;  // Scoped — will throw or use stale context!
}

// GOOD: Create scope for each unit of work
public class OutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            await ProcessMessagesAsync(repo, ct);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
```

---

## Anti-Patterns

```csharp
// DON'T: Overly generic — communicates nothing
public static IServiceCollection AddServices(this IServiceCollection services) { ... }

// DON'T: Hide connection string inside extension
public static IServiceCollection AddDatabase(this IServiceCollection services)
{
    services.AddDbContext<AppDbContext>(opts =>
        opts.UseNpgsql("hardcoded-connection-string"));  // Hidden!
}

// DO: Accept configuration explicitly
public static IServiceCollection AddDatabase(
    this IServiceCollection services,
    string connectionString)
{
    services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(connectionString));
    return services;
}
```

---

## Best Practices Summary

| Practice | Benefit |
|----------|---------|
| Group related services into `Add*` methods | Clean Program.cs, clear boundaries |
| Return `IServiceCollection` for chaining | Fluent API |
| Accept configuration parameters | Flexibility and testability |
| Never inject scoped into singleton | Prevents captive dependency |
| Use `IServiceScopeFactory` in background services | Correct scoped service lifetime |
