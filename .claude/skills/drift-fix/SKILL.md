---
name: drift-fix
description: >
  Projede biriken mimari drift'i (kasıtlı sapmalar, convention ihlalleri, bilinen tutarsızlıklar) sistematik olarak düzelt.
  "Drift düzelt", "bilinen sorunları düzelt", "teknik borcu temizle", "convention'a uygun yap",
  "tutarsızlıkları gider", "status code yanlış", "catch bloğu sorunlu", "hardcoded veri var" gibi ifadelerde devreye gir.
  Her fix izole — birden fazlasını aynı anda değiştirme.
---

# Drift Fix Skill

## Drift Fix Yaklaşımı

Drift düzeltmek refactor'dan farklıdır — burada davranış da değişebilir. Bu yüzden:
1. **Hangi drift?** → Tam olarak belirle
2. **Risk seviyesi?** → Client veya sistem davranışı değişiyor mu?
3. **İzole et** → Tek bir drift per commit
4. **Aç belirle** → "Bu fix X davranışını Y'den Z'ye değiştirir"

## Drift Tipolojisi

### TİP 1: Status Code Tutarsızlığı

```
Belirti: .Produces(StatusCodes.Status201Created) ama handler Results.Ok() (200) döndürüyor
Risk: Client 201 bekliyor, 200 alıyor — düşük ama yanlış
```

**Fix seçenekleri:**
```csharp
// Seçenek A: Status code'u gerçek davranışa uydur (daha az kırıcı)
.Produces<[Feature]Response>(StatusCodes.Status200OK)

// Seçenek B: Gerçekten 201 döndür (semantik olarak daha doğru)
return Results.Created($"/[route]/{result.Id}", response);
```

---

### TİP 2: Blanket Catch — Hata Yutma

```
Belirti: catch (Exception) { return new Result(false); }
Risk: Hata gizlenir, debug edilemez, monitoring'e gitmez
```

**Fix:**
```csharp
// try-catch bloğunu kaldır — exception pipeline'a yayılsın
// Global exception handler (CustomExceptionHandler) zaten yakalar
public async Task<[Feature]Result> Handle([Feature]Command cmd, CancellationToken ct)
{
    // iş mantığı — try-catch YOK
    await db.SaveChangesAsync(ct);
    return new [Feature]Result(true);
}
```

**Uyarı:** Client şu an `false` alıyorsa, fix sonrası 500 alacak — beklenen ve doğru davranış.

---

### TİP 3: Hardcoded Event Consumer Data

```
Belirti: Consumer'da new [Entity]("Hardcoded Value", fixedNumber)
Risk: Event payload yetersiz, consumer gerçek veriyi kullanamıyor
```

**Fix — iki adımlı:**

Adım 1: Event payload'ı genişlet:
```csharp
// Integration event'e eksik alanları ekle
public record [Domain]Event(
    // mevcut alanlar...
    IReadOnlyList<[Item]Dto> Items  // ← ekle
) : IntegrationEvent;
```

Adım 2: Consumer gerçek veriyi kullansın:
```csharp
public async Task Consume(ConsumeContext<[Domain]Event> context)
{
    var cmd = context.Message.Adapt<[Target]Command>();
    // Items artık Adapt ile otomatik map edilir
    await sender.Send(cmd);
}
```

**Uyarı:** Event schema değişikliği — publisher ve consumer birlikte deploy edilmeli.

---

### TİP 4: Route Büyük Harf

```
Belirti: Results.Created($"/[Module]s/{id}") — büyük harf
Risk: REST convention bozulur, bazı client'lar case-sensitive
```

**Fix:**
```csharp
return Results.Created($"/[module]s/{result.Id}", response);
```

---

### TİP 5: Naming Drift

```
Belirti: [Feature]sEndpoint (çoğul), ServiceExtentions (typo)
Risk: Convention bozulur, gelecek katkıcılar kopyalar
```

**Fix:**
```
[Feature]sEndpoint → [Feature]Endpoint
ServiceExtentions  → ServiceExtensions
```

Call site'ları da güncelle — derleyici yönlendirir.

## Fix Öncelik Sırası

1. **Naming/typo drift** — Güvenli, hiç risk yok
2. **Status code tutarsızlığı** — Güvenli, client impact minimal
3. **Blanket catch** — Dikkat: client davranışı değişir
4. **Route büyük harf** — Dikkat: bazı client'lar etkilenebilir
5. **Event schema** — En riskli: koordineli deploy gerekir

## Çıktı Formatı

```
# Drift Fix: [Drift Tipi]

## Ne Değişiyor
Önceki davranış: ...
Yeni davranış: ...

## Risk
[Düşük/Orta/Yüksek]: ...

## Değişen Dosyalar
- path/to/File.cs: satır X-Y
```
