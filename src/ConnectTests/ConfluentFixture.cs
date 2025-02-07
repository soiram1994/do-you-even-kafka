using Confluent.Kafka;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Kafka;

public class ConfluentFixture : IAsyncLifetime
{
    private INetwork _network;
    public KafkaContainer KafkaContainer { get; private set; }
    public IContainer SchemaRegistryContainer { get; private set; }
    public IContainer KafkaConnectContainer { get; private set; }

    public string SchemaRegistryUrl { get; private set; }
    public string KafkaConnectUrl { get; private set; }
    public string KafkaBootstrapServers { get; private set; }

    public ConfluentFixture()
    {
        _network = new NetworkBuilder()
            .WithName(Guid.NewGuid().ToString("D"))
            .Build();
        _network.CreateAsync().Wait();
        KafkaContainer = new KafkaBuilder()
            .WithHostname("kafka")
            .WithImage("confluentinc/cp-kafka:latest")
            .WithNetwork(_network)
            .WithName("kafka")
            .WithNetworkAliases("kafka")
            // enable auto topic creation
            .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "true")
            .WithEnvironment("KAFKA_CONFLUENT_SCHEMA_REGISTRY_URL", "http://schema-registry:8085")
            .WithCleanUp(true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await KafkaContainer.StartAsync();
        KafkaBootstrapServers = KafkaContainer.GetBootstrapAddress();
        await WaitForKafkaBrokerAsync();

        SchemaRegistryContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-schema-registry:latest")
            .WithName("scehma-registry")
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS", "PLAINTEXT://kafka:9093")
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_SECURITY_PROTOCOL", "PLAINTEXT")
            .WithEnvironment("SCHEMA_REGISTRY_LISTENERS", "http://0.0.0.0:8085")
            .WithEnvironment("SCHEMA_REGISTRY_HOST_NAME", "schema-registry")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server started, listening for requests"))
            .WithNetwork(_network)
            .WithNetworkAliases("schema-registry")
            .DependsOn(KafkaContainer)
            .WithPortBinding(8085, true)
            .Build();

        KafkaConnectContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-kafka-connect:latest")
            .WithEnvironment("CONNECT_BOOTSTRAP_SERVERS", "plaintext://kafka:9093")
            .WithEnvironment("CONNECT_REST_ADVERTISED_HOST_NAME", "kafka-connect")
            .WithEnvironment("CONNECT_GROUP_ID", "test-connect-group")
            .WithEnvironment("CONNECT_CONFIG_STORAGE_TOPIC", "connect-configs")
            .WithEnvironment("CONNECT_OFFSET_STORAGE_TOPIC", "connect-offsets")
            .WithEnvironment("CONNECT_STATUS_STORAGE_TOPIC", "connect-statuses")
            .WithEnvironment("CONNECT_KEY_CONVERTER", "org.apache.kafka.connect.storage.StringConverter")
            .WithEnvironment("CONNECT_VALUE_CONVERTER", "io.confluent.connect.avro.AvroConverter")
            .WithEnvironment("CONNECT_VALUE_CONVERTER_SCHEMA_REGISTRY_URL", "http://schema-registry:8085")
            .WithEnvironment("CONNECT_PLUGIN_PATH", "/usr/share/java,/usr/share/confluent-hub-components")
            .WithEnvironment("CONNECT_CONFIG_STORAGE_REPLICATION_FACTOR", "1")
            .WithEnvironment("CONNECT_OFFSET_STORAGE_REPLICATION_FACTOR", "1")
            .WithEnvironment("CONNECT_STATUS_STORAGE_REPLICATION_FACTOR", "1")
            .WithEnvironment("CONNECT_PLUGIN_PATH",
                "/usr/share/java,/usr/share/confluent-hub-components,/usr/share/java/kafka")
            .WithNetwork(_network)
            .WithCommand("/bin/sh", "-c",
                "confluent-hub install --no-prompt confluentinc/kafka-connect-jdbc:latest && " +
                "wget -P /usr/share/confluent-hub-components/confluentinc-kafka-connect-jdbc/lib https://jdbc.postgresql.org/download/postgresql-42.2.23.jar && " +
                "/etc/confluent/docker/run")
            .WithPortBinding(8083, true)
            .DependsOn(KafkaContainer)
            .Build();

        await SchemaRegistryContainer.StartAsync();
        await KafkaConnectContainer.StartAsync();

        SchemaRegistryUrl = $"http://localhost:{SchemaRegistryContainer.GetMappedPublicPort(8085)}";
        KafkaConnectUrl = $"http://localhost:{KafkaConnectContainer.GetMappedPublicPort(8083)}";


        await WaitForSchemaRegistryAsync();
        await WaitForKafkaConnectAsync();
        await ListAvailableConnectorsAsync();
    }

    public async Task DisposeAsync()
    {
        await KafkaConnectContainer.StopAsync();
        await SchemaRegistryContainer.StopAsync();
        await KafkaContainer.StopAsync();
    }

    private async Task WaitForKafkaBrokerAsync()
    {
        using var adminClient =
            new AdminClientBuilder(new AdminClientConfig { BootstrapServers = KafkaBootstrapServers }).Build();

        for (int i = 0; i < 30; i++) // Retry for up to 30 seconds
        {
            try
            {
                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
                if (metadata.Brokers.Count > 0)
                {
                    Console.WriteLine("✅ Kafka Broker is ready.");
                    return;
                }
            }
            catch (KafkaException)
            {
                Console.WriteLine("⏳ Waiting for Kafka Broker...");
            }

            await Task.Delay(1000);
        }

        throw new Exception("🚨 Kafka Broker did not become ready in time!");
    }

    private async Task WaitForSchemaRegistryAsync()
    {
        using var httpClient = new HttpClient();

        for (int i = 0; i < 60; i++) // Retry for up to 30 seconds
        {
            try
            {
                var response = await httpClient.GetAsync($"{SchemaRegistryUrl}/subjects");
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Schema Registry is ready.");
                    return;
                }
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("⏳ Waiting for Schema Registry...");
            }

            await Task.Delay(1000);
        }

        throw new Exception("🚨 Schema Registry did not become ready in time!");
    }

    private async Task WaitForKafkaConnectAsync()
    {
        using var httpClient = new HttpClient();

        for (int i = 0; i < 30; i++) // Retry for up to 30 seconds
        {
            try
            {
                var response = await httpClient.GetAsync($"{KafkaConnectUrl}/connectors");
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Kafka Connect is ready.");
                    return;
                }
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("⏳ Waiting for Kafka Connect...");
            }

            await Task.Delay(1000);
        }

        throw new Exception("🚨 Kafka Connect did not become ready in time!");
    }

    private async Task ListAvailableConnectorsAsync()
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetStringAsync($"{KafkaConnectUrl}/connector-plugins");

        Console.WriteLine($"🔌 Available Connectors: {response}");
    }
}