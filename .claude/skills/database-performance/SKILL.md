---
name: database-performance
description: Database access patterns for performance. Separate read/write models, avoid N+1 queries, use AsNoTracking, apply row limits, and never do application-side joins. Works with EF Core and Dapper.
invocable: false
tags: [cqrs, performance, patterns]
---

# Database Performance Patterns

## When to Use This Skill

Use this skill when:
- Designing data access layers
- Optimizing slow database queries
- Choosing between EF Core and Dapper
- Avoiding common performance pitfalls

---

## Core Principles

1. **Separate read and write models** - Don't use the same types for both
2. **Think in batches** - Avoid N+1 queries
3. **Only retrieve what you need** - No SELECT *
4. **Apply row limits** - Always have a configurable Take/Limit
5. **Do joins in SQL** - Never in application code
6. **AsNoTracking for reads** - EF Core change tracking is expensive

---

## Read/Write Model Separation (CQRS Pattern)

Read and write models are fundamentally different — they have different shapes, columns, and purposes. Don't create a single entity and reuse it everywhere.

- **Read models** are denormalized, optimized for query efficiency, return multiple projection types
- **Write models** are normalized, validation-focused, accept strongly-typed commands

### Read Store Interface

```csharp
public interface IOrderReadStore
{
    Task<OrderDetail?> GetByIdAsync(OrderId id, CancellationToken ct = default);
    Task<IReadOnlyList<OrderSummary>> GetByCustomerAsync(CustomerId id, int limit, OrderId? cursor = null, CancellationToken ct = default);
    Task<bool> ExistsAsync(OrderId id, CancellationToken ct = default);
}
```

### Write Store Interface

```csharp
public interface IOrderWriteStore
{
    // Returns only the created ID - caller doesn't need the full entity
    Task<OrderId> CreateAsync(CreateOrderCommand command, CancellationToken ct = default);
    Task UpdateAsync(OrderId id, UpdateOrderCommand command, CancellationToken ct = default);
    Task DeleteAsync(OrderId id, CancellationToken ct = default);
}
```

---

## Always Apply Row Limits

Never return unbounded result sets. Every read method should have a configurable limit.

```csharp
// EF Core with pagination
public async Task<IReadOnlyList<OrderSummary>> GetOrdersAsync(
    CustomerId customerId,
    int limit,
    CancellationToken ct = default)
{
    return await _context.Orders
        .AsNoTracking()
        .Where(o => o.CustomerId == customerId.Value)
        .OrderByDescending(o => o.CreatedAt)
        .Take(limit)  // Always limit!
        .Select(o => new OrderSummary(o.Id, o.Total, o.Status, o.CreatedAt))
        .ToListAsync(ct);
}
```

---

## AsNoTracking for Read Queries

EF Core's change tracking is expensive. Disable it for read-only queries.

```csharp
// ✅ DO: Disable tracking for reads
var orders = await _context.Orders
    .AsNoTracking()
    .Where(o => o.CustomerId == customerId)
    .ToListAsync();

// ❌ DON'T: Track entities you won't modify
var orders = await _context.Orders
    .Where(o => o.CustomerId == customerId)
    .ToListAsync(); // Change tracking enabled - wasteful
```

For read-heavy workloads, configure NoTracking at DbContext level (see `efcore-patterns` skill).

---

## Avoid N+1 Queries

The N+1 problem: fetching a list, then querying for each item's related data.

```csharp
// ❌ BAD: N+1 queries
var orders = await _context.Orders.ToListAsync();
foreach (var order in orders)
{
    var items = await _context.OrderItems
        .Where(i => i.OrderId == order.Id)
        .ToListAsync(); // Each iteration hits the database!
}

// ✅ GOOD: Single query with Include
var orders = await _context.Orders
    .AsNoTracking()
    .Include(o => o.Items)
    .ToListAsync();

// ✅ GOOD: Explicit projection - only fetch needed columns
var orders = await _context.Orders
    .AsNoTracking()
    .Select(o => new OrderSummary(
        o.Id,
        o.Total,
        o.Items.Count))
    .ToListAsync();
```

---

## Avoid Cartesian Explosions with Multiple Includes

```csharp
// ❌ DANGEROUS: Can explode into thousands of rows
var product = await _context.Products
    .Include(p => p.Reviews)      // 100 reviews
    .Include(p => p.Images)       // 20 images
    .Include(p => p.Categories)   // 5 categories
    .FirstOrDefaultAsync(p => p.Id == id);
// Result: 100 * 20 * 5 = 10,000 rows transferred!

// ✅ GOOD: Split queries
var product = await _context.Products
    .AsSplitQuery()
    .Include(p => p.Reviews)
    .Include(p => p.Images)
    .Include(p => p.Categories)
    .FirstOrDefaultAsync(p => p.Id == id);

// ✅ BEST: Explicit projection
var product = await _context.Products
    .AsNoTracking()
    .Where(p => p.Id == id)
    .Select(p => new ProductDetail(
        p.Id,
        p.Name,
        p.Reviews.OrderByDescending(r => r.CreatedAt).Take(10).ToList(),
        p.Images.Take(5).ToList()))
    .FirstOrDefaultAsync();
```

---

## Never Do Application-Side Joins

Joins must happen in SQL, not in C#.

```csharp
// ❌ BAD: Application join - O(n*m) in memory
var customers = await _context.Customers.ToListAsync();
var orders = await _context.Orders.ToListAsync();
var result = customers.Select(c => new {
    Customer = c,
    Orders = orders.Where(o => o.CustomerId == c.Id).ToList()
});

// ✅ GOOD: SQL join
var result = await _context.Customers
    .AsNoTracking()
    .Include(c => c.Orders)
    .ToListAsync();
```

---

## Don't Build Generic Repositories

Generic repositories hide query complexity and make optimization difficult.

```csharp
// ❌ BAD: Generic repository
public interface IRepository<T>
{
    Task<IEnumerable<T>> GetAllAsync();  // No limit!
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate); // Can't optimize
}

// ✅ GOOD: Purpose-built read stores
public interface IOrderReadStore
{
    Task<OrderDetail?> GetByIdAsync(OrderId id, CancellationToken ct = default);
    Task<IReadOnlyList<OrderSummary>> GetByCustomerAsync(CustomerId id, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<OrderSummary>> GetPendingAsync(int limit, CancellationToken ct = default);
}
```

---

## When to Use EF Core vs Dapper

| Scenario | Recommendation |
|----------|---------------|
| Simple CRUD | EF Core |
| Complex read queries | Dapper |
| Writes with validation | EF Core |
| Bulk operations | Dapper or raw SQL |
| Reporting/analytics | Dapper |

You can use both in the same project — EF Core for writes, Dapper for reads.

---

## Quick Reference

| Anti-Pattern | Solution |
|--------------|----------|
| No row limit | Add `limit` parameter to every read method |
| SELECT * | Project only needed columns |
| N+1 queries | Use Include or projection |
| Application joins | Do joins in SQL |
| Cartesian explosion | Use AsSplitQuery or projection |
| Tracking read-only data | Use AsNoTracking |
| Generic repository | Purpose-built read/write stores |
