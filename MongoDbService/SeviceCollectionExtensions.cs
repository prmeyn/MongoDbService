using Microsoft.Extensions.DependencyInjection;

namespace MongoDbService
{
	public static class SeviceCollectionExtensions
	{
		public static void AddMongoDbServices(this IServiceCollection services)
		{
			services.AddSingleton<MongoService>();
		}
	}
}
