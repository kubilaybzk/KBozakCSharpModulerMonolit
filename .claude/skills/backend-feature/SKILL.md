---
name: backend-feature
description: >
  Modüler monolith projesine yeni bir vertical slice feature eklerken veya mevcut bir feature'ı güncellerken kullan.
  Kullanıcı "yeni endpoint ekle", "feature yaz", "handler oluştur", "CQRS ile uygula", "bir use-case ekleyeyim",
  "command/query yaz" gibi şeyler söylediğinde bu skill'i devreye al. Carter + MediatR tabanlı VSA (Vertical Slice
  Architecture) projelerinde tek bir feature slice'ı doğru yapıyla oluşturmak için kritik rehberdir.
---

# Backend Feature Skill

## Amaç

Vertical Slice Architecture'da her use-case kendi bağımsız klasöründe yaşar. Bu skill o klasörü doğru yapıyla kurmanı sağlar: endpoint, handler, validator ve DTO'lar birbirine kenetli ama dış dünyaya kapalı.

## Klasör Yapısı

```
[Module]/
  [Domain]/
    Features/
      [FeatureName]/
        [FeatureName]Handler.cs    ← command/query + validator + handler
        [FeatureName]Endpoint.cs  ← sadece Carter routing + mapping
```

Her feature kendi klasöründe. İki dosyadan fazlası olursa feature büyüyor demektir — yeniden düşün.

## Handler.cs Şablonu

### Read (Query)

```csharp
namespace [Module].[Domain].Features.[FeatureName];

public record [Feature]Query([ParamType] [param]) : IQuery<[Feature]Result>;
public record [Feature]Result([ReturnType] [field]);

public class [Feature]QueryValidator : AbstractValidator<[Feature]Query>
{
    public [Feature]QueryValidator()
    {
        RuleFor(x => x.[param]).NotEmpty();
    }
}

// internal: bu handler dışarıdan doğrudan çağrılamaz — modül kapsüllenmesi böyle sağlanır
internal class [Feature]Handler([Module]DbContext db)
    : IQueryHandler<[Feature]Query, [Feature]Result>
{
    public async Task<[Feature]Result> Handle([Feature]Query query, CancellationToken ct)
    {
        var entity = await db.[Entities]
            .AsNoTracking()   // read-only — tracking gereksiz bellek harcar
            .FirstOrDefaultAsync(e => e.Id == query.[param], ct)
            ?? throw new [Entity]NotFoundException(query.[param]);

        return new [Feature]Result(entity.Adapt<[Entity]Dto>());
    }
}
```

### Write (Command)

```csharp
namespace [Module].[Domain].Features.[FeatureName];

// ICommand pipeline'da validation'ı otomatik tetikler — IQuery tetiklemez
public record [Feature]Command([FieldType] [field]) : ICommand<[Feature]Result>;
public record [Feature]Result([ReturnType] [returnField]);

public class [Feature]CommandValidator : AbstractValidator<[Feature]Command>
{
    public [Feature]CommandValidator()
    {
        RuleFor(x => x.[field]).NotEmpty();
    }
}

internal class [Feature]Handler([Module]DbContext db)
    : ICommandHandler<[Feature]Command, [Feature]Result>
{
    public async Task<[Feature]Result> Handle([Feature]Command cmd, CancellationToken ct)
    {
        var entity = [Entity].Create(cmd.[field]);
        db.[Entities].Add(entity);
        await db.SaveChangesAsync(ct);
        return new [Feature]Result(entity.Id);
    }
}
```

## Endpoint.cs Şablonu

```csharp
namespace [Module].[Domain].Features.[FeatureName];

// Endpoint'e ait request/response — handler'ın query/result'ından kasıtlı ayrı
// Endpoint transport detayı bilir (HTTP verb, route), handler bilmez
public record [Feature]Request([FieldType] [field]);
public record [Feature]Response([ReturnType] [returnField]);

public class [Feature]Endpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.Map[Verb]("/[route-prefix]/{[param]}", async ([ParamType] [param], ISender sender) =>
        {
            var result = await sender.Send(new [Feature]Query([param]));
            var response = result.Adapt<[Feature]Response>();
            return Results.Ok(response);
        })
        .WithName("[FeatureName]")
        .WithSummary("[Feature açıklaması]")
        .Produces<[Feature]Response>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("[Module]");
    }
}
```

## Kritik Kurallar

**Handler neden `internal`?**
Başka modüller bu handler'ı doğrudan çağırırsa modül sınırı deliniyor. `internal` bunu derleme seviyesinde engeller.

**Validator neden handler dosyasında?**
Feature kohezyon sağlar — birlikte değişirler. Validator'ı ayrı dosyaya koyarsan zamanla command ile senkronu kaçar.

**`AsNoTracking()` neden her read query'de?**
EF Core change tracker'ı açık bırakmak okuma operasyonlarında gereksiz bellek harcar ve hafif yavaşlatır. Mutation yapmayacaksan tracking'i kapat.

**`ICommand` vs `IQuery` neden önemli?**
Pipeline'daki `ValidationBehavior` sadece `ICommand` için çalışır. Query için validator yazdıysan ama `IQuery` kullandıysan validation hiç çalışmaz — sessiz hata.

## Endpoint Nedir, Ne Değildir

Endpoint sadece şunları yapar:
- HTTP request'i alır
- `ISender.Send()` ile handler'a delege eder
- Response'u döner

Endpoint şunları yapmaz:
- DbContext'e dokunmaz
- İş mantığı çalıştırmaz
- Başka servislere doğrudan çağrı yapmaz

## Review Checklist

- [ ] Feature klasörü modülün içinde mi?
- [ ] Endpoint sadece `ISender.Send()` mu yapıyor?
- [ ] Handler `internal` mı?
- [ ] Validator handler dosyasında mı?
- [ ] Read handler `AsNoTracking()` kullanıyor mu?
- [ ] `ICommand` mı yoksa `IQuery` mı — doğru seçildi mi?
- [ ] `Produces` / `ProducesProblem` gerçek davranışla eşleşiyor mu?
