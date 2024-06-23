using MongoDB.Bson.Serialization.Attributes;

namespace MongoDbService.DTOs
{
	public class ConnectionRecord
	{
		[BsonId]
		public required string Id { get; init; }
		public required string EnvironmentMachineName { get; init; }
		public required DateTimeOffset ConnectionDateTimeOffset { get; init; }
	}
}
