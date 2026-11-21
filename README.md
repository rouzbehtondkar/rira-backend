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
- gRPC CRUD service
- Basic request validation and gRPC status mapping for invalid input, missing employees, and duplicate national codes

## Local configuration

The server expects a SQL Server connection string named `DefaultConnection`.

Configure it locally with .NET User Secrets; do not commit connection strings or credentials:

```bash
dotnet user-secrets init --project src/ViraTask.Server
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<YOUR_SQL_SERVER_CONNECTION_STRING>" --project src/ViraTask.Server
```

Database creation and EF Core migrations will be added after the local SQL Server connection is configured.

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
