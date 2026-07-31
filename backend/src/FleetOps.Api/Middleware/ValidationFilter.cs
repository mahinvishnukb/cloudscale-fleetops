using FleetOps.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FleetOps.Api.Middleware;

/// <summary>
/// Runs any registered FluentValidation validator against each action argument.
/// Replaces the deprecated FluentValidation.AspNetCore auto-validation package.
/// </summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count > 0)
        {
            var errors = failures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

            throw new ValidationFailedException(errors);
        }

        await next();
    }
}
