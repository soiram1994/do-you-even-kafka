using System.Text;
using System.Text.Json;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;

namespace ConnectTests;

public class KafkaJdbcIntegrationTests(ConfluentFixture kafkaFixture, DatabaseFixture dbFixture)
    : IClassFixture<ConfluentFixture>, IClassFixture<DatabaseFixture>
{
    private const string TopicName = "jdbc-test_table";

    private async Task RegisterJdbcConnectorAsync()
    {
        using var httpClient = new HttpClient();

        var dbConnectionString = dbFixture.ConnectionString
            .Replace("Host=", "jdbc:postgresql://")
            .Replace(";Port=", ":")
            .Replace(";Database=", "/") // ✅ Fix database name placement
            .Replace(";Username=", "?user=") // ✅ Ensure proper query format
            .Replace(";Password=", "&password="); // ✅ Append password correctly

        string jsonPayload = $@"
{{
    ""name"": ""jdbc-source-connector"",
    ""config"": {{
        ""connector.class"": ""io.confluent.connect.jdbc.JdbcSourceConnector"",
        ""connection.url"": ""{dbConnectionString}"",
        ""connection.user"": ""testuser"",
        ""connection.password"": ""testpass"",
        ""database.driver.class"": ""org.postgresql.Driver"",
        ""table.whitelist"": ""test_table"",
        ""mode"": ""incrementing"",
        ""incrementing.column.name"": ""id"",
        ""topic.prefix"": ""jdbc-"",
        ""key.converter"": ""org.apache.kafka.connect.storage.StringConverter"",
        ""value.converter"": ""io.confluent.connect.avro.AvroConverter"",
        ""value.converter.schema.registry.url"": ""{kafkaFixture.SchemaRegistryUrl}""
    }}
}}";

        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{kafkaFixture.KafkaConnectUrl}/connectors", content);

        response.EnsureSuccessStatusCode();
    }

    private async Task ConsumeKafkaMessagesAsync()
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = "test-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var schemaRegistry = new CachedSchemaRegistryClient(new SchemaRegistryConfig
            { Url = kafkaFixture.SchemaRegistryUrl });

        using var consumer = new ConsumerBuilder<string, GenericRecord>(consumerConfig)
            .SetValueDeserializer(new AvroDeserializer<GenericRecord>(schemaRegistry).AsSyncOverAsync())
            .Build();

        consumer.Subscribe(TopicName);

        var consumeResult = consumer.Consume(TimeSpan.FromSeconds(10));

        Assert.NotNull(consumeResult);
        Assert.Equal("Kafka JDBC Test", consumeResult.Message.Value["name"]);
    }

    [Fact]
    public async Task KafkaJdbcIntegrationTest()
    {
        await dbFixture.SeedDatabaseAsync();
        await RegisterJdbcConnectorAsync();
        await ConsumeKafkaMessagesAsync();
    }
}