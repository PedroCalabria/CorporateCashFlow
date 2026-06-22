using System.Net;
using System.Text.Json;
using CorporateCashFlow.Business.Exceptions;
using CorporateCashFlow.Entity.DTOs;

namespace CorporateCashFlow.API.Middlewares;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public GlobalExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail, errors) = exception switch
        {
            UnauthorizedException unauthorized => (
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                unauthorized.Message,
                (Dictionary<string, string[]>?)null),
            ValidationException validation => (
                HttpStatusCode.BadRequest,
                "Validation Failed",
                validation.Message,
                validation.Errors),
            ServiceUnavailableException unavailable => (
                HttpStatusCode.ServiceUnavailable,
                "Service Unavailable",
                unavailable.Message,
                null),
            _ => (
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred.",
                null)
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var problem = new ErrorResponseDto
        {
            Type = "https://tools.ietf.org/html/rfc7807",
            Title = title,
            Status = (int)statusCode,
            Detail = detail,
            Instance = context.Request.Path,
            Errors = errors
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
