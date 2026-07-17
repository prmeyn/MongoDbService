using Microsoft.Extensions.DependencyInjection;

namespace MongoDbService
{
	public static class ServiceCollectionExtensions
	{
		public static void AddMongoDbServices(this IServiceCollection services)
		{
			services.AddSingleton<MongoService>();
		}
	}
}
