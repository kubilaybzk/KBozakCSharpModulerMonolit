# Infrastructure Testing Patterns

Patterns for testing Redis, RabbitMQ, multi-container networks, container reuse, and database reset with Respawn.

## Redis Integration Tests

```csharp
public class RedisTests : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer;
    private IConnectionMultiplexer _redis = null!;

    public RedisTests()
    {
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();
        _redis = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _redis.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task Redis_ShouldCacheValues()
    {
        var db = _redis.GetDatabase();

        await db.StringSetAsync("key1", "value1");
        var value = await db.StringGetAsync("key1");

        Assert.Equal("value1", value.ToString());
    }

    [Fact]
    public async Task Redis_ShouldExpireKeys()
    {
        var db = _redis.GetDatabase();

        await db.StringSetAsync("temp-key", "temp-value", expiry: TimeSpan.FromMilliseconds(500));
        Assert.True(await db.KeyExistsAsync("temp-key"));

        await Task.Delay(700);
        Assert.False(await db.KeyExistsAsync("temp-key"));
    }
}
```

## RabbitMQ Integration Tests

```csharp
public class RabbitMqTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitContainer;
    private IConnection _connection = null!;

    public RabbitMqTests()
    {
        _rabbitContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _rabbitContainer.StartAsync();

        var factory = new ConnectionFactory
        {
            Uri = new Uri(_rabbitContainer.GetConnectionString())
        };

        _connection = await factory.CreateConnectionAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.CloseAsync();
        await _rabbitContainer.DisposeAsync();
    }

    [Fact]
    public async Task RabbitMq_ShouldPublishAndConsumeMessage()
    {
        using var channel = await _connection.CreateChannelAsync();

        var queueName = "test-queue";
        await channel.QueueDeclareAsync(queueName, durable: false, exclusive: false, autoDelete: true);

        var message = "Hello, RabbitMQ!";
        var body = Encoding.UTF8.GetBytes(message);
        await channel.BasicPublishAsync(exchange: "", routingKey: queueName, body: body);

        var consumer = new EventingBasicConsumer(channel);
        var tcs = new TaskCompletionSource<string>();
        consumer.Received += (_, ea) => tcs.SetResult(Encoding.UTF8.GetString(ea.Body.ToArray()));
        await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(message, received);
    }
}
```

## Reusing Containers Across Tests (Collection Fixture)

For faster test execution, share containers across tests in a collection:

```csharp
// Shared fixture - one container for all tests in the collection
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    public string ConnectionString { get; private set; } = null!;
    private Respawner _respawner = null!;

    public DatabaseFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Run migrations once
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var ctx = new ApplicationDbContext(options);
        await ctx.Database.MigrateAsync();

        // Set up Respawn for fast data resets
        _respawner = await Respawner.CreateAsync(ConnectionString, new RespawnerOptions
        {
            TablesToIgnore = new Table[] { "__EFMigrationsHistory" },
            DbAdapter = DbAdapter.Postgres
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }

[Collection("Database")]
public class OrderTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;

    public OrderTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync(); // Fresh data each test
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateOrder_ShouldPersist()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        await using var ctx = new ApplicationDbContext(options);

        var order = Order.Create(customerId: Guid.NewGuid(), total: 100m);
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var saved = await ctx.Orders.FindAsync(order.Id);
        Assert.NotNull(saved);
    }
}
```

## Database Reset with Respawn

Respawn is ~50ms vs 10-30s for container recreation:

```csharp
// Required package: Respawn

var respawner = await Respawner.CreateAsync(connectionString, new RespawnerOptions
{
    TablesToIgnore = new Table[]
    {
        "__EFMigrationsHistory",
        new Table("catalog", "products_seed_tracking"),
    },
    SchemasToInclude = new[] { "catalog", "basket", "ordering" },
    DbAdapter = DbAdapter.Postgres,
    WithReseed = true
});

// In each test's InitializeAsync:
await respawner.ResetAsync(connectionString);
```

### Why Respawn Over Container Recreation

| Approach | Speed | Isolation |
|----------|-------|-----------|
| New container per test | ~10-30s | Complete |
| **Respawn** | **~50ms** | Data only (schema preserved) |
| Transaction rollback | ~1ms | Can't test commit behavior |
