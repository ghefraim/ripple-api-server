using Application.Common.Exceptions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Filters;

public class ApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    private readonly IDictionary<Type, Action<ExceptionContext>> _exceptionHandlers;

    public ApiExceptionFilterAttribute()
    {
        // Register known exception types and handlers.
        _exceptionHandlers = new Dictionary<Type, Action<ExceptionContext>>
        {
            { typeof(ValidationException), HandleValidationException },
            { typeof(ConflictException), HandleConflictException },
            { typeof(NotFoundException), HandleNotFoundException },
            { typeof(UnauthorizedAccessException), HandleUnauthorizedAccessException },
            { typeof(ForbiddenAccessException), HandleForbiddenAccessException },
            { typeof(TransactionNotExecutedException), HandleTransactionNotExecutedException },
            { typeof(UserLoginFailedException), HandleUserLoginFailedException },
            { typeof(UserRegisterFailedException), HandleUserRegisterFailedException },
            { typeof(InvalidOperationException), HandleInvalidOperationException },
            { typeof(InvalidCredentialsException), HandleInvalidCredentialsException },
            { typeof(EmailConfirmationException), HandleEmailConfirmationException },
            { typeof(PaymentRequiredException), HandlePaymentRequiredException },
        };
    }

    public override void OnException(ExceptionContext context)
    {
        HandleException(context);

        base.OnException(context);
    }

    private void HandleException(ExceptionContext context)
    {
        Type type = context.Exception.GetType();
        if (_exceptionHandlers.ContainsKey(type))
        {
            _exceptionHandlers[type].Invoke(context);
            return;
        }

        if (!context.ModelState.IsValid)
        {
            HandleInvalidModelStateException(context);
            return;
        }

        HandleUnknownException(context);
    }

    private void HandleValidationException(ExceptionContext context)
    {
        ValidationException? exception = context.Exception as ValidationException;

        ValidationProblemDetails details = new(exception!.Errors)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        };

        context.Result = new BadRequestObjectResult(details);

        context.ExceptionHandled = true;
    }

    private void HandleInvalidModelStateException(ExceptionContext context)
    {
        ValidationProblemDetails details = new(context.ModelState)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        };

        context.Result = new BadRequestObjectResult(details);

        context.ExceptionHandled = true;
    }

    private void HandleConflictException(ExceptionContext context)
    {
        ConflictException? exception = context.Exception as ConflictException;

        ProblemDetails details = new()
        {
            Status = StatusCodes.Status409Conflict,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            Title = "Conflict",
            Detail = exception!.Message,
        };

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status409Conflict };

        context.ExceptionHandled = true;
    }

    private void HandleNotFoundException(ExceptionContext context)
    {
        NotFoundException? exception = context.Exception as NotFoundException;

        ProblemDetails details = new()
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Title = "The specified resource was not found.",
            Detail = exception!.Message,
        };

        context.Result = new NotFoundObjectResult(details);

        context.ExceptionHandled = true;
    }

    private void HandleUnauthorizedAccessException(ExceptionContext context)
    {
        ProblemDetails details = new()
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
        };

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status401Unauthorized };

        context.ExceptionHandled = true;
    }

    private void HandleForbiddenAccessException(ExceptionContext context)
    {
        ProblemDetails details = new()
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
        };

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status403Forbidden };

        context.ExceptionHandled = true;
    }

    private void HandleUnknownException(ExceptionContext context)
    {
        var correlationId = context.HttpContext.Items["CorrelationId"]?.ToString();

        ProblemDetails details = new()
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request.",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        };

        if (!string.IsNullOrEmpty(correlationId))
        {
            details.Extensions["correlationId"] = correlationId;
        }

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status500InternalServerError };

        context.ExceptionHandled = true;
    }

    private void HandleTransactionNotExecutedException(ExceptionContext context)
    {
        ProblemDetails details = new()
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "DB transaction not executed",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        };

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status500InternalServerError };

        context.ExceptionHandled = true;
    }

    private void HandleUserLoginFailedException(ExceptionContext context)
    {
        ProblemDetails details = new()
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Login user failed",
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
        };

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status401Unauthorized };

        context.ExceptionHandled = true;
    }

    private void HandleUserRegisterFailedException(ExceptionContext context)
    {
        ProblemDetails details = new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Register user failed",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        };

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status400BadRequest };

        context.ExceptionHandled = true;
    }

    private void HandleInvalidOperationException(ExceptionContext context)
    {
        InvalidOperationException? exception = context.Exception as InvalidOperationException;

        ProblemDetails details = new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = exception?.Message ?? "Invalid operation",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        };

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status400BadRequest };

        context.ExceptionHandled = true;
    }

    private void HandleInvalidCredentialsException(ExceptionContext context)
    {
        InvalidCredentialsException? exception = context.Exception as InvalidCredentialsException;

        ProblemDetails details = new()
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Invalid Credentials",
            Detail = exception!.Message,
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
        };

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status401Unauthorized };

        context.ExceptionHandled = true;
    }

    private void HandleEmailConfirmationException(ExceptionContext context)
    {
        EmailConfirmationException? exception = context.Exception as EmailConfirmationException;

        ProblemDetails details = new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Email Confirmation Failed",
            Detail = exception!.Message,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        };

        context.Result = new BadRequestObjectResult(details);

        context.ExceptionHandled = true;
    }

    private void HandlePaymentRequiredException(ExceptionContext context)
    {
        PaymentRequiredException? exception = context.Exception as PaymentRequiredException;

        ProblemDetails details = new()
        {
            Status = StatusCodes.Status402PaymentRequired,
            Title = "Upgrade Required",
            Detail = exception!.Message,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.2",
        };

        if (!string.IsNullOrEmpty(exception.FeatureKey))
        {
            details.Extensions["featureKey"] = exception.FeatureKey;
        }

        if (!string.IsNullOrEmpty(exception.FeatureDisplayName))
        {
            details.Extensions["featureDisplayName"] = exception.FeatureDisplayName;
        }

        if (exception.Limit.HasValue)
        {
            details.Extensions["limit"] = exception.Limit.Value;
        }

        if (exception.CurrentUsage.HasValue)
        {
            details.Extensions["currentUsage"] = exception.CurrentUsage.Value;
        }

        details.Extensions["upgradeUrl"] = "/settings/billing";

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status402PaymentRequired };

        context.ExceptionHandled = true;
    }
}