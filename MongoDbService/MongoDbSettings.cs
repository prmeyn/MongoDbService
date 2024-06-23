namespace MongoDbService
{
	public sealed class MongoDbSettings
	{
		public required string MongoDbConnectionString { get; set; }
		public required string MongoDatabaseName { get; set; }
	}
}
