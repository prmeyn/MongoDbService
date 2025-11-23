using Microsoft.Extensions.Configuration;
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
}
