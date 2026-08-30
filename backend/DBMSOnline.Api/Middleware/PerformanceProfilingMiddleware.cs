using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DBMSOnline.Api.Middleware;

public class PerformanceProfilingMiddleware
{
    private readonly RequestDelegate _next;

    public PerformanceProfilingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        long initialMemory = GC.GetTotalMemory(false);

        context.Response.OnStarting(() =>
        {
            sw.Stop();
            long finalMemory = GC.GetTotalMemory(false);
            long allocatedKb = Math.Max(0, (finalMemory - initialMemory) / 1024);

            context.Response.Headers["X-Execution-Time-Ms"] = sw.ElapsedMilliseconds.ToString();
            context.Response.Headers["X-Memory-Allocated-KB"] = allocatedKb.ToString();
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
