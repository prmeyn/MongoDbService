using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDbService.DTOs;

namespace MongoDbService
{
	public sealed class MongoService
	{
		private readonly MongoDbSettings _mongoDbSettings;
		public readonly string DatabaseName;
		public readonly MongoClient MongoClient;

		public MongoService(IConfiguration configuration, ILogger<MongoService> logger) 
		{
			var mongoDbSettings = configuration.GetSection("MongoDbSettings");

			var mongoDbConnectionString = mongoDbSettings["ConnectionString"];

			if (string.IsNullOrWhiteSpace(mongoDbConnectionString))
			{
				throw new ArgumentException("MongoDbSettings:ConnectionString missing.");
			}

			DatabaseName = mongoDbSettings["DatabaseName"];
			if (string.IsNullOrWhiteSpace(DatabaseName))
			{
				DatabaseName = $"Untitled-MongoDbService";
				logger.LogWarning($"MongoDbSettings:DatabaseName missing, falling back to {DatabaseName}");
			}

			_mongoDbSettings = new MongoDbSettings()
			{
				MongoDbConnectionString = mongoDbConnectionString,
				DatabaseName = DatabaseName
			};

			MongoClient = new MongoClient(mongoDbConnectionString);

			var connectionCollection = Database.GetCollection<ConnectionRecord>(nameof(ConnectionRecord), new MongoCollectionSettings() { ReadConcern = ReadConcern.Majority, WriteConcern = WriteConcern.WMajority });

			_ = connectionCollection.InsertOneAsync(new ConnectionRecord()
			{
				Id = Guid.NewGuid().ToString(),
				EnvironmentMachineName = Environment.MachineName,
				ConnectionDateTimeOffset = DateTimeOffset.UtcNow
			});
		}
		public IMongoDatabase Database => MongoClient.GetDatabase(_mongoDbSettings.DatabaseName);
	}
}
