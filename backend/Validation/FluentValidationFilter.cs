using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PadelBooking.Api.Validation
{
    public class FluentValidationFilter : IAsyncActionFilter
    {
        private readonly IServiceProvider _serviceProvider;

        public FluentValidationFilter(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var failures = new Dictionary<string, List<string>>();

            foreach (var argument in context.ActionArguments.Values)
            {
                if (argument == null)
                {
                    continue;
                }

                var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

                if (_serviceProvider.GetService(validatorType) is not IValidator validator)
                {
                    continue;
                }

                var validationContext = new ValidationContext<object>(argument);
                var result = await validator.ValidateAsync(
                    validationContext,
                    context.HttpContext.RequestAborted
                );

                foreach (var error in result.Errors)
                {
                    var propertyName = ToCamelCase(error.PropertyName);

                    if (!failures.TryGetValue(propertyName, out var messages))
                    {
                        messages = new List<string>();
                        failures[propertyName] = messages;
                    }

                    if (!messages.Contains(error.ErrorMessage))
                    {
                        messages.Add(error.ErrorMessage);
                    }
                }
            }

            if (failures.Count > 0)
            {
                context.Result = new BadRequestObjectResult(new
                {
                    message = "Podaci nisu ispravni.",
                    errors = failures
                });
                return;
            }

            await next();
        }

        private static string ToCamelCase(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return propertyName;
            }

            return char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
        }
    }
}
