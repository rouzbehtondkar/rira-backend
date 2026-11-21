using Grpc.Core;
using Grpc.Net.Client;
using ViraTask.Contracts.Employee.V1;

var address = Environment.GetEnvironmentVariable("VIRATASK_GRPC_ADDRESS")
    ?? "http://localhost:5000";

using var channel = GrpcChannel.ForAddress(address);
var client = new EmployeeService.EmployeeServiceClient(channel);

Console.WriteLine("ViraTask Employee gRPC Client");
Console.WriteLine($"Server: {address}");

while (true)
{
    Console.WriteLine();
    Console.WriteLine("1. Create employee");
    Console.WriteLine("2. Get employee");
    Console.WriteLine("3. Update employee");
    Console.WriteLine("4. Delete employee");
    Console.WriteLine("0. Exit");
    Console.Write("Choose an operation: ");

    var choice = Console.ReadLine()?.Trim();

    if (choice == "0")
        break;

    try
    {
        switch (choice)
        {
            case "1":
            {
                var request = new CreateEmployeeRequest
                {
                    FirstName = ReadRequired("First name: "),
                    LastName = ReadRequired("Last name: "),
                    NationalCode = ReadRequired("National code: "),
                    BirthDate = ReadRequired("Birth date (yyyy-MM-dd): ")
                };

                var employee = await client.CreateEmployeeAsync(request);
                Console.WriteLine("Employee created successfully.");
                PrintEmployee(employee);
                break;
            }
            case "2":
            {
                var request = new GetEmployeeRequest
                {
                    Id = ReadRequired("Employee ID: ")
                };

                var employee = await client.GetEmployeeAsync(request);
                PrintEmployee(employee);
                break;
            }
            case "3":
            {
                var request = new UpdateEmployeeRequest
                {
                    Id = ReadRequired("Employee ID: "),
                    FirstName = ReadRequired("First name: "),
                    LastName = ReadRequired("Last name: "),
                    NationalCode = ReadRequired("National code: "),
                    BirthDate = ReadRequired("Birth date (yyyy-MM-dd): ")
                };

                var employee = await client.UpdateEmployeeAsync(request);
                Console.WriteLine("Employee updated successfully.");
                PrintEmployee(employee);
                break;
            }
            case "4":
            {
                var request = new DeleteEmployeeRequest
                {
                    Id = ReadRequired("Employee ID: ")
                };

                var response = await client.DeleteEmployeeAsync(request);
                Console.WriteLine(response.Deleted
                    ? "Employee deleted successfully."
                    : "Employee was not deleted.");
                break;
            }
            default:
                Console.WriteLine("Invalid choice. Select 0, 1, 2, 3, or 4.");
                break;
        }
    }
    catch (RpcException exception)
    {
        Console.WriteLine($"gRPC error: {exception.StatusCode} - {exception.Status.Detail}");
    }
    catch (Exception exception)
    {
        Console.WriteLine($"Unexpected client error: {exception.Message}");
    }
}

Console.WriteLine("Client closed.");

static string ReadRequired(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        var value = Console.ReadLine()?.Trim();

        if (!string.IsNullOrWhiteSpace(value))
            return value;

        Console.WriteLine("This field is required.");
    }
}

static void PrintEmployee(Employee employee)
{
    Console.WriteLine();
    Console.WriteLine($"ID:           {employee.Id}");
    Console.WriteLine($"First name:   {employee.FirstName}");
    Console.WriteLine($"Last name:    {employee.LastName}");
    Console.WriteLine($"National code:{employee.NationalCode}");
    Console.WriteLine($"Birth date:   {employee.BirthDate}");
}
