# MongoDbService

[![Release to Nuget](https://github.com/prmeyn/MongoDbService/actions/workflows/release.yml/badge.svg)](https://github.com/prmeyn/MongoDbService/actions/workflows/release.yml)
[![NuGet](https://img.shields.io/nuget/v/MongoDbService.svg)](https://www.nuget.org/packages/MongoDbService)

**MongoDbService** is an open-source C# class library that provides a wrapper around the official MongoDB.Driver, simplifying MongoDB integration in .NET applications.

## Features

- **Connection Tracking**: Creates a `ConnectionRecord` collection that keeps track of compute instances connecting to your MongoDB instance, expiring old records via a TTL index
- **Standardized Configuration**: Ensures uniform MongoDB configuration across all your projects
- **Simplified Integration**: Abstracts connection management so you can focus on business logic

## Requirements

- .NET 10.0
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
  "ConnectionString": "mongodb+srv://<user>:<password>@<cluster>.mongodb.net/<database>?retryWrites=true&w=majority",
  "ConnectionRecordRetentionDays": 30
}
```

**Configuration Options:**
- `DatabaseName` (optional): The name of your MongoDB database. Falls back to `Untitled-MongoDbService` with a warning if omitted.
- `ConnectionString` (required): Your MongoDB connection string. Throws if omitted.
- `ConnectionRecordRetentionDays` (optional): How long connection-tracking records are kept, enforced by a TTL index. Defaults to `30`. Set to `0` or below to keep them indefinitely.

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

### Short-lived processes

The connection-tracking write is started in the background so it never delays or breaks startup. In a long-running host there is nothing to do. In a short-lived process — a console app, CLI, or function that exits quickly — the process can terminate before the write lands, so await it before returning:

```csharp
await mongoService.ConnectionRecorded;
```

`ConnectionRecorded` never faults; a failed write is logged as a warning, so it needs no `try`/`catch`.

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

