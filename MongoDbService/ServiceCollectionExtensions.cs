using Microsoft.Extensions.DependencyInjection;

namespace MongoDbService
{
	public static class ServiceCollectionExtensions
	{
		public static IServiceCollection AddMongoDbServices(this IServiceCollection services)
		{
			services.AddSingleton<MongoService>();
			return services;
		}
	}
}
