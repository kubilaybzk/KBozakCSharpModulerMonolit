---
name: code-review
description: >
  Modüler monolith projesindeki kodu bug, mimari ihlal ve convention sorunları açısından incele.
  "Review et", "incele", "kontrol et", "sorun var mı", "PR bak", "audit", "güvenlik bak",
  "mimari uygun mu", "drift var mı" gibi ifadelerde devreye gir. Öncelikli sırayla çalışır:
  önce runtime, sonra mimari, sonra convention.
---

# Code Review Skill

## Çıktı Formatı

Her bulguyu bu formatta yaz — özet değil, bulgular:

```
[SEVİYE] Kategori: Kısa açıklama
Dosya: path/to/File.cs:satır
Neden sorun: ...
Düzeltme: ...
```

**Seviyeler:**
- `[P0-RUNTIME]` — Canlıda çöker veya yanlış davranır
- `[P1-ARCH]` — Modül sınırı ihlali veya mimari bozulma
- `[P2-CONV]` — Convention uyumsuzluğu, drift riski
- `[P3-WARN]` — Dikkat noktası, kritik değil

## İnceleme Sırası

1. **Runtime davranış** → Önce bunu bul
2. **Mimari sınırlar** → Sonra bunu
3. **Convention** → En son bunu

## P0 — Runtime Kontrolleri

```
❌ Status code tutarsızlığı:
   .Produces(StatusCodes.Status201Created) ama Results.Ok() döndürüyor

❌ Blanket catch:
   catch (Exception) { return false; }
   → Hata gizleniyor, debugging imkansız

❌ Query validation varsayımı:
   IQuery için AbstractValidator yazıldı ama pipeline otomatik çalıştırmıyor

❌ AsNoTracking eksikliği:
   Read query'de tracking açık — gereksiz bellek + hafif yavaşlama
```

## P1 — Mimari Kontrolleri

```
❌ Implementation-to-implementation referans:
   using [ModuleA].[Domain];  →  [ModuleB] projesinde
   Bu modül sınırını deler — contracts üzerinden git

❌ DbContext endpoint'te:
   MapGet("/route", async ([Module]DbContext db, ...) => ...)
   → Handler'a taşı

❌ Repository/cache decorator bypass:
   _cache.GetAsync(...)  →  handler içinde doğrudan
   → Repository üzerinden git

❌ Consumer iş mantığı yazıyor:
   new [Entity]("Hardcoded Value", 1)
   → Event payload yetersiz, consumer'ı hardcode etme
```

## P2 — Convention Kontrolleri

```
⚠️  Çoğul class ismi:
   [Feature]sEndpoint  →  doğrusu [Feature]Endpoint

⚠️  Typo:
   ServiceExtentions  →  doğrusu ServiceExtensions

⚠️  Büyük harf route:
   Results.Created($"/[Module]s/{id}")  →  küçük harf: /[module]s/{id}

⚠️  Auth eksikliği:
   Kimlik doğrulama gerektiren endpoint'te .RequireAuthorization() yok

⚠️  Config drift:
   appsettings.json ile docker-compose.override.yml farklı değerler
```

## Mevcut Drift: Flag Et vs Tolere Et

Projede zaten bilinen drift'ler var. Bunları yeni kodun **kopyalamamasını** sağla:

| Kopyalanmaması gereken drift          | Neden tehlikeli               |
|---------------------------------------|-------------------------------|
| Blanket `catch { return false; }`     | Hata yutulur, görünmez        |
| Hardcoded consumer mapping data       | Event payload sorunu büyür    |
| `Produces(201)` + `Results.Ok()`      | Client yanlış beklenti kurar  |
| `/Module/{id}` büyük harf route       | REST convention bozulur       |

Mevcut drift'i gördüğünde: "Bilinen drift, teknik borç" yaz. Genişleten yeni kod yazıldıysa P2 bug olarak işaretle.

## Sonuç Yapısı

Review bulguları listele, sonra kısa özet:

```
## Bulgular
[P0-RUNTIME] ...
[P1-ARCH] ...
[P2-CONV] ...

## Özet
X adet P0, Y adet P1, Z adet P2.
Öncelikli: [en kritik bulgu]
```
