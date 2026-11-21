using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace ViraTask.Server.Grpc;

public sealed class GlobalExceptionInterceptor(ILogger<GlobalExceptionInterceptor> logger)
    : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var response = await continuation(request, context);
            logger.LogInformation(
                "gRPC request {Method} completed successfully in {ElapsedMilliseconds:F2} ms.",
                context.Method,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return response;
        }
        catch (RpcException exception)
        {
            // Preserve the status code while recording the failed request.
            // Do not log status details: they may contain user input or sensitive data.
            logger.LogWarning(
                "gRPC request {Method} failed with status {StatusCode} in {ElapsedMilliseconds:F2} ms.",
                context.Method,
                exception.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            throw;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "gRPC request {Method} was cancelled after {ElapsedMilliseconds:F2} ms.",
                context.Method,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            throw new RpcException(
                new Status(StatusCode.Cancelled, "The request was cancelled."));
        }
        catch (Exception exception)
        {
            // Keep implementation details in server logs, not in the client response.
            logger.LogError(
                exception,
                "An unexpected error occurred while processing gRPC request {Method} after {ElapsedMilliseconds:F2} ms.",
                context.Method,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            throw new RpcException(
                new Status(StatusCode.Internal, "An unexpected server error occurred."));
        }
    }
}
