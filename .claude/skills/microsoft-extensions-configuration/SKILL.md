---
name: microsoft-extensions-configuration
description: Microsoft.Extensions.Options patterns including IValidateOptions, strongly-typed settings, validation on startup, and the Options pattern for clean configuration management.
invocable: false
---

# Microsoft.Extensions Configuration Patterns

## When to Use This Skill

Use this skill when:
- Binding configuration from appsettings.json to strongly-typed classes
- Validating configuration at application startup (fail fast)
- Implementing complex validation logic for settings
- Designing configuration classes that are testable and maintainable
- Understanding IOptions<T>, IOptionsSnapshot<T>, and IOptionsMonitor<T>

## Why Configuration Validation Matters

Applications often fail at runtime due to misconfiguration — missing connection strings, invalid URLs, out-of-range values. These failures happen deep in business logic, far from where configuration is loaded.

**The Solution:** Validate configuration at startup. If invalid, fail immediately with a clear error message.

```csharp
// BAD: Fails at runtime when someone tries to use the service
public class EmailService
{
    public EmailService(IOptions<SmtpSettings> options)
    {
        var settings = options.Value;
        _client = new SmtpClient(settings.Host, settings.Port); // NullReferenceException in prod!
    }
}

// GOOD: Fails at startup with: "SmtpSettings validation failed: Host is required"
```

---

## Pattern 1: Basic Options Binding

```csharp
public class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; } = true;
}
```

```csharp
builder.Services.AddOptions<SmtpSettings>()
    .BindConfiguration(SmtpSettings.SectionName);
```

```json
// appsettings.json
{
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "Username": "user@example.com",
    "Password": "secret"
  }
}
```

---

## Pattern 2: Data Annotations Validation

For simple rules — use Data Annotations + `.ValidateOnStart()`:

```csharp
using System.ComponentModel.DataAnnotations;

public class DatabaseSettings
{
    public const string SectionName = "Database";

    [Required(ErrorMessage = "Connection string is required")]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(1, 300, ErrorMessage = "CommandTimeout must be between 1 and 300 seconds")]
    public int CommandTimeout { get; set; } = 30;

    [Required]
    public string Schema { get; set; } = string.Empty;
}
```

```csharp
builder.Services.AddOptions<DatabaseSettings>()
    .BindConfiguration(DatabaseSettings.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();  // CRITICAL: fail at startup, not first use
```

---

## Pattern 3: IValidateOptions<T> for Complex Validation

For cross-property rules and conditional logic:

```csharp
public class SmtpSettingsValidator : IValidateOptions<SmtpSettings>
{
    public ValidateOptionsResult Validate(string? name, SmtpSettings options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Host))
            failures.Add("Host is required");

        if (options.Port is < 1 or > 65535)
            failures.Add($"Port {options.Port} is invalid. Must be between 1 and 65535");

        // Cross-property validation
        if (!string.IsNullOrEmpty(options.Username) && string.IsNullOrEmpty(options.Password))
            failures.Add("Password is required when Username is specified");

        // Conditional validation
        if (options.UseSsl && options.Port == 25)
            failures.Add("Port 25 is not used with SSL. Consider port 465 or 587");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
```

```csharp
builder.Services.AddOptions<SmtpSettings>()
    .BindConfiguration(SmtpSettings.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<SmtpSettings>, SmtpSettingsValidator>();
```

| Validation Type | Data Annotations | IValidateOptions |
|-----------------|------------------|------------------|
| Required field | Yes | Yes |
| Range check | Yes | Yes |
| Cross-property validation | No | Yes |
| Conditional validation | No | Yes |
| DI in validator | No | Yes |

---

## Pattern 4: Options Lifetime

| Interface | Lifetime | Reloads on Change | Use Case |
|-----------|----------|-------------------|----------|
| `IOptions<T>` | Singleton | No | Static config, read once |
| `IOptionsSnapshot<T>` | Scoped | Yes (per request) | Web apps needing fresh config |
| `IOptionsMonitor<T>` | Singleton | Yes (with callback) | Background services |

```csharp
// Background service: use IOptionsMonitor for live config updates
public class OutboxWorker : BackgroundService
{
    private readonly IOptionsMonitor<OutboxSettings> _optionsMonitor;

    public OutboxWorker(IOptionsMonitor<OutboxSettings> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = _optionsMonitor.CurrentValue;
            await DoWorkAsync();
            await Task.Delay(settings.PollingInterval, stoppingToken);
        }
    }
}
```

---

## Anti-Patterns to Avoid

```csharp
// BAD: Raw IConfiguration - no validation, no strong typing
public class MyService
{
    public MyService(IConfiguration configuration)
    {
        var host = configuration["Smtp:Host"]; // No validation!
    }
}

// GOOD: Strongly-typed, validated at startup
public class MyService
{
    public MyService(IOptions<SmtpSettings> options)
    {
        var host = options.Value.Host; // Validated at startup
    }
}
```

```csharp
// BAD: Missing ValidateOnStart — validation only runs when first accessed
builder.Services.AddOptions<Settings>()
    .ValidateDataAnnotations(); // Forgot ValidateOnStart!

// GOOD
builder.Services.AddOptions<Settings>()
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

```csharp
// BAD: Throwing in IValidateOptions breaks validation chain
public ValidateOptionsResult Validate(string? name, Settings options)
{
    if (options.Value < 0)
        throw new ArgumentException("Value cannot be negative"); // Wrong!
}

// GOOD: Return failure result
public ValidateOptionsResult Validate(string? name, Settings options)
{
    if (options.Value < 0)
        return ValidateOptionsResult.Fail("Value cannot be negative");
    return ValidateOptionsResult.Success;
}
```

---

## Summary

| Principle | Implementation |
|-----------|----------------|
| Fail fast | `.ValidateOnStart()` |
| Strongly-typed | Bind to POCO classes with `const SectionName` |
| Simple validation | Data Annotations |
| Complex/conditional validation | `IValidateOptions<T>` |
| Background services | `IOptionsMonitor<T>` |
| Per-request | `IOptionsSnapshot<T>` |
