using System.Diagnostics.CodeAnalysis;
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
    public async Task CreateEmployee_ShouldReturnAlreadyExists_WhenNationalCodeIsDuplicated()
    {
        await using var dbContext = CreateDbContext();
        await InsertEmployee(dbContext);
        var service = new EmployeeGrpcService(dbContext);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.CreateEmployee(
                new CreateEmployeeRequest
                {
                    FirstName = "Reza",
                    LastName = "Karimi",
                    NationalCode = "TEST-001",
                    BirthDate = "1996-06-21"
                },
                CallContext));

        Assert.Equal(StatusCode.AlreadyExists, exception.StatusCode);
        Assert.Equal(
            "An employee with this national code already exists.",
            exception.Status.Detail);
        Assert.Single(await dbContext.Employees.ToListAsync());
    }

    [Fact]
    public async Task UpdateEmployee_ShouldReturnAlreadyExists_WhenNationalCodeBelongsToAnotherEmployee()
    {
        await using var dbContext = CreateDbContext();
        await InsertEmployee(dbContext);
        var secondEmployee = new ViraTask.Server.Domain.Employee
        {
            Id = Guid.NewGuid(),
            FirstName = "Reza",
            LastName = "Karimi",
            NationalCode = "TEST-002",
            BirthDate = new DateOnly(1996, 6, 21)
        };
        dbContext.Employees.Add(secondEmployee);
        await dbContext.SaveChangesAsync();

        var service = new EmployeeGrpcService(dbContext);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.UpdateEmployee(
                new UpdateEmployeeRequest
                {
                    Id = secondEmployee.Id.ToString(),
                    FirstName = "Reza",
                    LastName = "Karimi",
                    NationalCode = "TEST-001",
                    BirthDate = "1996-06-21"
                },
                CallContext));

        Assert.Equal(StatusCode.AlreadyExists, exception.StatusCode);

        var unchangedEmployee = await dbContext.Employees.SingleAsync(x => x.Id == secondEmployee.Id);
        Assert.Equal("TEST-002", unchangedEmployee.NationalCode);
        Assert.Equal(2, await dbContext.Employees.CountAsync());
    }

    [Fact]
    public async Task ListEmployees_ShouldReturnEmployeesOrderedByLastName()
    {
        await using var dbContext = CreateDbContext();
        await InsertEmployee(dbContext);
        dbContext.Employees.Add(new ViraTask.Server.Domain.Employee
        {
            Id = Guid.NewGuid(),
            FirstName = "Sara",
            LastName = "Zamani",
            NationalCode = "TEST-010",
            BirthDate = new DateOnly(1997, 7, 10)
        });
        await dbContext.SaveChangesAsync();
        var service = new EmployeeGrpcService(dbContext);

        var result = await service.ListEmployees(new ListEmployeesRequest(), CallContext);

        Assert.Equal(2, result.Employees.Count);
        Assert.Equal("Ahmadi", result.Employees[0].LastName);
        Assert.Equal("Zamani", result.Employees[1].LastName);
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

    [Fact]
    public async Task CreateEmployee_ShouldReturnInvalidArgument_WhenFirstNameIsEmpty()
    {
        await using var dbContext = CreateDbContext();
        var service = new EmployeeGrpcService(dbContext);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.CreateEmployee(
                new CreateEmployeeRequest
                {
                    FirstName = " ",
                    LastName = "Ahmadi",
                    NationalCode = "TEST-003",
                    BirthDate = "1995-05-20"
                },
                CallContext));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Empty(await dbContext.Employees.ToListAsync());
    }

    [Fact]
    public async Task CreateEmployee_ShouldReturnInvalidArgument_WhenFirstNameExceeds100Characters()
    {
        await using var dbContext = CreateDbContext();
        var service = new EmployeeGrpcService(dbContext);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.CreateEmployee(
                new CreateEmployeeRequest
                {
                    FirstName = new string('A', 101),
                    LastName = "Ahmadi",
                    NationalCode = "TEST-004",
                    BirthDate = "1995-05-20"
                },
                CallContext));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Empty(await dbContext.Employees.ToListAsync());
    }

    [Fact]
    public async Task CreateEmployee_ShouldReturnInvalidArgument_WhenLastNameIsEmpty()
    {
        await using var dbContext = CreateDbContext();
        var service = new EmployeeGrpcService(dbContext);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.CreateEmployee(
                new CreateEmployeeRequest
                {
                    FirstName = "Ali",
                    LastName = "",
                    NationalCode = "TEST-005",
                    BirthDate = "1995-05-20"
                },
                CallContext));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Empty(await dbContext.Employees.ToListAsync());
    }

    [Fact]
    public async Task CreateEmployee_ShouldReturnInvalidArgument_WhenLastNameExceeds100Characters()
    {
        await using var dbContext = CreateDbContext();
        var service = new EmployeeGrpcService(dbContext);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.CreateEmployee(
                new CreateEmployeeRequest
                {
                    FirstName = "Ali",
                    LastName = new string('A', 101),
                    NationalCode = "TEST-006",
                    BirthDate = "1995-05-20"
                },
                CallContext));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Empty(await dbContext.Employees.ToListAsync());
    }

    [Fact]
    public async Task DeleteEmployee_ShouldReturnNotFound_WhenEmployeeDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var service = new EmployeeGrpcService(dbContext);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.DeleteEmployee(
                new DeleteEmployeeRequest { Id = Guid.NewGuid().ToString() },
                CallContext));

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
        Assert.Empty(await dbContext.Employees.ToListAsync());
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
        [AllowNull]
        protected override Status StatusCore { get; set; }
        [AllowNull]
        protected override WriteOptions WriteOptionsCore { get; set; } = new();
        protected override AuthContext AuthContextCore => null!;
        protected override ContextPropagationToken CreatePropagationTokenCore([AllowNull] ContextPropagationOptions options) => null!;
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
