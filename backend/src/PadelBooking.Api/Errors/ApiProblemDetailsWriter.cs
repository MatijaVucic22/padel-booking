using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace PadelBooking.Api.Errors;

public sealed class ApiProblemDetailsWriter(IOptions<ProblemDetailsOptions> options) : IProblemDetailsWriter
{
    public bool CanWrite(ProblemDetailsContext context) =>
        context.HttpContext.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        options.Value.CustomizeProblemDetails?.Invoke(context);
        var status = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
        return new ValueTask(Results.Json(context.ProblemDetails,
            contentType: "application/problem+json", statusCode: status)
            .ExecuteAsync(context.HttpContext));
    }
}
