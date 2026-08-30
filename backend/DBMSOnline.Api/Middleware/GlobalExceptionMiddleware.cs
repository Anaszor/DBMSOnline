using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DBMSOnline.Domain.Exceptions;

namespace DBMSOnline.Api.Middleware;

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

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails();

        switch (exception)
        {
            case SecurityViolationException secEx:
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                problem.Status = (int)HttpStatusCode.Forbidden;
                problem.Title = "Security Policy Violation";
                problem.Detail = secEx.Message;
                problem.Type = "https://dbmsonline.internal/errors/security-violation";
                break;

            case SsrfBlockedException ssrfEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                problem.Status = (int)HttpStatusCode.BadRequest;
                problem.Title = "SSRF Protection Guard";
                problem.Detail = ssrfEx.Message;
                problem.Type = "https://dbmsonline.internal/errors/ssrf-blocked";
                break;

            case DomainException domEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                problem.Status = (int)HttpStatusCode.BadRequest;
                problem.Title = "Domain Processing Error";
                problem.Detail = domEx.Message;
                problem.Type = "https://dbmsonline.internal/errors/domain-error";
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                problem.Status = (int)HttpStatusCode.InternalServerError;
                problem.Title = "Server Execution Failure";
                problem.Detail = exception.InnerException?.Message ?? exception.Message;
                problem.Type = "https://dbmsonline.internal/errors/internal";
                break;
        }

        string json = JsonSerializer.Serialize(problem);
        return context.Response.WriteAsync(json);
    }
}
