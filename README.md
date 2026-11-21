# Vira Task — Employee Management gRPC Service

An employee-management service built with C#, ASP.NET Core gRPC, Protocol Buffers, Entity Framework Core, and Microsoft SQL Server.

## Technology stack

- .NET 10
- ASP.NET Core gRPC
- Protocol Buffers
- Microsoft SQL Server
- Entity Framework Core
- Console gRPC client
- xUnit tests

## Employee contract

The Protobuf contract is in `contracts/employee/v1/employee.proto`.

The service exposes:

- `CreateEmployee`
- `GetEmployee`
- `UpdateEmployee`
- `DeleteEmployee`

Employee fields:

- ID: server-generated GUID
- First name
- Last name
- National code: unique
- Birth date: ISO date string (`yyyy-MM-dd`) in the gRPC contract and `DateOnly` in the application

## Current implementation

- Employee domain entity and SQL Server EF Core mapping
- Unique database index on national code
- Initial EF Core migration for the `Employees` table
- gRPC CRUD service
- Console client for all four CRUD operations
- Basic request validation and gRPC status mapping for invalid input, missing employees, and duplicate national codes; duplicate checks run before writes, while the SQL Server unique index remains the final safeguard against concurrent inserts
- Global gRPC exception interceptor that preserves known gRPC errors, maps caller cancellation to `Cancelled`, logs unexpected exceptions, and returns a generic `Internal` status without exposing server details
- Structured request logging with gRPC method, outcome, and elapsed execution time for successful and failed calls
- Structured server-side logging through the built-in ASP.NET Core logging abstractions
- Automated service tests for CRUD behavior, validation, duplicate national codes on create/update, missing employees, and global exception interception (unexpected exceptions, preserved gRPC statuses, cancellation, server-side logging, and successful-request duration logging)
- Service tests use EF Core InMemory with an isolated database per test; SQL Server-specific unique-index behavior is additionally verified through the local SQL Server setup

## Local configuration

The server expects a SQL Server connection string named `DefaultConnection`.

Configure it locally with .NET User Secrets; do not commit connection strings or credentials:

```bash
dotnet user-secrets init --project src/ViraTask.Server
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<YOUR_SQL_SERVER_CONNECTION_STRING>" --project src/ViraTask.Server
```

Apply database migrations:

```bash
dotnet ef database update --project src/ViraTask.Server --startup-project src/ViraTask.Server
```

## Run locally

The server listens on `http://localhost:5000` using HTTP/2 without TLS (h2c) for local development.

Start the server in the first terminal:

```bash
dotnet run --project src/ViraTask.Server
```

Start the console client in a second terminal:

```bash
dotnet run --project src/ViraTask.Client
```

To use another gRPC endpoint, set the `VIRATASK_GRPC_ADDRESS` environment variable before starting the client.

## Build and test

```bash
dotnet restore
dotnet build
dotnet test
```

## Repository structure

- `src/ViraTask.Server`: gRPC server, domain model, and persistence
- `src/ViraTask.Client`: console gRPC client
- `contracts/employee/v1`: Protobuf contract
- `tests/ViraTask.Tests`: automated tests
