using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDbService.DTOs;

namespace MongoDbService
{
	public sealed class MongoService : IDisposable
	{
		private const int DefaultConnectionRecordRetentionDays = 30;
		private const string ConnectionRecordExpiryIndexName = "ConnectionRecord_ttl";

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

			ConnectionRecorded = RecordConnectionAsync(connectionCollection, ReadRetentionDays(mongoDbSettings, logger), logger);
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

		public void Dispose() => MongoClient.Dispose();

		private static int ReadRetentionDays(IConfigurationSection mongoDbSettings, ILogger<MongoService> logger)
		{
			var configured = mongoDbSettings["ConnectionRecordRetentionDays"];

			if (string.IsNullOrWhiteSpace(configured))
			{
				return DefaultConnectionRecordRetentionDays;
			}

			if (!int.TryParse(configured, out var retentionDays))
			{
				logger.LogWarning("MongoDbSettings:ConnectionRecordRetentionDays is not a number, falling back to {RetentionDays} days", DefaultConnectionRecordRetentionDays);
				return DefaultConnectionRecordRetentionDays;
			}

			return retentionDays;
		}

		private static async Task RecordConnectionAsync(IMongoCollection<ConnectionRecord> connectionCollection, int retentionDays, ILogger<MongoService> logger)
		{
			await EnsureExpiryIndexAsync(connectionCollection, retentionDays, logger);

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

		private static async Task EnsureExpiryIndexAsync(IMongoCollection<ConnectionRecord> connectionCollection, int retentionDays, ILogger<MongoService> logger)
		{
			if (retentionDays <= 0)
			{
				logger.LogDebug("ConnectionRecord retention is disabled; records will be kept indefinitely.");
				return;
			}

			try
			{
				// The driver stores DateTimeOffset as a subdocument, so the TTL index has to
				// target the nested BSON date rather than the field itself. Indexing the
				// existing field this way needs no schema change and expires records that
				// were written before retention was introduced.
				await connectionCollection.Indexes.CreateOneAsync(new CreateIndexModel<ConnectionRecord>(
					Builders<ConnectionRecord>.IndexKeys.Ascending($"{nameof(ConnectionRecord.ConnectionDateTimeOffset)}.DateTime"),
					new CreateIndexOptions
					{
						Name = ConnectionRecordExpiryIndexName,
						ExpireAfter = TimeSpan.FromDays(retentionDays)
					}));
			}
			catch (MongoCommandException ex) when (ex.Code == 85)
			{
				// IndexOptionsConflict: the index exists with a different retention. Changing
				// it means dropping and recreating, which is the operator's call, not ours.
				logger.LogWarning(
					"ConnectionRecord TTL index already exists with a different retention than the configured {RetentionDays} days. Drop the '{IndexName}' index to apply the new value.",
					retentionDays,
					ConnectionRecordExpiryIndexName);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Failed to ensure the ConnectionRecord TTL index; records may accumulate without bound.");
			}
		}
	}
}
