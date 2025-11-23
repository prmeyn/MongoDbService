# MongoDbService

[![Release to Nuget](https://github.com/prmeyn/MongoDbService/actions/workflows/release.yml/badge.svg)](https://github.com/prmeyn/MongoDbService/actions/workflows/release.yml)
[![NuGet](https://img.shields.io/nuget/v/MongoDbService.svg)](https://www.nuget.org/packages/MongoDbService)

**MongoDbService** is an open-source C# class library that provides a wrapper around the official MongoDB.Driver, simplifying MongoDB integration in .NET applications.

## Features

- **Connection Tracking**: Creates a `ConnectionRecord` collection that keeps track of compute instances connecting to your MongoDB instance
- **Standardized Configuration**: Ensures uniform MongoDB configuration across all your projects
- **Simplified Integration**: Abstracts connection management so you can focus on business logic

## Requirements

- .NET 8.0 or higher
- MongoDB instance (local or cloud-based)

## Installation

Install the [NuGet package](https://www.nuget.org/packages/MongoDbService):

```bash
dotnet add package MongoDbService
```

## Configuration

Add the following to your `appsettings.json` and update the values to match your MongoDB instance:

```json
"MongoDbSettings": {
  "DatabaseName": "YourDatabaseName",
  "ConnectionString": "mongodb+srv://.........@gpcluster.0bulb.mongodb.net/myDatabase?retryWrites=true&w=majority"
}
```

**Configuration Options:**
- `DatabaseName` (required): The name of your MongoDB database
- `ConnectionString` (required): Your MongoDB connection string

## Usage

Inject `MongoService` into your classes via dependency injection:

### Example: Vehicle Management

**1. Define your DTO:**

```csharp
using MongoDB.Bson.Serialization.Attributes;

namespace YourNameSpace
{
    public sealed class Vehicle
    {
        [BsonId]
        public required string Id { get; init; }
        public required string Name { get; set; }
    }
}
```

**2. Create a handler class:**

```csharp
using MongoDB.Driver;
using MongoDbService;

namespace YourNameSpace
{
    public sealed class VehicleHandler
    {
        private readonly IMongoCollection<Vehicle> _vehicleCollection;

        public VehicleHandler(MongoService mongoService)
        {
            _vehicleCollection = mongoService.Database.GetCollection<Vehicle>(
                nameof(Vehicle), 
                new MongoCollectionSettings() 
                { 
                    ReadConcern = ReadConcern.Majority, 
                    WriteConcern = WriteConcern.WMajority 
                });
        }

        public async Task AddVehicle(string vehicleName)
        {
            await _vehicleCollection.InsertOneAsync(
                new Vehicle() 
                { 
                    Id = Guid.NewGuid().ToString(), 
                    Name = vehicleName 
                });
        }

        public async Task<DeleteResult> RemoveVehicle(string vehicleId)
        {
            return await _vehicleCollection.DeleteOneAsync(
                Builders<Vehicle>.Filter.Eq(v => v.Id, vehicleId));
        }
    }
}
```

## Testing

The project includes integration tests that require a running MongoDB instance.

### Running Tests Locally

1. Ensure MongoDB is running on `localhost:27017` (or set the `MONGODB_CONNECTION_STRING` environment variable)
2. Run the tests:

```bash
dotnet test
```

### CI/CD

The GitHub Actions workflow automatically runs tests against a MongoDB container on every release.

## Contributing

We welcome contributions! If you find a bug or have an idea for improvement, please submit an issue or pull request on [GitHub](https://github.com/prmeyn/MongoDbService).

## Links

- [NuGet Package](https://www.nuget.org/packages/MongoDbService)
- [GitHub Repository](https://github.com/prmeyn/MongoDbService)

## License

This project is licensed under the GNU General Public License v3.0.

---

Happy coding! 🚀🌐📚

