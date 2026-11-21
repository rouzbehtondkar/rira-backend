using System.Diagnostics.CodeAnalysis;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ViraTask.Contracts.Employee.V1;
using ViraTask.Server.Grpc;
using Xunit;

namespace ViraTask.Tests;

public sealed class GlobalExceptionInterceptorTests
{
    [Fact]
    public async Task UnaryServerHandler_ShouldReturnInternalAndLog_WhenUnexpectedExceptionOccurs()
    {
        var logger = new CapturingLogger<GlobalExceptionInterceptor>();
        var interceptor = new GlobalExceptionInterceptor(logger);
        var context = new TestServerCallContext();

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            interceptor.UnaryServerHandler(
                new GetEmployeeRequest { Id = Guid.NewGuid().ToString() },
                context,
                (_, _) => Task.FromException<Employee>(
                    new InvalidOperationException("Sensitive database details."))));

        Assert.Equal(StatusCode.Internal, exception.StatusCode);
        Assert.Equal("An unexpected server error occurred.", exception.Status.Detail);
        Assert.DoesNotContain("Sensitive", exception.Status.Detail);

        var logEntry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logEntry.Level);
        Assert.IsType<InvalidOperationException>(logEntry.Exception);
        Assert.Contains("test-method", logEntry.Message);
    }

    [Fact]
    public async Task UnaryServerHandler_ShouldPreserveStatus_WhenRpcExceptionOccurs()
    {
        var logger = new CapturingLogger<GlobalExceptionInterceptor>();
        var interceptor = new GlobalExceptionInterceptor(logger);
        var context = new TestServerCallContext();
        var expectedException = new RpcException(
            new Status(StatusCode.NotFound, "Employee was not found."));

        var actualException = await Assert.ThrowsAsync<RpcException>(() =>
            interceptor.UnaryServerHandler(
                new GetEmployeeRequest { Id = Guid.NewGuid().ToString() },
                context,
                (_, _) => Task.FromException<Employee>(expectedException)));

        Assert.Same(expectedException, actualException);
        Assert.Equal(StatusCode.NotFound, actualException.StatusCode);
        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
    }

    [Fact]
    public async Task UnaryServerHandler_ShouldReturnCancelled_WhenCallerCancelsRequest()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var logger = new CapturingLogger<GlobalExceptionInterceptor>();
        var interceptor = new GlobalExceptionInterceptor(logger);
        var context = new TestServerCallContext(cancellationSource.Token);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            interceptor.UnaryServerHandler(
                new GetEmployeeRequest { Id = Guid.NewGuid().ToString() },
                context,
                (_, _) => Task.FromException<Employee>(
                    new OperationCanceledException("Operation cancelled."))));

        Assert.Equal(StatusCode.Cancelled, exception.StatusCode);
        Assert.Equal("The request was cancelled.", exception.Status.Detail);
        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, logger.Entries[0].Level);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);

    private sealed class TestServerCallContext(CancellationToken cancellationToken = default)
        : ServerCallContext
    {
        protected override string MethodCore => "test-method";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "ipv4:127.0.0.1:5000";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => new();
        protected override CancellationToken CancellationTokenCore => cancellationToken;
        protected override Metadata ResponseTrailersCore => new();
        [AllowNull]
        protected override Status StatusCore { get; set; }
        [AllowNull]
        protected override WriteOptions WriteOptionsCore { get; set; } = new();
        protected override AuthContext AuthContextCore => null!;
        protected override ContextPropagationToken CreatePropagationTokenCore(
            [AllowNull] ContextPropagationOptions options) => null!;
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
