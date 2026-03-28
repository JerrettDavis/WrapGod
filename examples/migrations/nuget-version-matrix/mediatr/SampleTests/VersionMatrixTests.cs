// VersionMatrixTests.cs
// Demonstrates MediatR patterns that differ across v10, v11, and v12.
// This file is illustrative -- it does not compile standalone (no .csproj).
// Focus: generic parameter changes, constraint differences, and handler signatures.

using MediatR;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace WrapGod.Examples.MediatR.VersionMatrix;

// =============================================================================
// Common types used across all version examples
// =============================================================================

public record GetUserQuery(int Id) : IRequest<UserDto>;
public record UserDto(int Id, string Name, string Email);

public record CreateUserCommand(string Name, string Email) : IRequest<int>;

public record UserCreatedNotification(int UserId, string Name) : INotification;

// =============================================================================
// PATTERN 1: Void request handler -- Unit vs Task return type
// v10/v11: IRequestHandler<TRequest> returns Task<Unit>, inherits IRequestHandler<TRequest, Unit>
// v12: IRequestHandler<TRequest> returns Task directly, standalone interface
// =============================================================================

#if MEDIATR_V12
// v12 pattern: Task-returning void handler
public record DeleteUserCommand(int Id) : IRequest;  // IRequest inherits IBaseRequest directly

public class DeleteUserHandler : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        // Delete user logic...
        await Task.CompletedTask;
        // No return statement needed -- clean async void pattern
    }
}
#else
// v10/v11 pattern: Unit-returning void handler
public record DeleteUserCommand(int Id) : IRequest;  // IRequest inherits IRequest<Unit>

public class DeleteUserHandler : IRequestHandler<DeleteUserCommand>
{
    public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        // Delete user logic...
        await Task.CompletedTask;
        return Unit.Value;  // Must return Unit.Value or Unit.Task
    }
}
#endif

// =============================================================================
// PATTERN 2: Pipeline behavior generic constraints
// v10/v11: where TRequest : IRequest<TResponse> (STRICT)
// v12: where TRequest : notnull (RELAXED)
// =============================================================================

#if MEDIATR_V12
// v12 pattern: relaxed constraint enables open-generic pipeline registration
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull  // <-- relaxed constraint
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        var response = await next();
        Console.WriteLine($"Handled {typeof(TRequest).Name}");
        return response;
    }
}

// v12: This open-generic registration WORKS because of 'notnull' constraint
// services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
#else
// v10/v11 pattern: strict constraint prevents some open-generic patterns
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>  // <-- strict constraint
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        var response = await next();
        Console.WriteLine($"Handled {typeof(TRequest).Name}");
        return response;
    }
}

// v10/v11: This open-generic registration may FAIL at DI resolution time
// because TRequest cannot be proven to satisfy IRequest<TResponse> for all T.
// Workaround: register closed-generic versions explicitly.
#endif

// =============================================================================
// PATTERN 3: Validation behavior -- constraint impact on cross-cutting concerns
// v10/v11: Must constrain to IRequest<TResponse>, can't share with streaming
// v12: notnull works across both regular and stream requests
// =============================================================================

#if MEDIATR_V12
// v12: single validation behavior works for ANY request type
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Validate using FluentValidation, DataAnnotations, etc.
        // Works for IRequest<T>, IRequest, and any other request type
        return await next();
    }
}
#else
// v10/v11: separate behaviors needed for regular vs stream requests
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>  // Only works with IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        return await next();
    }
}

// v10/v11: need a SEPARATE behavior for stream requests
public class StreamValidationBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>  // Different constraint!
{
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Same validation logic, duplicated due to constraint incompatibility
        await foreach (var item in next().WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }
}
#endif

// =============================================================================
// PATTERN 4: Stream request handlers -- constraint evolution
// v10/v11: where TRequest : IStreamRequest<TResponse>
// v12: where TRequest : notnull
// =============================================================================

public record GetUsersStreamQuery(string? NameFilter) : IStreamRequest<UserDto>;

#if MEDIATR_V12
public class GetUsersStreamHandler : IStreamRequestHandler<GetUsersStreamQuery, UserDto>
    // v12: implicit 'where GetUsersStreamQuery : notnull' -- satisfied by record type
{
    public async IAsyncEnumerable<UserDto> Handle(
        GetUsersStreamQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 1; i <= 10; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new UserDto(i, $"User {i}", $"user{i}@test.com");
            await Task.Delay(100, cancellationToken);
        }
    }
}
#else
public class GetUsersStreamHandler : IStreamRequestHandler<GetUsersStreamQuery, UserDto>
    // v10/v11: implicit 'where GetUsersStreamQuery : IStreamRequest<UserDto>'
{
    public async IAsyncEnumerable<UserDto> Handle(
        GetUsersStreamQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 1; i <= 10; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new UserDto(i, $"User {i}", $"user{i}@test.com");
            await Task.Delay(100, cancellationToken);
        }
    }
}
#endif

// =============================================================================
// PATTERN 5: Custom notification publisher (v12 only)
// v10/v11: No way to customize how notifications are dispatched to handlers
// v12: INotificationPublisher enables parallel, sequential, fire-and-forget
// =============================================================================

#if MEDIATR_V12
public class ParallelNotificationPublisher : INotificationPublisher
{
    public Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        // Dispatch to all handlers in parallel
        var tasks = new List<Task>();
        foreach (var executor in handlerExecutors)
        {
            tasks.Add(executor.HandlerCallback(notification, cancellationToken));
        }
        return Task.WhenAll(tasks);
    }
}

// Registration: services.AddSingleton<INotificationPublisher, ParallelNotificationPublisher>();
#endif

// =============================================================================
// PATTERN 6: Pre/post processor constraint changes
// v10/v11: where TRequest : IBaseRequest
// v12: where TRequest : notnull
// =============================================================================

#if MEDIATR_V12
public class TimingPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull  // v12: relaxed
{
    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Pre-processing {typeof(TRequest).Name}");
        return Task.CompletedTask;
    }
}
#else
public class TimingPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : IBaseRequest  // v10/v11: must implement IBaseRequest
{
    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Pre-processing {typeof(TRequest).Name}");
        return Task.CompletedTask;
    }
}
#endif
