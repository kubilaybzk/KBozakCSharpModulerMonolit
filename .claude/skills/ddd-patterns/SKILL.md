---
name: ddd-patterns
description: DDD implementation patterns for this ASP.NET Core modular monolith. Covers Aggregate<TId>, Entity<TId>, IDomainEvent, value objects (record + Of() factory), factory methods, domain event raising via AddDomainEvent(), and DispatchDomainEventsInterceptor. Use when creating aggregates, domain events, value objects, or event handlers in any module.
invocable: false
---

# DDD Patterns

## Project Base Types

These types live in `Shared/Shared/DDD/` and are available to all modules.

```csharp
// IDomainEvent — extends INotification so MediatR can dispatch it
public interface IDomainEvent : INotification
{
    Guid EventId => Guid.NewGuid();
    DateTime OccurredOn => DateTime.Now;
    string EventType => GetType().AssemblyQualifiedName!;
}

// Entity<T> — auditable base with Id
public abstract class Entity<T> : IEntity<T>
{
    public T Id { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? LastModified { get; set; }
    public string? LastModifiedBy { get; set; }
}

// Aggregate<TId> — adds domain event collection on top of Entity<T>
public abstract class Aggregate<TId> : Entity<TId>, IAggregate<TId>
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    public IDomainEvent[] ClearDomainEvents()
    {
        IDomainEvent[] events = _domainEvents.ToArray();
        _domainEvents.Clear();
        return events;
    }
}
```

---

## Pattern 1: Aggregate with Factory Method

The `Create(...)` static factory is the only public entry point for creating an aggregate. It enforces invariants, sets state, and raises the domain event.

```csharp
// [Module]/[Feature]/Models/[Entity].cs
public class [Entity] : Aggregate<Guid>
{
    private readonly List<[ChildEntity]> _items = new();
    public IReadOnlyList<[ChildEntity]> Items => _items.AsReadOnly();

    public string [Property] { get; private set; } = default!;

    // Private constructor — callers must use Create()
    private [Entity]() { }

    public static [Entity] Create(Guid id, string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);

        var entity = new [Entity]
        {
            Id = id,
            [Property] = property
        };

        entity.AddDomainEvent(new [Entity]CreatedEvent(entity));

        return entity;
    }

    // Domain behavior — not just setters
    public void Update[Property](string newValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newValue);
        [Property] = newValue;
        AddDomainEvent(new [Entity]UpdatedEvent(this));
    }
}
```

**Real example — Order aggregate:**
```csharp
public class Order : Aggregate<Guid>
{
    public static Order Create(Guid id, Guid customerId, string orderName,
        Address shippingAddress, Address billingAddress, Payment payment)
    {
        var order = new Order
        {
            Id = id, CustomerId = customerId, OrderName = orderName,
            ShippingAddress = shippingAddress, BillingAddress = billingAddress,
            Payment = payment
        };
        order.AddDomainEvent(new OrderCreatedEvent(order));
        return order;
    }
}
```

---

## Pattern 2: Domain Event

Domain events are `record` types (immutable by default) implementing `IDomainEvent`.

```csharp
// [Module]/[Feature]/Events/[Entity]CreatedEvent.cs
public record [Entity]CreatedEvent([Entity] [Entity]) : IDomainEvent;
```

**Real examples:**
```csharp
// Ordering module
public record OrderCreatedEvent(Order Order) : IDomainEvent;

// Catalog module
public record ProductCreatedEvent(Product Product) : IDomainEvent;
public record ProductPriceChangedEvent(Product Product) : IDomainEvent;
```

---

## Pattern 3: Domain Event Handler

Event handlers live in `EventHandlers/` next to `Events/`. They implement `INotificationHandler<TEvent>` — MediatR dispatches them automatically via `DispatchDomainEventsInterceptor`.

```csharp
// [Module]/[Feature]/EventHandlers/[Entity]CreatedEventHandler.cs
public class [Entity]CreatedEventHandler(ILogger<[Entity]CreatedEventHandler> logger)
    : INotificationHandler<[Entity]CreatedEvent>
{
    public Task Handle([Entity]CreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event handled: {DomainEvent}", notification.GetType().Name);
        // business reaction to the event
        return Task.CompletedTask;
    }
}
```

**How dispatch works:** `DispatchDomainEventsInterceptor` (EF Core `SaveChangesInterceptor`) fires before `SaveChanges`. It collects events from all tracked `IAggregate` instances, clears them, and publishes each via `IMediator.Publish()`. No manual dispatch needed.

---

## Pattern 4: Value Object

Value objects are `record` types with a private constructor and a static `Of(...)` factory that validates inputs.

```csharp
// [Module]/[Feature]/ValueObjects/[ValueObject].cs
public record [ValueObject]
{
    public string [Property] { get; } = default!;

    protected [ValueObject]() { }  // required by EF for owned entity mapping

    private [ValueObject](string property)
    {
        [Property] = property;
    }

    public static [ValueObject] Of(string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        return new [ValueObject](property);
    }
}
```

**Real examples:**
```csharp
// Address value object — validates emailAddress and addressLine
public static Address Of(string firstName, string lastName, string emailAddress,
    string addressLine, string country, string state, string zipCode)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);
    ArgumentException.ThrowIfNullOrWhiteSpace(addressLine);
    return new Address(firstName, lastName, emailAddress, addressLine, country, state, zipCode);
}

// Payment value object — validates card fields and CVV length
public static Payment Of(string cardName, string cardNumber, string expiration,
    string cvv, int paymentMethod)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(cvv.Length, 3);
    return new Payment(cardName, cardNumber, expiration, cvv, paymentMethod);
}
```

**EF mapping** — value objects are owned entities:
```csharp
// In DbContext OnModelCreating
modelBuilder.Entity<Order>(builder =>
{
    builder.OwnsOne(o => o.ShippingAddress);
    builder.OwnsOne(o => o.BillingAddress);
    builder.OwnsOne(o => o.Payment);
});
```

---

## Pattern 5: Child Entity (non-aggregate)

Child entities belong to an aggregate and are only reachable through it. They extend `Entity<T>` directly, not `Aggregate<T>`.

```csharp
public class [ChildEntity] : Entity<Guid>
{
    public Guid ParentId { get; private set; }
    public Guid [RelatedId] { get; private set; }
    public int Quantity { get; internal set; }

    internal [ChildEntity](Guid parentId, Guid relatedId, int quantity, decimal price)
    {
        Id = Guid.NewGuid();
        ParentId = parentId;
        [RelatedId] = relatedId;
        Quantity = quantity;
        Price = price;
    }
}
```

Child entities are added through aggregate methods — never instantiated by handlers directly.

---

## Folder Structure

```
[Module]/
└── [Feature]/
    ├── Models/
    │   └── [Entity].cs          ← Aggregate<TId>
    ├── Events/
    │   └── [Entity]CreatedEvent.cs   ← record : IDomainEvent
    ├── EventHandlers/
    │   └── [Entity]CreatedEventHandler.cs  ← INotificationHandler<T>
    └── ValueObjects/
        └── [ValueObject].cs     ← record with Of() factory
```

---

## Common Pitfalls

| Pitfall | Fix |
|---------|-----|
| Raising event in handler, not in aggregate | Move `AddDomainEvent()` inside the aggregate factory/method |
| Missing `protected` constructor on value object | EF owned entity mapping requires it |
| Returning `IList` instead of `IReadOnlyList` for children | Expose `IReadOnlyList` — mutate only via aggregate methods |
| Using `record` for aggregate root | Use `class` — records with inheritance create EF tracking issues |
| Calling `AddDomainEvent()` after `SaveChanges` | Interceptor fires on `SaveChanges` — events must be queued before that |
| Directly mutating child via public setter | All child mutation goes through aggregate methods |
