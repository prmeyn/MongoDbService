using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MongoDbService.Tests;

public class MongoServiceIntegrationTests
{
    [Fact]
    public async Task Should_Create_And_Retrieve_Data()
    {
        // Arrange
        var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") 
                               ?? "mongodb://localhost:27017";
        
        var inMemorySettings = new Dictionary<string, string> {
            {"MongoDbSettings:ConnectionString", connectionString},
            {"MongoDbSettings:DatabaseName", "TestDatabase"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var logger = NullLogger<MongoService>.Instance;
        var mongoService = new MongoService(configuration, logger);

        try
        {
            // Act
            // 1. Verify Database Name
            Assert.Equal("TestDatabase", mongoService.DatabaseName);

            // 2. Create Data
            const int expectedValue = 123;
            var collection = mongoService.Database.GetCollection<BsonDocument>("IntegrationTestCollection");
            var testDoc = new BsonDocument { { "Name", "TestItem" }, { "Value", expectedValue } };
            await collection.InsertOneAsync(testDoc);

            // 3. Retrieve Data
            var retrievedDoc = await collection.Find(new BsonDocument("Name", "TestItem")).FirstOrDefaultAsync();

            // Assert
            Assert.NotNull(retrievedDoc);
            Assert.Equal(expectedValue, retrievedDoc["Value"].AsInt32);
        }
        finally
        {
            // Cleanup: Drop the database
            await mongoService.MongoClient.DropDatabaseAsync("TestDatabase");
        }
    }

    [Fact]
    public async Task Should_Not_Throw_When_Connection_Tracking_Write_Fails()
    {
        // Arrange: a connection string that will fail fast instead of hanging.
        var inMemorySettings = new Dictionary<string, string> {
            {"MongoDbSettings:ConnectionString", "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=200&connectTimeoutMS=200"},
            {"MongoDbSettings:DatabaseName", "TestDatabase"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var logger = new RecordingLogger<MongoService>();

        // Act
        var exception = Record.Exception(() => new MongoService(configuration, logger));

        // Assert: construction itself never throws, even though the tracking write is doomed to fail.
        Assert.Null(exception);

        // The tracking write is fire-and-forget, so give it a moment to fail and get logged
        // instead of being silently swallowed.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && !logger.HasWarningWithException)
        {
            await Task.Delay(50);
        }

        Assert.True(logger.HasWarningWithException, "Expected the failed connection-tracking write to be logged as a warning.");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private volatile bool _hasWarningWithException;

        public bool HasWarningWithException => _hasWarningWithException;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning && exception is not null)
            {
                _hasWarningWithException = true;
            }
        }
    }
}
