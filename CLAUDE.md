# KozakCSharpModularMonolith

Read this file first. Keep it in memory; open deeper `.claude/*` files only when needed.

## Core Rules

- ASP.NET Core 8 modular monolith.
- Main modules: `Catalog`, `Basket`, `Ordering`.
- Shared libs: `Shared`, `Shared.Contracts`, `Shared.Messaging`.
- No direct implementation-to-implementation module references.
- Sync cross-module access via contracts.
- Async cross-module access via integration events.
- One feature folder per use case.
- Preferred pair: `[Feature]Endpoint.cs` and `[Feature]Handler.cs`.
- Endpoints stay thin; handlers are `internal`.
- One `DbContext` and one schema per module: `catalog`, `basket`, `ordering`.
- Prefer intended architecture over accidental drift.

## Load Only If Needed

- Full compact rules: [.claude/CLAUDE_RULES.md](/Users/dora_smart/Desktop/EshopModularMonoliths/.claude/CLAUDE_RULES.md:1)

## Skills (Auto-trigger based on task)

| Görev | Skill |
|-------|-------|
| Yeni feature / endpoint ekle | `backend-feature` |
| Yeni modül scaffold | `new-module` |
| EF Core migration | `ef-migration` |
| Cross-module iletişim tasarımı | `integration-design` |
| Kod inceleme / PR review | `code-review` |
| Refactor / isimlendirme düzeltme | `refactor` |
| Bilinen drift düzeltme | `drift-fix` |
| EF Core query/tracking/migration patterns | `efcore-patterns` |
| DB performans, N+1, row limit, projeksiyon | `database-performance` |
| Integration test, PostgreSQL/Redis/RabbitMQ container | `testcontainers-integration-tests` |
| Typed settings, IValidateOptions, startup validation | `microsoft-extensions-configuration` |
| DI extension methods, captive dependency, scope | `dependency-injection-patterns` |
| Redis cache, Scrutor decorator, TTL, fail-open | `redis-patterns` |
| Aggregate, Entity, domain event, value object, DDD | `ddd-patterns` |

## Known Drift To Avoid Copying

- `*Endpoints` — doğrusu `*Endpoint`
- `*Extentions` — doğrusu `*Extensions`
- blanket `catch` in basket checkout
- hardcoded order items in ordering consumer
- assuming query validators run automatically
