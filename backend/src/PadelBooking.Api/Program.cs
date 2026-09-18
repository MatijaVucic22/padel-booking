using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PadelBooking.Api.Validation;
using PadelBooking.Api.Errors;
using PadelBooking.Api.Validators;
using PadelBooking.Api.Services;
using PadelBooking.Api.Hubs;
using PadelBooking.Api.Authentication;
using System.Threading.RateLimiting;
using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Payments;
using PadelBooking.Application;
using PadelBooking.Infrastructure;
using PadelBooking.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var courtImageStorageRoot = Path.Combine(
    builder.Environment.WebRootPath ??
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot"),
    "uploads",
    "courts");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration,
    courtImageStorageRoot);

builder.Services.AddScoped<FluentValidationFilter>();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddScoped<ICourtChangeNotifier, SignalRCourtChangeNotifier>();
builder.Services.AddScoped<IReservationNotificationLogger, ReservationNotificationLogger>();
builder.Services.AddScoped<IPaymentResolutionLogger, PaymentResolutionLogger>();
builder.Services.AddHostedService<ReservationReminderBackgroundService>();
builder.Services.AddHostedService<PaymentReconciliationBackgroundService>();
builder.Services.AddSignalR();
builder.Services.AddTransient<IProblemDetailsWriter, ApiProblemDetailsWriter>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var status = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
        var code = status switch
        {
            StatusCodes.Status400BadRequest => ApiErrorCodes.Validation,
            StatusCodes.Status401Unauthorized => ApiErrorCodes.Unauthorized,
            StatusCodes.Status403Forbidden => ApiErrorCodes.Forbidden,
            StatusCodes.Status404NotFound => ApiErrorCodes.NotFound,
            StatusCodes.Status429TooManyRequests => ApiErrorCodes.RateLimited,
            _ => status >= 500 ? ApiErrorCodes.Internal : ApiErrorCodes.RequestFailed
        };
        var detail = status == StatusCodes.Status500InternalServerError
            ? "Došlo je do neočekivane greške. Pokušajte ponovo."
            : status switch
            {
                401 => "Prijava je potrebna.",
                403 => "Pristup nije dozvoljen.",
                404 => "Traženi resurs nije pronađen.",
                429 => "Previše zahteva. Pokušajte ponovo kasnije.",
                _ => "Zahtev nije moguće obraditi."
            };
        ApiProblem.Populate(context.ProblemDetails, context.HttpContext, status, code, detail);
    };
});

builder.Services.AddControllers(options =>
{
    options.Filters.Add<FluentValidationFilter>();
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => NormalizeValidationField(entry.Key),
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrEmpty(error.ErrorMessage)
                        ? "Vrednost nije ispravna."
                        : error.ErrorMessage)
                    .ToArray()
            );

        return ApiProblem.Validation(context.HttpContext, errors);
    };
});

static string NormalizeValidationField(string key)
{
    if (string.IsNullOrWhiteSpace(key)) return "request";

    var field = key.StartsWith("$.", StringComparison.Ordinal)
        ? key[2..]
        : key.Split('.').Last();

    var bracketIndex = field.IndexOf('[');
    if (bracketIndex >= 0) field = field[..bracketIndex];

    return string.IsNullOrEmpty(field)
        ? "request"
        : char.ToLowerInvariant(field[0]) + field[1..];
}

var allowedFrontendOrigins = new List<string> { "http://localhost:5173" };
var additionalOrigin = builder.Configuration["Cors:AdditionalOrigin"];
if (!string.IsNullOrWhiteSpace(additionalOrigin)) allowedFrontendOrigins.Add(additionalOrigin.TrimEnd('/'));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy
            .WithOrigins(allowedFrontendOrigins.ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key nije konfigurisan.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer nije konfigurisan.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience nije konfigurisan.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrWhiteSpace(context.Request.Headers.Authorization))
                    context.Token = context.Request.Cookies[BrowserAuthCookie.Name];

                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            )
        };
    });



builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }
        ));

    options.OnRejected = async (context, cancellationToken) =>
    {
        await Results.Json(
            ((ObjectResult)ApiProblem.Result(context.HttpContext, 429, ApiErrorCodes.RateLimited,
                "Previše pokušaja prijave. Pokušajte ponovo za minut.")).Value,
            contentType: "application/problem+json", statusCode: 429)
            .ExecuteAsync(context.HttpContext);
    };
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("AllowReactApp");
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var isUnsafeMethod = !HttpMethods.IsGet(context.Request.Method) &&
        !HttpMethods.IsHead(context.Request.Method) &&
        !HttpMethods.IsOptions(context.Request.Method) &&
        !HttpMethods.IsTrace(context.Request.Method);
    var usesCookie = context.User.Identity?.IsAuthenticated == true &&
        context.Request.Cookies.ContainsKey(BrowserAuthCookie.Name) &&
        string.IsNullOrWhiteSpace(context.Request.Headers.Authorization);

    if (isUnsafeMethod && usesCookie)
    {
        var origin = context.Request.Headers.Origin.ToString();
        var serverOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
        if (string.IsNullOrWhiteSpace(origin) ||
            (!allowedFrontendOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase) &&
             !string.Equals(origin, serverOrigin, StringComparison.OrdinalIgnoreCase)))
        {
            await Results.Json(
                ((ObjectResult)ApiProblem.Result(context, 403, ApiErrorCodes.Forbidden,
                    "Zahtev nije dozvoljen sa ovog izvora.")).Value,
                contentType: "application/problem+json", statusCode: 403)
                .ExecuteAsync(context);
            return;
        }
    }

    await next(context);
});
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();
app.MapHub<CourtAvailabilityHub>("/hubs/court-availability");


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}



var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public partial class Program { }
