---
name: refactor
description: >
  Davranışı değiştirmeden kodu temizle, isimlendirmeyi düzelt, yapıyı iyileştir.
  "Refactor et", "ismi düzelt", "temizle", "convention'a uygun yap", "yeniden adlandır",
  "organize et", "okunurluğu artır", "naming düzelt" gibi ifadelerde devreye gir.
  Modüler monolith convention'larına uygun behavior-preserving cleanup rehberi.
---

# Refactor Skill

## Başlamadan: Bu Gerçek Refactor mı?

Bu 4 soruyu sor:

1. **Davranış değişiyor mu?** → Değişiyorsa ayrı task aç, refactor değil
2. **Call site'lar etkileniyor mu?** → Var ise hepsini bul ve güncelle
3. **Mevcut bug sabitliyor mu?** → "Bu refactor X bug'ını sabitliyor" diye belirt
4. **EF mapping veya startup'a dokunuyor mu?** → Yüksek risk alanı, dikkat

## Güvenli Refactor Alanları

| Ne               | Örnek                                          | Risk  |
|------------------|------------------------------------------------|-------|
| İsim düzeltme    | `[Feature]sEndpoint` → `[Feature]Endpoint`    | Düşük |
| Typo düzeltme    | `ServiceExtentions` → `ServiceExtensions`      | Düşük |
| Status code hizalama | `Produces(201)` + `Results.Created(...)`   | Düşük |
| Validator taşıma | Ayrı dosyadan handler dosyasına               | Düşük |
| Handler okunurluğu | Uzun method'u private helper'a çıkarma      | Düşük |

## Riskli Alanlar — İki Kez Düşün

| Alan                      | Neden riskli                                  |
|---------------------------|-----------------------------------------------|
| Cross-module contract'lar | Downstream consumer'ları kırar               |
| Event payload shape       | Deserialization kırar, silent failure         |
| Auth attribute/policy     | `.RequireAuthorization()` silmek güvenlik açığı |
| EF mappings               | Schema değişikliği, migration gerekir         |
| Startup composition       | Modül registration sırası önemli olabilir     |
| docker-compose / appsettings | Env config drift açar                     |

## Naming Reference

```
# Doğru
[Feature]Endpoint       → CreateOrderEndpoint
[Feature]Command        → CreateOrderCommand
[Feature]Query          → GetOrderByIdQuery
[Feature]Result         → GetOrderByIdResult
[Feature]Handler        → CreateOrderHandler
[Entity]Dto             → OrderDto
[Entity]NotFoundException → OrderNotFoundException

# Yaygın drift (kopyalama)
[Feature]sEndpoint      ✗  (çoğul)
ServiceExtentions       ✗  (typo)
```

## Refactor Workflow

1. **Scope belirle** — tek dosya, single feature, bir modül
2. **Pre-check yap** — yukardaki 4 soru
3. **Küçük adımlar** — bir seferde bir concern
4. **Call site'ları güncelle** — derleyici yönlendirir
5. **Behavior değişikliklerini ayır** — ayrı commit

## Çıktı Formatı

Refactor önerisi yaparken:

```
# [Dosya adı] Refactor

## Güvenli Değişiklikler
- [Class ismi]: `OldName` → `NewName`
- [Route metadata]: `Produces(201)` → `Produces(200)` (actual behavior ile hizalama)

## Riskli Değişiklikler (onay gerekir)
- [Alan]: değişiklik + neden riskli

## Değiştirilmeyenler
- [Neden dokunulmadı]
```
