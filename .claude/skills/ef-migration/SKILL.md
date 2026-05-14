---
name: ef-migration
description: >
  EF Core migration oluşturma, uygulama, seed data yazma, schema yönetimi için kullan.
  "Migration ekle", "EF migration", "veritabanı şeması", "migration oluştur", "dotnet ef",
  "seed data", "entity eklendi migration gerekiyor", "schema değişikliği" gibi durumlarda tetikle.
  Modül başına izole schema sahipliği ilkesini uygular.
---

# EF Migration Skill

## Temel İlke: Schema Sahipliği

Her modülün migration'ları **sadece kendi projesinde** yaşar. Başka modülün schema'sına migration yazamazsın — bu boundary ihlalidir.

Her modül kendi `HasDefaultSchema("[module]")` ayarını DbContext'inde tanımlar. Tablo isimleri çakışmaz, migration'lar bağımsız kalır.

## Migration Oluşturma

```bash
dotnet ef migrations add [MigrationIsmi] \
  --project src/Modules/[Module]/[Module] \
  --startup-project src/Bootstrapper/Api \
  --context [Module]DbContext \
  --output-dir Data/Migrations
```

**İyi migration isimleri:**
```
AddUserProfileTable       ✓  (ne eklendi)
AddEmailColumnToUsers     ✓  (hangi kolona ne yapıldı)
InitialCreate             ✓  (ilk migration)
CreateIndexOnOrderDate    ✓  (ne oluşturuldu)

Fix                       ✗  (ne fix'lendi?)
Update                    ✗  (neyi update etti?)
Migration20240101         ✗  (tarih isim değil)
```

## Migration Silme (Henüz Apply Edilmemişse)

```bash
dotnet ef migrations remove \
  --project src/Modules/[Module]/[Module] \
  --startup-project src/Bootstrapper/Api \
  --context [Module]DbContext
```

## Startup Migration Pattern

Migration'lar uygulama başlarken otomatik çalışır — `Use[Module]Module()` içinde:

```csharp
public static IApplicationBuilder Use[Module]Module(this IApplicationBuilder app)
{
    app.UseMigration<[Module]DbContext>();  // migration + seed tetikler
    return app;
}
```

Yeni migration ekledikten sonra bu çağrının zaten mevcut olduğunu doğrula — eksikse ekle.

## EF Fluent API: Konfigürasyon Şablonu

```csharp
// Data/Configurations/[Entity]Configuration.cs
public class [Entity]Configuration : IEntityTypeConfiguration<[Entity]>
{
    public void Configure(EntityTypeBuilder<[Entity]> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Amount)
            .HasColumnType("decimal(18,2)");

        // Value Object mapping
        builder.OwnsOne(e => e.Address, a =>
        {
            a.Property(x => x.Street).HasMaxLength(200);
            a.Property(x => x.City).HasMaxLength(100);
        });
    }
}
```

DbContext'te schema ve konfigürasyon tarama:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema("[module_lowercase]");  // kritik
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    base.OnModelCreating(modelBuilder);
}
```

## Seed Data Pattern

```csharp
public class [Module]DataSeeder([Module]DbContext db) : IDataSeeder
{
    public async Task SeedAllAsync()
    {
        if (await db.[Entities].AnyAsync()) return;  // idempotent — iki kez çalışmaz

        var items = GetInitialData();
        db.[Entities].AddRange(items);
        await db.SaveChangesAsync();
    }
}
```

`IDataSeeder` registration:
```csharp
services.AddScoped<IDataSeeder, [Module]DataSeeder>();
```

## Tehlikeli Durumlar

**Column drop/rename** → Canlı data kaybolur. `MigrationBuilder.RenameColumn()` kullan.

**NOT NULL column ekleme (var olan data varsa)** → Default value ver ya da nullable yap:
```csharp
migrationBuilder.AddColumn<string>(
    name: "NewField",
    table: "Users",
    schema: "[module]",
    nullable: true,     // önce nullable
    defaultValue: null);
```

**Migration çatışması (iki kişi aynı anda)** → Son migration'ı sil, diğerini pull et, kendi migration'ını yeniden oluştur.

## Checklist

- [ ] Migration doğru modülün projesinde mi?
- [ ] Migration ismi değişikliği açıklıyor mu?
- [ ] `Use[Module]Module()` çağrısı Program.cs'te var mı?
- [ ] Seed data idempotent mi? (`AnyAsync()` kontrolü var mı?)
- [ ] Destructive değişiklik var mı? (column drop, rename)
- [ ] Schema adı modülle eşleşiyor mu?
