using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDbService.DTOs;

namespace MongoDbService
{
	public sealed class MongoService
	{
		public string DatabaseName { get; }
		public MongoClient MongoClient { get; }

		public MongoService(IConfiguration configuration, ILogger<MongoService> logger)
		{
			var mongoDbSettings = configuration.GetSection("MongoDbSettings");

			var mongoDbConnectionString = mongoDbSettings["ConnectionString"];

			if (string.IsNullOrWhiteSpace(mongoDbConnectionString))
			{
				throw new ArgumentException("MongoDbSettings:ConnectionString missing.", nameof(configuration));
			}

			DatabaseName = mongoDbSettings["DatabaseName"] ?? string.Empty;
			if (string.IsNullOrWhiteSpace(DatabaseName))
			{
				DatabaseName = "Untitled-MongoDbService";
				logger.LogWarning("MongoDbSettings:DatabaseName missing, falling back to {DatabaseName}", DatabaseName);
			}

			MongoClient = new MongoClient(mongoDbConnectionString);
			Database = MongoClient.GetDatabase(DatabaseName);

			var connectionCollection = Database.GetCollection<ConnectionRecord>(nameof(ConnectionRecord), new MongoCollectionSettings() { ReadConcern = ReadConcern.Majority, WriteConcern = WriteConcern.WMajority });

			_ = RecordConnectionAsync(connectionCollection, logger);
		}
		public IMongoDatabase Database { get; }

		private static async Task RecordConnectionAsync(IMongoCollection<ConnectionRecord> connectionCollection, ILogger<MongoService> logger)
		{
			try
			{
				await connectionCollection.InsertOneAsync(new ConnectionRecord()
				{
					Id = Guid.NewGuid().ToString(),
					EnvironmentMachineName = Environment.MachineName,
					ConnectionDateTimeOffset = DateTimeOffset.UtcNow
				});
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Failed to record MongoDbService connection.");
			}
		}
	}
}
