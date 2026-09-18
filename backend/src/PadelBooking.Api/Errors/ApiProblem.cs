using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PadelBooking.Api.Errors;

public static class ApiProblem
{
    public static IActionResult ApiError(this ControllerBase controller, int status,
        string code, string detail, object? quote = null) =>
        Result(controller.HttpContext, status, code, detail, quote);

    public static IActionResult Result(HttpContext context, int status,
        string code, string detail, object? quote = null)
    {
        var problem = new ProblemDetails();
        Populate(problem, context, status, code, detail);
        if (quote is not null) problem.Extensions["quote"] = quote;
        return Response(status, problem);
    }

    public static IActionResult Validation(HttpContext context,
        IDictionary<string, string[]> errors)
    {
        var problem = new ValidationProblemDetails(errors);
        Populate(problem, context, StatusCodes.Status400BadRequest,
            ApiErrorCodes.Validation, "Podaci nisu ispravni.");
        problem.Title = "Podaci nisu validni";
        return Response(StatusCodes.Status400BadRequest, problem);
    }

    public static void Populate(ProblemDetails problem, HttpContext context, int status,
        string code, string detail)
    {
        problem.Type = $"https://httpstatuses.com/{status}";
        problem.Status = status;
        problem.Title = status switch
        {
            400 => "Neispravan zahtev",
            401 => "Prijava je potrebna",
            402 => "Plaćanje je potrebno",
            403 => "Pristup nije dozvoljen",
            404 => "Nije pronađeno",
            409 => "Zahtev je u konfliktu",
            429 => "Previše zahteva",
            503 => "Usluga nije dostupna",
            _ => status >= 500 ? "Interna greška" : "Zahtev nije uspeo"
        };
        problem.Detail = detail;
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
    }

    private static ObjectResult Response(int status, ProblemDetails problem)
    {
        var result = new ObjectResult(problem) { StatusCode = status };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }
}
