---
name: efcore-patterns
description: Entity Framework Core best practices including NoTracking by default, query splitting for navigation collections, migration management, dedicated migration services, and common pitfalls to avoid.
invocable: false
---

# Entity Framework Core Patterns

## When to Use This Skill

Use this skill when:
- Setting up EF Core in a new project
- Optimizing query performance
- Managing database migrations
- Debugging change tracking issues
- Loading multiple navigation collections efficiently (query splitting)

## Core Principles

1. **NoTracking by Default** - Most queries are read-only; opt-in to tracking
2. **Never Edit Migrations Manually** - Always use CLI commands
3. **Dedicated Migration Service** - Separate migration execution from application startup
4. **ExecutionStrategy for Retries** - Handle transient database failures
5. **Explicit Updates** - When NoTracking, explicitly mark entities for update

---

## Pattern 1: NoTracking by Default

Configure your DbContext to disable change tracking by default. This improves performance for read-heavy workloads.

```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
        // Disable change tracking by default for better performance on read-only queries
        // Use .AsTracking() explicitly for queries that need to track changes
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }
}
```

### When NoTracking is Active

**Read-only queries work normally:**
```csharp
// ✅ Fast read - no tracking overhead
var orders = await dbContext.Orders
    .Where(o => o.Status == OrderStatus.Pending)
    .ToListAsync();
```

**Writes require explicit handling:**
```csharp
// ❌ WRONG - Entity not tracked, SaveChanges does nothing
var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
order.Status = OrderStatus.Shipped;
await dbContext.SaveChangesAsync(); // Nothing happens!

// ✅ CORRECT - Explicitly mark entity for update
var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
order.Status = OrderStatus.Shipped;
dbContext.Orders.Update(order);
await dbContext.SaveChangesAsync();

// ✅ ALSO CORRECT - Use AsTracking() for the query
var order = await dbContext.Orders
    .AsTracking()
    .FirstOrDefaultAsync(o => o.Id == orderId);
order.Status = OrderStatus.Shipped;
await dbContext.SaveChangesAsync();
```

### When to Use Tracking

| Scenario | Use Tracking? | Why |
|----------|---------------|-----|
| Display data in UI | No | Read-only, no updates |
| API GET endpoints | No | Returning data, no mutations |
| Update single entity | Yes or explicit Update() | Need to save changes |
| Complex update with navigation | Yes | Tracking handles relationships |
| Batch operations | No + ExecuteUpdate | More efficient |

---

## Pattern 2: Never Edit Migrations Manually

**CRITICAL:** Always use EF Core CLI commands to manage migrations. Never manually edit migration files (except for custom SQL in `Up()`/`Down()`), delete, rename, or copy migration files.

```bash
# Create a new migration
dotnet ef migrations add AddCustomerTable \
    --project src/MyApp.Infrastructure \
    --startup-project src/MyApp.Api

# Remove the last migration (if not yet applied)
dotnet ef migrations remove \
    --project src/MyApp.Infrastructure \
    --startup-project src/MyApp.Api

# Generate idempotent SQL script
dotnet ef migrations script \
    --idempotent \
    --project src/MyApp.Infrastructure \
    --startup-project src/MyApp.Api
```

---

## Pattern 3: ExecutionStrategy for Transient Failures

Always use `CreateExecutionStrategy()` for operations that might fail transiently:

```csharp
public async Task UpdateWithRetryAsync(Guid id, Action<Order> update)
{
    var strategy = _dbContext.Database.CreateExecutionStrategy();

    await strategy.ExecuteAsync(async () =>
    {
        var order = await _dbContext.Orders
            .AsTracking()
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return;

        update(order);
        await _dbContext.SaveChangesAsync();
    });
}
```

**Important:** Transaction must be INSIDE the strategy callback:

```csharp
var strategy = _dbContext.Database.CreateExecutionStrategy();

await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await _dbContext.Database.BeginTransactionAsync();
    try
    {
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
});
```

---

## Pattern 4: Bulk Operations with ExecuteUpdate/ExecuteDelete

For bulk operations, use EF Core 7+ `ExecuteUpdateAsync` and `ExecuteDeleteAsync`:

```csharp
// ❌ SLOW - Loads all entities into memory
var expiredOrders = await _db.Orders
    .Where(o => o.ExpiresAt < DateTimeOffset.UtcNow)
    .ToListAsync();
foreach (var order in expiredOrders) order.Status = OrderStatus.Expired;
await _db.SaveChangesAsync();

// ✅ FAST - Single SQL UPDATE statement
await _db.Orders
    .Where(o => o.ExpiresAt < DateTimeOffset.UtcNow)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(o => o.Status, OrderStatus.Expired)
        .SetProperty(o => o.UpdatedAt, DateTimeOffset.UtcNow));
```

---

## Pattern 5: Query Splitting to Prevent Cartesian Explosion

When you load multiple navigation collections via `Include()`, EF Core generates a single query that can cause cartesian explosion. If you have 10 orders with 10 items each, you get 100 rows instead of 10 + 10.

### Global Configuration (Recommended)

```csharp
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
```

### Per-Query Override

```csharp
var orders = await dbContext.Orders
    .Include(o => o.Items)
    .Include(o => o.Payments)
    .AsSingleQuery()  // Override global split behavior when you know it's safe
    .ToListAsync();
```

### Trade-offs

| Behavior | Pros | Cons |
|-----------|-------|-------|
| SplitQuery | No cartesian explosion, better for large collections | Multiple round-trips |
| SingleQuery | Single round-trip, transactional consistency | Cartesian explosion risk |

**Recommendation**: Default to `SplitQuery` globally, override with `AsSingleQuery()` for specific queries.

---

## Common Pitfalls

### 1. Forgetting to Update When NoTracking

```csharp
// ❌ Silent failure - entity not tracked
var customer = await _db.Customers.FindAsync(id);
customer.Name = "New Name";
await _db.SaveChangesAsync(); // Does nothing!

// ✅ Explicit update
_db.Customers.Update(customer);
await _db.SaveChangesAsync();
```

### 2. N+1 Query Problem

```csharp
// ❌ N+1 queries - one query per customer
var customers = await _db.Customers.ToListAsync();
foreach (var customer in customers)
{
    var orders = customer.Orders; // Lazy load triggers query
}

// ✅ Eager loading - single query
var customers = await _db.Customers
    .Include(c => c.Orders)
    .ToListAsync();
```

### 3. Querying Inside Loops

```csharp
// ❌ Query per iteration
foreach (var orderId in orderIds)
{
    var order = await _db.Orders.FindAsync(orderId);
}

// ✅ Single query
var orders = await _db.Orders
    .Where(o => orderIds.Contains(o.Id))
    .ToListAsync();
```

---

## DbContext Lifetime in DI

```csharp
// ASP.NET Core: Scoped = one instance per HTTP request
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Background services: Create scope per unit of work
public class MyBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // ... use dbContext ...
    }
}
```

---

## Testing with EF Core

For integration tests against real PostgreSQL, use the `testcontainers-integration-tests` skill.

```csharp
var container = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .Build();

await container.StartAsync();

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(container.GetConnectionString())
    .Options;
```
