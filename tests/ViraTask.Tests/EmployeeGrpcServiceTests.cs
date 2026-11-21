using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ViraTask.Contracts.Employee.V1;
using ViraTask.Server.Grpc;
using ViraTask.Server.Infrastructure;
using Xunit;

namespace ViraTask.Tests;

public sealed class EmployeeGrpcServiceTests
{
    private static readonly ServerCallContext CallContext = new TestServerCallContext();

    [Fact]
    public async Task CreateEmployee_ShouldPersistAndReturnEmployee()
    {
        await using var dbContext = CreateDbContext();
        var service = new EmployeeGrpcService(dbContext);

        var result = await service.CreateEmployee(
            new CreateEmployeeRequest
            {
                FirstName = "Ali",
                LastName = "Ahmadi",
                NationalCode = "TEST-001",
                BirthDate = "1995-05-20"
            },
            CallContext);

        Assert.NotEqual(Guid.Empty, Guid.Parse(result.Id));
        Assert.Equal("Ali", result.FirstName);
        Assert.Equal("Ahmadi", result.LastName);
        Assert.Equal("TEST-001", result.NationalCode);
        Assert.Equal("1995-05-20", result.BirthDate);

        var savedEmployee = await dbContext.Employees.SingleAsync();
        Assert.Equal(Guid.Parse(result.Id), savedEmployee.Id);
    }

    [Fact]
    public async Task GetEmployee_ShouldReturnNotFound_WhenEmployeeDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var service = new EmployeeGrpcService(dbContext);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetEmployee(
                new GetEmployeeRequest { Id = Guid.NewGuid().ToString() },
                CallContext));

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task UpdateEmployee_ShouldChangeExistingEmployee()
    {
        await using var dbContext = CreateDbContext();
        var employeeId = await InsertEmployee(dbContext);
        var service = new EmployeeGrpcService(dbContext);

        var result = await service.UpdateEmployee(
            new UpdateEmployeeRequest
            {
                Id = employeeId.ToString(),
                FirstName = "Reza",
                LastName = "Karimi",
                NationalCode = "TEST-001",
                BirthDate = "1996-06-21"
            },
            CallContext);

        Assert.Equal("Reza", result.FirstName);
        Assert.Equal("Karimi", result.LastName);
        Assert.Equal("1996-06-21", result.BirthDate);

        var savedEmployee = await dbContext.Employees.SingleAsync();
        Assert.Equal("Reza", savedEmployee.FirstName);
        Assert.Equal("Karimi", savedEmployee.LastName);
    }

    [Fact]
    public async Task UpdateEmployee_ShouldReturnNotFound_WhenEmployeeDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var service = new EmployeeGrpcService(dbContext);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.UpdateEmployee(
                new UpdateEmployeeRequest
                {
                    Id = Guid.NewGuid().ToString(),
                    FirstName = "Reza",
                    LastName = "Karimi",
                    NationalCode = "TEST-001",
                    BirthDate = "1995-05-20"
                },
                CallContext));

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task DeleteEmployee_ShouldRemoveExistingEmployee()
    {
        await using var dbContext = CreateDbContext();
        var employeeId = await InsertEmployee(dbContext);
        var service = new EmployeeGrpcService(dbContext);

        var result = await service.DeleteEmployee(
            new DeleteEmployeeRequest { Id = employeeId.ToString() },
            CallContext);

        Assert.True(result.Deleted);
        Assert.Empty(await dbContext.Employees.ToListAsync());
    }

    [Fact]
    public async Task GetEmployee_ShouldReturnInvalidArgument_WhenIdIsNotGuid()
    {
        await using var dbContext = CreateDbContext();
        var service = new EmployeeGrpcService(dbContext);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetEmployee(new GetEmployeeRequest { Id = "not-a-guid" }, CallContext));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public async Task CreateEmployee_ShouldReturnInvalidArgument_WhenBirthDateHasWrongFormat()
    {
        await using var dbContext = CreateDbContext();
        var service = new EmployeeGrpcService(dbContext);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.CreateEmployee(
                new CreateEmployeeRequest
                {
                    FirstName = "Ali",
                    LastName = "Ahmadi",
                    NationalCode = "TEST-001",
                    BirthDate = "20-05-1995"
                },
                CallContext));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Guid> InsertEmployee(AppDbContext dbContext)
    {
        var employee = new ViraTask.Server.Domain.Employee
        {
            Id = Guid.NewGuid(),
            FirstName = "Ali",
            LastName = "Ahmadi",
            NationalCode = "TEST-001",
            BirthDate = new DateOnly(1995, 5, 20)
        };

        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();
        return employee.Id;
    }

    private sealed class TestServerCallContext : ServerCallContext
    {
        protected override string MethodCore => "test-method";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "ipv4:127.0.0.1:5000";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => new();
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => new();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions WriteOptionsCore { get; set; } = new();
        protected override AuthContext AuthContextCore => null!;
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions options) => null!;
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
