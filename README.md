See the overall picture of **Modular Monoliths (Modulith) architecture on .NET** in real-world **KozakCSharpModularMonolith** project;

![0modulith](https://github.com/user-attachments/assets/0f1f340e-6cb1-4bfd-aa05-f54109e5b865)

There is a couple of modules implemented **KozakCSharpModularMonolith** domain over **Catalog, Basket, Identity** and **Ordering** modules with **Cloud-native Backing services (Redis, RabbitMQ, Keycloak)** and **Relational PostgreSQL databases isolated db schemas**, communicating over **RabbitMQ Event Driven Communication** and following **VSA, DDD, CQRS and Outbox Patterns**.

### Check Explanation of this Repository on Medium
* [.NET Backend Bootcamp: Modular Monoliths, VSA, DDD, CQRS and Outbox](https://mehmetozkaya.medium.com/net-backend-bootcamp-modular-monoliths-vsa-ddd-cqrs-and-outbox-b6332b272209)

## Technology Stack & Libraries

| Layer | Technology | Version |
|-------|-----------|---------|
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

| Pattern | Purpose |
|---------|---------|
| **Modular Monolith** | Each business domain in its own module — single deployable, isolated development |
| **Vertical Slice Architecture** | Each use-case in its own folder, Handler + Endpoint pair |
| **CQRS** | `ICommand` / `IQuery` separates read and write paths |
| **DDD** | Business logic modeled via Aggregate, Value Object, Domain Event |
| **Outbox Pattern** | DB transaction and message publish are atomic — reliable messaging |
| **Decorator / Cache-aside** | Redis cache layer via repository decorator |

---

## Claude Code Skills

This repo is configured to work actively with Claude Code. The skills under `.claude/skills/` guide Claude according to the project's architectural rules.

### backend-feature

**When:** Adding a new endpoint / feature to a module or updating an existing feature.

Generates a `[Feature]Handler.cs` (command/query + validator + handler) and `[Feature]Endpoint.cs` (Carter routing) pair conforming to the Vertical Slice pattern. Makes the handler `internal`, reminds `AsNoTracking()`, applies `ICommand` vs `IQuery` distinction.

```
"Add GetNotificationById endpoint to Notification module"
→ Applies Handler.cs + Endpoint.cs template
```

---

### new-module

**When:** Adding a new business domain (module).

Generates step-by-step: Implementation project + Contracts project folder structure, DbContext (isolated schema), DI registration (`Add[Module]Module` / `Use[Module]Module`) and Program.cs integration.

```
"I want to add a new module called Payment"
→ Full project scaffold + first migration command
```

---

### ef-migration

**When:** When there's an entity or property change, or when adding seed data.

Shows per-module migration generation commands, schema ownership rules, good migration naming, and idempotent seed pattern. Enforces that each module's migrations live in their own project.

```
"I added a Category column to Product, need migration"
→ dotnet ef command with correct --project and --context arguments
```

---

### integration-design

**When:** When a module needs to communicate with another module.

Decides between Sync (Contract + IQuery) vs Async (Integration Event + Consumer + Command). Applies event payload completeness check, outbox necessity test, and consumer delegation pattern.

```
"When basket checkout happens, Ordering should create a new order"
→ Async: BasketCheckoutEvent + Consumer + CreateOrderCommand template
```

---

### code-review

**When:** Code review, PR review, or architectural audit.

Lists findings in P0 (runtime) → P1 (architectural) → P2 (convention) priority order. Checks hotspots like status code inconsistency, blanket catch, cross-module reference, DbContext in endpoint.

```
"Review this PR, are there any issues?"
→ Leveled findings: [P0-RUNTIME], [P1-ARCH], [P2-CONV]
```

---

### refactor

**When:** Cleaning up code without changing behavior, fixing naming.

Asks 4 safety questions before starting. Distinguishes safe areas (naming, metadata) from risky areas (EF mapping, auth, event payload). Includes naming quick reference table.

```
"Fix CheckoutBasketEndpoints class name"
→ Safe: CheckoutBasketEndpoints → CheckoutBasketEndpoint
```

---

### drift-fix

**When:** Fixing accumulated architectural inconsistencies in the project.

Contains full fix code for 5 drift types: status code inconsistency, blanket catch, hardcoded consumer data, route uppercase, naming typo. Gives risk level and behavior change warning for each fix.

```
"Fix the catch block in CheckoutBasket handler"
→ Risk analysis + behavior change warning + fix code
```

---

### efcore-patterns

**When:** Setting up EF Core, optimizing query performance, debugging change tracking issues.

Covers NoTracking by default, query splitting (`AsSplitQuery`) to prevent cartesian explosion, ExecutionStrategy for transient failures, and bulk `ExecuteUpdate`/`ExecuteDelete` patterns.

```
"Order.Items Include causes too many rows"
→ AsSplitQuery configuration + per-query override
```

---

### database-performance

**When:** Optimizing slow queries in handlers, reviewing EF Core access patterns.

N+1 prevention, row limit enforcement (`Take()`), `AsNoTracking` for read paths, explicit projection instead of loading full entities, application-side join prohibition.

```
"GetProducts handler is slow"
→ N+1 check + projection + pagination pattern
```

---

### testcontainers-integration-tests

**When:** Writing integration tests against real infrastructure.

Real PostgreSQL, Redis, and RabbitMQ containers via TestContainers. xUnit `IAsyncLifetime` + `CollectionFixture` pattern, Respawn for fast data reset between tests (~50ms vs 10-30s container recreation).

```
"Write integration test for Basket module"
→ PostgreSqlContainer + EF Core migration + Respawn setup
```

---

### microsoft-extensions-configuration

**When:** Binding appsettings to typed classes, validating config at startup.

`IOptions<T>` binding, `IValidateOptions<T>` for cross-property validation, `.ValidateOnStart()` to fail fast. Prevents silent runtime failures from missing or invalid configuration.

```
"Connection string is null at runtime"
→ Typed settings class + ValidateOnStart pattern
```

---

### dependency-injection-patterns

**When:** Organizing DI registrations, debugging lifetime issues.

`Add[Module]Module` extension method composition, captive dependency detection (scoped in singleton), `IServiceScopeFactory` pattern for background services. Directly relevant to Scrutor decorator + Redis cache combination.

```
"BasketRepository behaves strangely in background worker"
→ Captive dependency diagnosis + scope pattern fix
```

---

## Claude Code Agents

Agents run as isolated sub-agents — zero cost to the main conversation context.

### dotnet-benchmark-designer

**When:** Designing performance benchmarks for handlers, pipelines, or infrastructure.

BenchmarkDotNet setup, memory diagnostics, parameterized benchmarks, anti-pattern detection (Debug mode measurement, insufficient warmup, shared state). Generates complete runnable benchmark code.

```
"Benchmark MediatR pipeline overhead"
→ Complete BenchmarkDotNet class with MemoryDiagnoser
```

---

### dotnet-performance-analyst

**When:** Analyzing profiling results, interpreting benchmark output, detecting regressions.

BenchmarkDotNet result interpretation, JetBrains dotTrace/dotMemory analysis, closure allocation detection in hot paths, bottleneck identification (CPU-bound, memory-bound, I/O-bound, lock contention).

```
"Why is my handler allocating 2KB per request?"
→ Closure allocation analysis + delegate caching fix
```
