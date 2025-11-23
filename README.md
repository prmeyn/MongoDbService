# [MongoDbService](https://www.nuget.org/packages/MongoDbService)

**MongoDbService** is an open-source C# class library that provides a wrapper around the official MongoDB.Driver

## Features

- Creates a collection called `ConnectionRecord` that keeps track of the number of compute instances that established a connection to your MongoDB instance.
- Ensures that all your projects have a uniform way of reading your MongoDB configurations
- Abstracts the code that is responsible for creating the connection to your MongoDB instance, so you can focus on your code having only the Business logic.


## Contributing

We welcome contributions! If you find a bug, have an idea for improvement, please submit an issue or a pull request on GitHub.

## Getting Started

### [NuGet Package](https://www.nuget.org/packages/MongoDbService)

To include **MongoDbService** in your project, [install the NuGet package](https://www.nuget.org/packages/MongoDbService):

```bash
dotnet add package MongoDbService
```
Then in your `appsettings.json` add the following sample configuration and change the values to mtch the details of your MongoDB instance.
```json
"MongoDbSettings": {
  "DatabaseName": "YourDatabaseName",
  "ConnectionString": "mongodb+srv://.........@gpcluster.0bulb.mongodb.net/myDatabase?retryWrites=true&w=majority"
}
```

After the above is done, you can just Dependency inject the `MongoService` in your C# class.

#### For example:
Lets say your had a DTO to represent a record of an `IMongoCollection` called `Vehicle`
```csharp
﻿using MongoDB.Bson.Serialization.Attributes;

namespace YourNameSpace
{
    public sealed class Vehicle
    {
        [BsonId]
        public required string Id { get; init; }
        public required string  Name { get; set; }
    }
}
```

Then you could have another class called `VehicleHandler` to add and remove `Vehicle` instances as follows:
```csharp
using MongoDB.Driver;
using MongoDbService;

namespace YourNameSpace
{
	public sealed class VehicleHandler
	{
		private IMongoCollection<Vehicle> _vehicleCollection;

		public VehicleHandler(
			MongoService mongoService
		)
		{
        _vehicleCollection = mongoService.Database.GetCollection<Vehicle>(nameof(Vehicle), new MongoCollectionSettings() { ReadConcern = ReadConcern.Majority, WriteConcern = WriteConcern.WMajority });
		}

		public async Task AddVehicle(string vehicleName)
		{
			await _vehicleCollection.InsertOneAsync(new Vehicle() { Id = Guid.NewGuid().ToString(), Name = vehicleName });
		}

		public async Task<DeleteResult> RemoveVehicle(string vehicleId)
		{
			return await _vehicleCollection.DeleteOneAsync(Builders<Vehicle>.Filter.Eq(v => v.Id, vehicleId));
		}
	}
}
```
### GitHub Repository
Visit our GitHub repository for the latest updates, documentation, and community contributions.
https://github.com/prmeyn/MongoDbService


## License

This project is licensed under the GNU GENERAL PUBLIC LICENSE.

Happy coding! 🚀🌐📚


