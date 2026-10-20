using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PhoneNumbersManagement.Domain;
namespace PhoneNumbersManagement;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
    {
        var status = error switch
        {
            RuleViolation or JsonException => StatusCodes.Status400BadRequest,
            EntityNotFound => StatusCodes.Status404NotFound,
            Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        if (status == 500) logger.LogError("Unhandled API failure of type {ErrorType}; trace {TraceId}", error.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status, Title = status == 500 ? "An unexpected error occurred." : error.Message,
                Extensions = { ["traceId"] = context.TraceIdentifier }
            }
        });
    }
}