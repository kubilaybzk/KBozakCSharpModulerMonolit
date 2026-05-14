---
name: redis-patterns
description: Redis caching patterns for ASP.NET Core modular monolith. Covers Cache-aside via Scrutor repository decorator, IConnectionMultiplexer configuration, key naming conventions, TTL strategy, distributed lock, and cache invalidation. Use when adding Redis caching to a module, implementing cache decorator, or debugging Redis connection/data issues.
invocable: false
---

# Redis Caching Patterns

## When to Use This Skill

Use this skill when:
- Adding Redis cache to a module's repository
- Implementing Scrutor decorator pattern for caching
- Configuring IConnectionMultiplexer
- Designing key naming and TTL strategy
- Implementing distributed lock
- Debugging cache staleness or connection issues

## Core Principles

1. **Cache via decorator** — handler doesn't know about cache; repository decorator handles it
2. **Cache-aside** — read from cache → miss → read from DB → write to cache
3. **Explicit TTL** — every key must have an expiry
4. **Key namespacing** — `[module]:[entity]:[id]` format prevents collisions
5. **Fail-open** — cache errors should not break the request

---

## Pattern 1: Scrutor Repository Decorator

The project uses Scrutor to decorate repositories with caching — handler stays clean.

```csharp
// 1. Interface
public interface I[Entity]Repository
{
    Task<[Entity]?> Get[Entity]Async(string key, CancellationToken ct = default);
    Task Store[Entity]Async([Entity] entity, CancellationToken ct = default);
}

// 2. Real repository (DB)
public class [Entity]Repository : I[Entity]Repository
{
    private readonly [Module]DbContext _db;

    public async Task<[Entity]?> Get[Entity]Async(string key, CancellationToken ct = default)
    {
        return await _db.[Entities]
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, ct);
    }

    public async Task Store[Entity]Async([Entity] entity, CancellationToken ct = default)
    {
        _db.[Entities].Add(entity);
        await _db.SaveChangesAsync(ct);
    }
}

// 3. Cache decorator
public class Cached[Entity]Repository : I[Entity]Repository
{
    private readonly I[Entity]Repository _inner;
    private readonly IDatabase _cache;

    public Cached[Entity]Repository(I[Entity]Repository inner, IConnectionMultiplexer redis)
    {
        _inner = inner;
        _cache = redis.GetDatabase();
    }

    public async Task<[Entity]?> Get[Entity]Async(string key, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(key);

        var cached = await _cache.StringGetAsync(cacheKey);
        if (cached.HasValue)
            return JsonSerializer.Deserialize<[Entity]>(cached!);

        var entity = await _inner.Get[Entity]Async(key, ct);

        if (entity is not null)
            await _cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(entity),
                expiry: TimeSpan.FromMinutes(10));

        return entity;
    }

    public async Task Store[Entity]Async([Entity] entity, CancellationToken ct = default)
    {
        await _inner.Store[Entity]Async(entity, ct);
        // Invalidate cache on write
        await _cache.KeyDeleteAsync(CacheKey(entity.Key));
    }

    private static string CacheKey(string key) => $"[module]:[entity]:{key}";
}

// 4. DI registration with Scrutor
services.AddScoped<I[Entity]Repository, [Entity]Repository>();
services.Decorate<I[Entity]Repository, Cached[Entity]Repository>();
```

---

## Pattern 2: IConnectionMultiplexer Configuration

```csharp
// In Add[Module]Module or shared registration
var connectionString = configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Redis connection string missing");

services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(connectionString));
```

```json
// appsettings.json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

**Lifetime:** `IConnectionMultiplexer` must be **Singleton** — it manages the connection pool internally. `IDatabase` is cheap and obtained per-operation via `.GetDatabase()`.

---

## Pattern 3: Key Naming Convention

```
[module]:[entity]:[identifier]

Examples:
  basket:cart:user@example.com
  catalog:product:abc123
  ordering:order:550e8400-e29b-41d4-a716-446655440000
```

Rules:
- All lowercase
- Colon-separated segments
- Module prefix prevents cross-module collisions
- Use the natural business key (userName, productId), not DB surrogate key

---

## Pattern 4: TTL Strategy

| Data Type | TTL | Reason |
|-----------|-----|--------|
| User session/cart | 15–30 min | Matches session timeout |
| Product catalog | 5–10 min | Changes infrequently |
| Order status | 1–2 min | Changes frequently |
| Static lookup data | 1 hour | Rarely changes |

```csharp
// Explicit TTL — never cache without expiry
await _cache.StringSetAsync(key, value, expiry: TimeSpan.FromMinutes(10));

// ❌ Never do this — key persists forever
await _cache.StringSetAsync(key, value);
```

---

## Pattern 5: Distributed Lock (SemaphoreSlim for single-instance)

For single-instance modular monolith, in-process semaphore is sufficient:

```csharp
private static readonly SemaphoreSlim _lock = new(1, 1);

public async Task<[Entity]> GetOrCreateAsync(string key, CancellationToken ct)
{
    var cached = await _cache.StringGetAsync(key);
    if (cached.HasValue)
        return Deserialize(cached!);

    await _lock.WaitAsync(ct);
    try
    {
        // Double-check after acquiring lock
        cached = await _cache.StringGetAsync(key);
        if (cached.HasValue)
            return Deserialize(cached!);

        var entity = await _inner.GetAsync(key, ct);
        await _cache.StringSetAsync(key, Serialize(entity),
            expiry: TimeSpan.FromMinutes(10));
        return entity;
    }
    finally
    {
        _lock.Release();
    }
}
```

---

## Pattern 6: Fail-Open Cache

Cache failures should not break the application:

```csharp
public async Task<[Entity]?> Get[Entity]Async(string key, CancellationToken ct)
{
    try
    {
        var cached = await _cache.StringGetAsync(CacheKey(key));
        if (cached.HasValue)
            return JsonSerializer.Deserialize<[Entity]>(cached!);
    }
    catch (RedisException ex)
    {
        // Log and fall through to DB — don't rethrow
        _logger.LogWarning(ex, "Redis unavailable, falling back to database for key {Key}", key);
    }

    return await _inner.Get[Entity]Async(key, ct);
}
```

---

## Common Pitfalls

| Pitfall | Fix |
|---------|-----|
| Singleton IDatabase injected as Scoped | Register IConnectionMultiplexer as Singleton, call `.GetDatabase()` per operation |
| No TTL on cached key | Always pass `expiry` parameter |
| Cache not invalidated on write | Delete key in Store/Update/Delete methods |
| Serializing EF tracked entity | Detach or use a dedicated DTO for serialization |
| Cache key collision | Use `[module]:[entity]:[id]` prefix |
