using System.Globalization;
using Grpc.Core;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ViraTask.Contracts.Employee.V1;
using ViraTask.Server.Domain;
using ViraTask.Server.Infrastructure;

namespace ViraTask.Server.Grpc;

public sealed class EmployeeGrpcService(AppDbContext dbContext)
    : EmployeeService.EmployeeServiceBase
{
    public override async Task<Employee> CreateEmployee(
        CreateEmployeeRequest request,
        ServerCallContext context)
    {
        ValidateEmployeeFields(request.FirstName, request.LastName, request.NationalCode, request.BirthDate);
        var employee = new Domain.Employee
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            NationalCode = request.NationalCode.Trim(),
            BirthDate = ParseBirthDate(request.BirthDate)
        };

        dbContext.Employees.Add(employee);
        await SaveChangesAsync();
        return ToContract(employee);
    }

    public override async Task<Employee> GetEmployee(
        GetEmployeeRequest request,
        ServerCallContext context)
    {
        var id = ParseId(request.Id);
        var employee = await dbContext.Employees
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, context.CancellationToken);

        return employee is null
            ? throw new RpcException(new Status(StatusCode.NotFound, "Employee was not found."))
            : ToContract(employee);
    }

    public override async Task<Employee> UpdateEmployee(
        UpdateEmployeeRequest request,
        ServerCallContext context)
    {
        var id = ParseId(request.Id);
        ValidateEmployeeFields(request.FirstName, request.LastName, request.NationalCode, request.BirthDate);

        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(x => x.Id == id, context.CancellationToken);

        if (employee is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Employee was not found."));

        employee.FirstName = request.FirstName.Trim();
        employee.LastName = request.LastName.Trim();
        employee.NationalCode = request.NationalCode.Trim();
        employee.BirthDate = ParseBirthDate(request.BirthDate);

        await SaveChangesAsync();
        return ToContract(employee);
    }

    public override async Task<DeleteEmployeeResponse> DeleteEmployee(
        DeleteEmployeeRequest request,
        ServerCallContext context)
    {
        var id = ParseId(request.Id);
        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(x => x.Id == id, context.CancellationToken);

        if (employee is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Employee was not found."));

        dbContext.Employees.Remove(employee);
        await dbContext.SaveChangesAsync(context.CancellationToken);

        return new DeleteEmployeeResponse { Deleted = true };
    }

    private async Task SaveChangesAsync()
    {
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new RpcException(
                new Status(StatusCode.AlreadyExists, "An employee with this national code already exists."));
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException &&
                (sqlException.Number == 2601 || sqlException.Number == 2627))
                return true;
        }

        return false;
    }

    private static void ValidateEmployeeFields(
        string firstName,
        string lastName,
        string nationalCode,
        string birthDate)
    {
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length > 100)
            throw InvalidArgument("First name is required and must not exceed 100 characters.");

        if (string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length > 100)
            throw InvalidArgument("Last name is required and must not exceed 100 characters.");

        if (string.IsNullOrWhiteSpace(nationalCode) || nationalCode.Trim().Length > 20)
            throw InvalidArgument("National code is required and must not exceed 20 characters.");

        _ = ParseBirthDate(birthDate);
    }

    private static DateOnly ParseBirthDate(string value)
    {
        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var birthDate))
            throw InvalidArgument("Birth date must use yyyy-MM-dd format.");

        return birthDate;
    }

    private static Guid ParseId(string value)
    {
        if (!Guid.TryParse(value, out var id))
            throw InvalidArgument("Employee ID must be a valid GUID.");

        return id;
    }

    private static RpcException InvalidArgument(string message) =>
        new(new Status(StatusCode.InvalidArgument, message));

    private static Employee ToContract(Domain.Employee employee) =>
        new()
        {
            Id = employee.Id.ToString(),
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            NationalCode = employee.NationalCode,
            BirthDate = employee.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
}
