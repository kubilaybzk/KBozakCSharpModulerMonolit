---
name: integration-design
description: >
  Bir modülün diğer modülle konuşması gerektiğinde — sync veya async iletişim tasarımı için kullan.
  "Integration event", "cross-module", "modüller arası", "consumer yaz", "outbox", "contract",
  "modül A'dan modül B'ye", "event publish", "MassTransit", "RabbitMQ" gibi ifadelerde devreye gir.
  Hangi pattern'i seçeceğini, nereye koyacağını, nasıl yazacağını gösterir.
---

# Integration Design Skill

## Karar: Sync mi Async mi?

Bu soruyu sor: "Cevap şu anda gerekli mi, yoksa sonra olsa da olur mu?"

```
İstek-cevap zorunluysa (kullanıcı bekliyorsa)
  → SYNC: Contract + IQuery

Eventual consistency yeterliyse (fire-and-forget)
  → ASYNC: Integration Event + Consumer + ICommand
```

**İyi sync örneği:** Sipariş oluşturulurken ürün fiyatı kontrol edilmesi
**İyi async örneği:** Ödeme başarılı olduğunda bildirim gönderilmesi

## Sync: Contract Pattern

Contract, modülün dışarıya sunduğu public interface'dir. Implementation gizli kalır.

**Yerleşim:** `[ModuleA].Contracts/[Domain]/Features/[FeatureName]/`

```csharp
// [ModuleA].Contracts projesinde
namespace [ModuleA].Contracts.[Domain].Features.[FeatureName];

// ModuleB bu query'yi kullanır ama ModuleA'nın implementation'ını hiç bilmez
public record Get[Entity]ByIdQuery(Guid Id) : IQuery<Get[Entity]ByIdResult>;
public record Get[Entity]ByIdResult(Guid Id, string Name, decimal Price);
```

**Handler ModuleA'nın implementation projesinde** (normal feature gibi):
```csharp
internal class Get[Entity]ByIdHandler([Module]DbContext db)
    : IQueryHandler<Get[Entity]ByIdQuery, Get[Entity]ByIdResult>
{
    public async Task<Get[Entity]ByIdResult> Handle(...)
    {
        // implementation burada
    }
}
```

## Async: Integration Event Pattern

**Yerleşim:** `Shared.Messaging/Events/`

```csharp
// Shared.Messaging projesinde
namespace Shared.Messaging.Events;

// Payload: consumer'ın işini tamamlamak için ihtiyaç duyduğu her şey burada olmalı
// Computed/türetilen değerleri buraya koy — olay anındaki değeri sabitler
public record [Domain]Event(
    Guid [EntityId],
    string [Field1],
    decimal [AmountField],
    Guid [RelatedEntityId]   // consumer tekrar sorgu yapmak zorunda kalmasın
) : IntegrationEvent;
```

**Consumer — ModuleB'de:**
```csharp
namespace [ModuleB].Consumers;

// Consumer iş mantığı YAZMAZ — sadece event'i command'a çevirir
// İş mantığı ilgili Handler'da kalır: test edilebilir, izole
public class [Domain]EventConsumer(ISender sender)
    : IConsumer<[Domain]Event>
{
    public async Task Consume(ConsumeContext<[Domain]Event> context)
    {
        var cmd = context.Message.Adapt<[TargetCommand]>();
        await sender.Send(cmd);
    }
}
```

## Outbox: Atomik Publish

Business değişikliği ve event publish'in birlikte atomik olması gerekiyorsa outbox kullan.

```csharp
// Handler içinde — iş değişikliği ve publish aynı transaction
public async Task<[Feature]Result> Handle([Feature]Command cmd, CancellationToken ct)
{
    var entity = await db.[Entities].FindAsync(cmd.Id, ct)
        ?? throw new [Entity]NotFoundException(cmd.Id);

    entity.DoBusinessThing();

    var @event = cmd.Adapt<[Domain]Event>();

    db.Remove(entity);          // ya da Update
    await bus.Publish(@event, ct);    // MassTransit Outbox middleware yakalar
    await db.SaveChangesAsync(ct);    // ikisi birlikte commit

    return new [Feature]Result(true);
}
```

## Payload Completeness Kontrolü

Event payload'ı yazmadan önce şunu sor:

- Consumer bu event'teki bilgilerle işini **tamamen** bitirebilir mi?
- Downstream'e **tekrar sorgu** yapmak zorunda kalacak mı?
- Fiyat, miktar gibi değerler event anındaki değeri mi, computed property mi?

Eğer consumer ek sorgu yapıyorsa → payload yetersiz, genişlet.
Eğer computed property kullanıyorsan → event zamanındaki değeri field olarak kaydet.

## Yerleşim Özeti

| Ne                      | Nerede                          |
|-------------------------|---------------------------------|
| Sync contract (query)   | `[ModuleA].Contracts/`          |
| Async event             | `Shared.Messaging/Events/`      |
| Consumer                | `[ModuleB]/Consumers/`          |
| Handler (contract impl) | `[ModuleA]/[Feature]Handler.cs` |

## Review Checklist

- [ ] Sync mi async mi — doğru seçildi mi?
- [ ] Contract doğru projede mi? (implementation'da değil, contracts'ta)
- [ ] Consumer sadece delegasyon mu yapıyor?
- [ ] Event payload consumer'ın tüm ihtiyacını karşılıyor mu?
- [ ] Outbox gerekli mi? (atomicity var mı?)
- [ ] Hata yönetimi görünür mü, sessizce yutulmuyor mu?
