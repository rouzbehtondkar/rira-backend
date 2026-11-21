# Vira Task — Employee Management gRPC Service

An employee-management service built with C#, ASP.NET Core gRPC, Protocol Buffers, Entity Framework Core, and Microsoft SQL Server.

The project demonstrates a contract-first RPC API, persistence with EF Core, validation and error handling, structured logging, and automated tests.

## Technology stack

- .NET 10
- ASP.NET Core gRPC
- Protocol Buffers
- Microsoft SQL Server
- Entity Framework Core 10
- Console gRPC client
- xUnit

## Architecture

```text
Console Client
     |
     | gRPC over HTTP/2 (Protobuf messages)
     v
ASP.NET Core gRPC Server
     |
     v
EmployeeGrpcService ---- GlobalExceptionInterceptor
     |
     v
Entity Framework Core
     |
     v
Microsoft SQL Server
```

The Protobuf contract is the shared API definition. The server and console client generate their respective C# gRPC types from the same contract:

- `contracts/employee/v1/employee.proto`

The current implementation keeps the service intentionally small. Request validation and CRUD orchestration live in `EmployeeGrpcService`; EF Core configuration and persistence mapping live in `AppDbContext`.

## Employee contract

The service exposes four unary RPC methods:

| RPC | Purpose |
|---|---|
| `CreateEmployee` | Create an employee |
| `GetEmployee` | Retrieve an employee by ID |
| `UpdateEmployee` | Replace the editable employee fields |
| `DeleteEmployee` | Delete an employee |

Employee fields and design decisions:

- **ID:** server-generated GUID, represented as a string in Protobuf.
- **First name / last name:** required strings with a maximum length of 100 characters.
- **National code:** required string with a maximum length of 20 characters and a unique database index. It is a string to preserve leading zeros and because it is an identifier, not a quantity.
- **Birth date:** `yyyy-MM-dd` in the gRPC contract and `DateOnly` in the domain model because a birth date has no time-of-day or time-zone component.

Update is a full update: the client sends all editable fields. Delete is a physical delete.

## Validation and error handling

The service maps expected failures to standard gRPC status codes:

| Condition | gRPC status |
|---|---|
| Invalid ID, missing required field, invalid date, or field too long | `InvalidArgument` |
| National code already belongs to another employee | `AlreadyExists` |
| Employee does not exist | `NotFound` |
| Caller cancellation | `Cancelled` |
| Unexpected server-side exception | `Internal` |

A pre-save duplicate check provides a clear response for ordinary requests. The SQL Server unique index is still the final protection against concurrent writes that race past the application-level check.

The global interceptor preserves known `RpcException` status codes, handles caller cancellation, logs unexpected exceptions on the server, and returns a generic `Internal` response to the client. Internal exception details are not returned to clients or included in expected-error log messages.

## Logging and observability

The server uses the built-in ASP.NET Core `ILogger` abstractions. The gRPC interceptor records:

- RPC method name
- Outcome or gRPC status code
- Request execution duration

Unexpected exceptions are logged server-side with exception details. Do not put connection strings, credentials, or complete national codes in logs.

## Prerequisites

- .NET 10 SDK
- Microsoft SQL Server instance accessible from the machine running the server
- .NET EF Core CLI tool (`dotnet-ef`)

Install the EF Core CLI tool if it is not already installed:

```bash
dotnet tool install --global dotnet-ef
```

## Local configuration

The server expects a connection string named `DefaultConnection`. Configure it with .NET User Secrets; do not commit connection strings or credentials:

```bash
dotnet user-secrets init --project src/ViraTask.Server
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<YOUR_SQL_SERVER_CONNECTION_STRING>" --project src/ViraTask.Server
```

User Secrets are loaded in the Development environment. Set the environment before running the server.

**PowerShell:**

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

**Git Bash:**

```bash
export ASPNETCORE_ENVIRONMENT=Development
```

Apply database migrations after configuring the connection string:

```bash
dotnet ef database update --project src/ViraTask.Server --startup-project src/ViraTask.Server
```

This creates the `Employees` table and the unique index on `NationalCode`.

## Run locally

For local development, the server listens on `http://localhost:5000` using HTTP/2 without TLS (h2c). Keep this development endpoint bound to localhost; do not expose it directly to the public internet. Production deployments should use TLS and an appropriately secured endpoint.

Start the server in the first terminal. Ensure `ASPNETCORE_ENVIRONMENT=Development` is set in that terminal:

```bash
dotnet run --project src/ViraTask.Server
```

Start the console client in a second terminal:

```bash
dotnet run --project src/ViraTask.Client
```

The client defaults to `http://localhost:5000`. To use another gRPC endpoint, set `VIRATASK_GRPC_ADDRESS` before starting the client.

**PowerShell:**

```powershell
$env:VIRATASK_GRPC_ADDRESS = "http://localhost:5000"
dotnet run --project src/ViraTask.Client
```

**Git Bash:**

```bash
export VIRATASK_GRPC_ADDRESS="http://localhost:5000"
dotnet run --project src/ViraTask.Client
```

## Build and test

Run from the repository root:

```bash
dotnet restore
dotnet build
dotnet test
```

The automated test project uses EF Core InMemory with an isolated database per test. It covers CRUD behavior, validation, duplicate national codes, missing employees, and global interceptor behavior. The InMemory provider does not reproduce SQL Server's unique-index enforcement; that database-specific constraint is defined in the EF Core model and migration and should also be verified against a real SQL Server instance.

The current suite contains **19 automated tests**.

## Repository structure

```text
contracts/
  employee/v1/employee.proto
src/
  ViraTask.Server/
    Domain/
    Grpc/
    Infrastructure/
    Migrations/
  ViraTask.Client/
tests/
  ViraTask.Tests/
```

- `contracts/employee/v1`: shared Protobuf contract
- `src/ViraTask.Server`: gRPC server, domain model, persistence, and migrations
- `src/ViraTask.Client`: interactive console gRPC client
- `tests/ViraTask.Tests`: automated unit/service tests

## Configuration and security notes

- Keep local connection strings in User Secrets or environment variables, never in source control.
- The included h2c endpoint is for local development only; use TLS for deployment.
- The interceptor deliberately returns generic messages for unexpected server errors so implementation details are not disclosed to clients.
