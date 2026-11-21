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
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException exception)
        {
            // Expected gRPC errors already have an appropriate status code.
            logger.LogWarning(
                "gRPC request {Method} failed with status {StatusCode}: {Detail}",
                context.Method,
                exception.StatusCode,
                exception.Status.Detail);

            throw;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "gRPC request {Method} was cancelled by the caller.",
                context.Method);

            throw new RpcException(
                new Status(StatusCode.Cancelled, "The request was cancelled."));
        }
        catch (Exception exception)
        {
            // Keep implementation details in server logs, not in the client response.
            logger.LogError(
                exception,
                "An unexpected error occurred while processing gRPC request {Method}.",
                context.Method);

            throw new RpcException(
                new Status(StatusCode.Internal, "An unexpected server error occurred."));
        }
    }
}
