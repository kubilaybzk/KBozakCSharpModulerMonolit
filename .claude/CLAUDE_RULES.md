# Claude Rules For KozakCSharpModularMonolith

Use this as the compact source of truth. Load `ARCHITECTURE_ANALYSIS.md` only when deeper rationale is needed.

## Architecture

- Composition root is `src/Bootstrapper/Api/Program.cs`.
- Business modules: `Catalog`, `Basket`, `Ordering`.
- Shared libraries: `Shared`, `Shared.Contracts`, `Shared.Messaging`.
- No direct implementation-to-implementation module references.
- Sync cross-module access via contracts.
- Async cross-module access via integration events.
- One `DbContext` and one schema per module: `catalog`, `basket`, `ordering`.

## Structure

- Use VSA: one feature folder per use case.
- Preferred file pair: `[Feature]Endpoint.cs` and `[Feature]Handler.cs`.
- Endpoint is thin and transport-only.
- Handler owns orchestration and data access.
- Handlers stay `internal`.
- Command/query/result stay `record`.
- Validator stays in the handler file.

## CQRS And Validation

- Writes use `ICommand` / `ICommand<T>`.
- Reads use `IQuery<T>`.
- Validation is wired automatically for commands.
- Do not assume query validators run automatically.
- Read paths should normally use `AsNoTracking()`.

## Endpoint Conventions

- Carter `ICarterModule` + MediatR `ISender`.
- Use Mapster at the boundary when mapping is needed.
- Do not inject `DbContext` into endpoints.
- Keep `Produces`, `ProducesProblem`, `WithSummary`, `WithDescription` aligned with actual behavior.
- Route families:
  - Catalog: `/products`
  - Basket: `/basket`
  - Ordering: `/orders`

## Shared Placement

- `Shared/Behaviors`: pipeline behaviors
- `Shared/DDD`: base entity/aggregate/domain-event types
- `Shared/Exceptions`: generic exceptions and global handler
- `Shared/Extensions`: registration helpers
- `Shared.Messaging`: integration events and MassTransit wiring

## Domain And Data

- Prefer aggregate factory methods like `Create(...)`.
- Raise domain events inside aggregates.
- Keep invariants in aggregate/value-object methods.
- EF mapping uses Fluent API under `Data/Configurations`.
- Migrations stay under the owning module.
- Startup migration/seeding runs through `Use*Module()` and `UseMigration<TContext>()`.
- Only Catalog currently seeds initial data.

## Messaging And Outbox

- Domain events are in-process MediatR notifications.
- Integration events are RabbitMQ/MassTransit contracts.
- Consumers should translate events into commands.
- Basket checkout is the only outbox-backed flow today.
- If reliability matters across a boundary, prefer the outbox pattern.

## Auth And Security

- Keycloak authentication is enabled at the host level.
- Basket endpoints require authorization.
- Catalog and Ordering endpoints are currently anonymous.
- In Basket flows, prefer identity-derived `UserName` over trusting request bodies.

## Caching

- Basket caching sits behind `IBasketRepository`.
- Use the repository decorator, not direct cache access from handlers.

## Error Handling

- Let business exceptions flow to `CustomExceptionHandler`.
- Shared mappings:
  - validation -> 400
  - bad request -> 400
  - not found -> 404
  - unknown -> 500
- Do not copy blanket `catch` patterns that hide failures behind booleans.

## Naming

- Prefer:
  - `[Feature]Endpoint`
  - `[Feature]Command`
  - `[Feature]Query`
  - `[Feature]Result`
  - `[Feature]Request`
  - `[Feature]Response`
  - `[Feature]Handler`
  - `[Entity]Dto`
  - `[Entity]NotFoundException`
- Do not extend current drift:
  - `*Endpoints`
  - `*Extentions`

## Ops And Config

- Main local backing services: PostgreSQL, Redis, Seq, RabbitMQ, Keycloak.
- Config comes from `appsettings*.json`, env vars, and user secrets.
- Watch for env drift between `appsettings` and `docker-compose.override.yml`.

## Known Drift

- `CheckoutBasketHandler` swallows exceptions.
- Ordering checkout consumer uses hardcoded order items.
- `BasketRepository.GetBasket` calls `AsNoTracking()` without assignment.
- `CheckoutBasketEndpoint` declares `201` but returns `200`.
- `CreateOrderEndpoint` returns `/Orders/{id}` with uppercase `O`.
- `IntegrationEvent` metadata properties are computed, not stable stored values.
