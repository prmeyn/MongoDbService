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

			// Connection tracking is diagnostic, so it takes the cheapest durable
			// write available rather than blocking on a majority acknowledgement.
			var connectionCollection = Database.GetCollection<ConnectionRecord>(nameof(ConnectionRecord), new MongoCollectionSettings() { ReadConcern = ReadConcern.Local, WriteConcern = WriteConcern.Acknowledged });

			ConnectionRecorded = RecordConnectionAsync(connectionCollection, logger);
		}
		public IMongoDatabase Database { get; }

		/// <summary>
		/// Completes when the startup connection-tracking write has finished. Never
		/// faults: a failed write is logged as a warning and swallowed, so awaiting
		/// this is always safe. Short-lived processes (console apps, CLIs, functions)
		/// should await it before exiting, otherwise the process can terminate before
		/// the write lands and the record is lost.
		/// </summary>
		public Task ConnectionRecorded { get; }

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

				logger.LogDebug("Recorded MongoDbService connection for {EnvironmentMachineName}.", Environment.MachineName);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Failed to record MongoDbService connection.");
			}
		}
	}
}
