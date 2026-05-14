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
