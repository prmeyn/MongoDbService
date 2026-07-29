using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDbService.DTOs;

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
            await collection.InsertOneAsync(testDoc, cancellationToken: TestContext.Current.CancellationToken);

            // 3. Retrieve Data
            var retrievedDoc = await collection.Find(new BsonDocument("Name", "TestItem")).FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(retrievedDoc);
            Assert.Equal(expectedValue, retrievedDoc["Value"].AsInt32);
        }
        finally
        {
            // Cleanup: Drop the database
            await mongoService.MongoClient.DropDatabaseAsync("TestDatabase", TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Should_Record_Connection_On_Construction()
    {
        // Arrange: a dedicated database, since the assertions below inspect the
        // ConnectionRecord collection that every MongoService construction writes to.
        var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
                               ?? "mongodb://localhost:27017";
        const string databaseName = "TestDatabase_ConnectionTracking";

        var inMemorySettings = new Dictionary<string, string> {
            {"MongoDbSettings:ConnectionString", connectionString},
            {"MongoDbSettings:DatabaseName", databaseName}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var mongoService = new MongoService(configuration, NullLogger<MongoService>.Instance);

        try
        {
            // Act: the write is fire-and-forget from the constructor, so await the task
            // that makes it observable. It is contractually non-faulting.
            await mongoService.ConnectionRecorded;

            // Assert: the record for this machine actually landed.
            var records = mongoService.Database.GetCollection<ConnectionRecord>(nameof(ConnectionRecord));
            var record = await records
                .Find(Builders<ConnectionRecord>.Filter.Eq(r => r.EnvironmentMachineName, Environment.MachineName))
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(record);
            Assert.False(string.IsNullOrWhiteSpace(record.Id));
            Assert.InRange(record.ConnectionDateTimeOffset, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));

            // Assert: the TTL index exists, so the collection cannot grow without bound.
            var indexCursor = await records.Indexes.ListAsync(TestContext.Current.CancellationToken);
            var indexes = await indexCursor.ToListAsync(TestContext.Current.CancellationToken);
            var ttlIndex = indexes.SingleOrDefault(i => i["name"].AsString == "ConnectionRecord_ttl");

            Assert.NotNull(ttlIndex);
            Assert.Equal(TimeSpan.FromDays(30).TotalSeconds, ttlIndex["expireAfterSeconds"].ToDouble());
        }
        finally
        {
            await mongoService.MongoClient.DropDatabaseAsync(databaseName, TestContext.Current.CancellationToken);
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
        MongoService? mongoService = null;
        var exception = Record.Exception(() => mongoService = new MongoService(configuration, logger));

        // Assert: construction itself never throws, even though the tracking write is doomed to fail.
        Assert.Null(exception);
        Assert.NotNull(mongoService);

        // ConnectionRecorded is contractually non-faulting, so awaiting it needs no
        // try/catch and settles the tracking write deterministically.
        await mongoService.ConnectionRecorded;

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
