---
name: testcontainers-integration-tests
description: Write integration tests using TestContainers for .NET with xUnit. Covers infrastructure testing with real databases, message queues, and caches in Docker containers instead of mocks.
invocable: false
---

# Integration Testing with TestContainers

## When to Use This Skill

Use this skill when:
- Writing integration tests that need real infrastructure (databases, caches, message queues)
- Testing data access layers against actual databases
- Verifying message queue integrations
- Testing Redis caching behavior
- Avoiding mocks for infrastructure components
- Testing database migrations and schema changes

## Reference Files

- [database-patterns.md](database-patterns.md): PostgreSQL and migration testing examples
- [infrastructure-patterns.md](infrastructure-patterns.md): Redis, RabbitMQ, multi-container networks, container reuse, and Respawn

## Core Principles

1. **Real Infrastructure Over Mocks** - Use actual databases/services in containers, not mocks
2. **Test Isolation** - Each test gets fresh containers or fresh data
3. **Automatic Cleanup** - TestContainers handles container lifecycle and cleanup
4. **Fast Startup** - Reuse containers across tests in the same class when appropriate
5. **CI/CD Compatible** - Works seamlessly in Docker-enabled CI environments
6. **Port Randomization** - Containers use random ports to avoid conflicts

## Why TestContainers Over Mocks?

```csharp
// BAD: Mocking a database - doesn't test real SQL, constraints, indexes
public class OrderRepositoryTests
{
    private readonly Mock<IDbConnection> _mockDb = new();

    [Fact]
    public async Task GetOrder_ReturnsOrder()
    {
        _mockDb.Setup(db => db.QueryAsync<Order>(It.IsAny<string>()))
            .ReturnsAsync(new[] { new Order { Id = 1 } });
        // Gives false confidence - misses SQL errors, FK violations, schema changes
    }
}

// GOOD: Testing against a real database
public class OrderRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer;
    private ApplicationDbContext _dbContext = null!;

    public OrderRepositoryTests()
    {
        _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;
        _dbContext = new ApplicationDbContext(options);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    [Fact]
    public async Task GetOrder_WithRealDatabase_ReturnsOrder()
    {
        // Arrange
        var order = Order.Create(customerId: Guid.NewGuid(), total: 100m);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _dbContext.Orders.FindAsync(order.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100m, result.Total);
    }
}
```

## Required NuGet Packages

```xml
<ItemGroup>
  <PackageReference Include="Testcontainers.PostgreSql" Version="*" />
  <PackageReference Include="Testcontainers.Redis" Version="*" />
  <PackageReference Include="Testcontainers.RabbitMq" Version="*" />
  <PackageReference Include="xunit" Version="*" />
  <PackageReference Include="xunit.runner.visualstudio" Version="*" />
  <PackageReference Include="Respawn" Version="*" />
</ItemGroup>
```

## Best Practices

1. **Always Use IAsyncLifetime** - Proper async setup and teardown
2. **Use Random Ports** - Let TestContainers assign ports automatically (`.WithPortBinding(5432, true)`)
3. **Reuse Containers When Possible** - Share via xUnit Collection Fixture
4. **Use Respawn** - Reset data between tests without recreating containers
5. **Test Real Queries** - Verify actual SQL behavior, constraints, indexes
6. **Use Alpine Images** - Smaller and faster (`postgres:16-alpine`)
7. **Always Dispose** - Always dispose containers in `DisposeAsync`

## CI/CD Integration

```yaml
# GitHub Actions
name: Integration Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest  # Has Docker pre-installed
    steps:
    - uses: actions/checkout@v3
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
    - name: Run Integration Tests
      run: dotnet test tests/IntegrationTests --filter Category=Integration
```

## Common Issues

**Container Startup Timeout:**
```csharp
_container = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilPortIsAvailable(5432)
        .WithTimeout(TimeSpan.FromMinutes(2)))
    .Build();
```

**Tests Fail in CI But Pass Locally:** Ensure CI runner has Docker support (`runs-on: ubuntu-latest`).

See reference files for complete PostgreSQL, Redis, RabbitMQ, and Respawn patterns.
