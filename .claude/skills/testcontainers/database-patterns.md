# Database Testing Patterns

Full code examples for testing with PostgreSQL and database migrations using TestContainers.

## PostgreSQL Integration Tests

```csharp
public class PostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer;
    private NpgsqlConnection _connection = null!;

    public PostgreSqlTests()
    {
        _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        _connection = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    [Fact]
    public async Task PostgreSql_ShouldHandleTransactions()
    {
        await _connection.ExecuteAsync(@"
            CREATE TABLE orders (
                id SERIAL PRIMARY KEY,
                customer_id VARCHAR(50) NOT NULL,
                total NUMERIC(10,2) NOT NULL
            )");

        using var transaction = await _connection.BeginTransactionAsync();

        await _connection.ExecuteAsync(
            "INSERT INTO orders (customer_id, total) VALUES (@CustomerId, @Total)",
            new { CustomerId = "CUST1", Total = 100.00m },
            transaction);

        await transaction.RollbackAsync();

        var count = await _connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM orders");

        Assert.Equal(0, count); // Rollback should prevent insert
    }
}
```

## Testing EF Core Migrations with Real Database

```csharp
public class MigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private ApplicationDbContext _dbContext = null!;

    public MigrationTests()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Migrations_ShouldRunSuccessfully()
    {
        await _dbContext.Database.MigrateAsync();

        var canConnect = await _dbContext.Database.CanConnectAsync();
        Assert.True(canConnect);

        var pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync();
        Assert.Empty(pendingMigrations);
    }

    [Fact]
    public async Task Migrations_ShouldBeIdempotent()
    {
        await _dbContext.Database.MigrateAsync();
        // Running again should not throw
        await _dbContext.Database.MigrateAsync();
    }
}
```
