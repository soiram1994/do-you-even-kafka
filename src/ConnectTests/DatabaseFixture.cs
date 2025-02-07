using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;

namespace ConnectTests;

public class DatabaseFixture : IAsyncLifetime
{
    public IContainer PostgresContainer { get; private set; }
    public string ConnectionString { get; private set; }

    public DatabaseFixture()
    {
        PostgresContainer = new ContainerBuilder()
            .WithImage("postgres:latest")
            .WithName("postgres")
            .WithNetworkAliases("postgres")
            .WithEnvironment("POSTGRES_USER", "testuser")
            .WithEnvironment("POSTGRES_PASSWORD", "testpass")
            .WithEnvironment("POSTGRES_DB", "testdb")
            .WithPortBinding(5432, true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await PostgresContainer.StartAsync();
        ConnectionString =
            $"Host=localhost;Port={PostgresContainer.GetMappedPublicPort(5432)};Username=testuser;Password=testpass;Database=testdb";
        await Task.Delay(TimeSpan.FromSeconds(5)); // Allow PostgreSQL to start
    }

    public async Task SeedDatabaseAsync()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var command =
            new NpgsqlCommand("CREATE TABLE IF NOT EXISTS test_table (id SERIAL PRIMARY KEY, name TEXT);", connection);
        await command.ExecuteNonQueryAsync();

        using var insertCommand =
            new NpgsqlCommand("INSERT INTO test_table (name) VALUES ('Kafka JDBC Test');", connection);
        await insertCommand.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await PostgresContainer.StopAsync();
    }
}