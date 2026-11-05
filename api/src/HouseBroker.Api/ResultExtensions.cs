using System.Globalization;
using System.Text.Json;
using HouseBroker.Api.Localization;
using HouseBroker.Application.Contracts;
using HouseBroker.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace HouseBroker.Api;

/// <summary>Result to action result mapping.</summary>
public static class ResultExtensions
{
    /// <summary>Maps a non-generic result.</summary>
    public static IActionResult ToActionResult(this Result result, ControllerBase controller)
        => result.IsSuccess
            ? controller.NoContent()
            : ToProblemResult(controller, result.Error!);

    /// <summary>Maps a generic result.</summary>
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller, Func<T?, IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess is null ? controller.Ok(result.Value) : onSuccess(result.Value);
        }

        return ToProblemResult(controller, result.Error!);
    }

    private static IActionResult ToProblemResult(ControllerBase controller, ApplicationError error)
    {
        var statusCode = error.Kind switch
        {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
            ErrorKind.PreconditionRequired => StatusCodes.Status428PreconditionRequired,
            ErrorKind.RateLimited => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status400BadRequest,
        };

        // UseRequestLocalization sets the UI culture to the negotiated request culture ("en" or "ne").
        var format = ArgumentFormat.For(CultureInfo.CurrentUICulture);
        var localizer = controller.HttpContext.RequestServices.GetService<IStringLocalizer<SharedResource>>();
        var message = localizer is null ? error.Code : localizer[error.Code];

        var problem = new ProblemDetails
        {
            Status = statusCode,
            // Title is localized like Detail; the stable code stays in the "code" extension.
            Title = message,
            Detail = AppendRelatedTiers(message, error.RelatedRecords, localizer, format),
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["correlationId"] = controller.HttpContext.GetCorrelationId();
        if (error.RelatedIds.Count > 0)
        {
            problem.Extensions["relatedIds"] = error.RelatedIds.ToList();
        }

        var fieldErrors = FieldErrorsOf(error);
        if (fieldErrors.Count > 0)
        {
            // The first failing field is also exposed as "field" for single-field callers.
            problem.Extensions["field"] = fieldErrors[0].Field;
            problem.Extensions["errors"] = fieldErrors
                .GroupBy(entry => entry.Field, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(entry => ToFieldErrorDetail(entry, localizer, format)).ToList(),
                    StringComparer.Ordinal);
        }

        return new ObjectResult(problem) { StatusCode = statusCode };
    }

    /// <summary>Returns the per-field entries (camelCase field names), synthesizing one from <see cref="ApplicationError.Field"/> when needed.</summary>
    private static List<(string Field, string Code, IReadOnlyList<object?> Args)> FieldErrorsOf(ApplicationError error)
    {
        var source = error.FieldErrors.Count > 0
            ? error.FieldErrors
            : string.IsNullOrWhiteSpace(error.Field)
                ? Array.Empty<FieldError>()
                : new[] { new FieldError(error.Field, error.Code) };

        return source
            .Select(entry => (Field: JsonNamingPolicy.CamelCase.ConvertName(entry.Field), entry.Code, entry.Args))
            .ToList();
    }

    private static FieldErrorDetail ToFieldErrorDetail(
        (string Field, string Code, IReadOnlyList<object?> Args) entry,
        IStringLocalizer<SharedResource>? localizer,
        ArgumentFormat format)
    {
        var args = entry.Args.Select(value => format.Format(value)).ToList();
        return new FieldErrorDetail(entry.Code, Localize(localizer, entry.Code, args), args);
    }

    /// <summary>
    /// Appends a sentence that names the existing bands the caller must fix first, when the error carries their bounds.
    /// Each record's arguments are <c>[maxPrice-or-null, maxInclusive]</c>.
    /// </summary>
    private static string AppendRelatedTiers(
        string message,
        IReadOnlyList<RelatedRecord> records,
        IStringLocalizer<SharedResource>? localizer,
        ArgumentFormat format)
    {
        var ranges = records
            .Where(record => record.Args.Count == 2)
            .Select(record => FormatTierRange(record.Args, format))
            .ToList();
        if (ranges.Count == 0)
        {
            return message;
        }

        var sentence = Localize(localizer, "validation.related_tiers", [string.Join("; ", ranges)]);
        return $"{message} {sentence}";
    }

    /// <summary>Formats one band's upper bound, for example <c>≤ 1,00,00,000</c>; an absent maximum is <c>∞</c>.</summary>
    private static string FormatTierRange(IReadOnlyList<object?> args, ArgumentFormat format)
    {
        if (args[0] is null)
        {
            return "∞";
        }

        var symbol = args[1] is true ? "≤" : "<";
        return $"{symbol} {format.Format(args[0])}";
    }

    private static string Localize(IStringLocalizer<SharedResource>? localizer, string key, IReadOnlyList<string> args)
    {
        if (localizer is null)
        {
            return key;
        }

        return args.Count == 0 ? localizer[key] : localizer[key, args.ToArray()];
    }

    /// <summary>
    /// Formats raw message arguments for a request culture. Numbers use Indian digit grouping (lakh and crore) for both
    /// English and Nepali; Nepali also uses Devanagari digits. Machine-readable values never pass through here.
    /// </summary>
    private sealed class ArgumentFormat
    {
        private static readonly NumberFormatInfo IndianNumbers = CreateNumbers("en-IN");

        private ArgumentFormat(NumberFormatInfo numbers, bool devanagariDigits)
        {
            Numbers = numbers;
            DevanagariDigits = devanagariDigits;
        }

        private NumberFormatInfo Numbers { get; }

        private bool DevanagariDigits { get; }

        /// <summary>Returns the format for a UI culture: <c>ne</c> uses Nepali digits, everything else English.</summary>
        public static ArgumentFormat For(CultureInfo uiCulture)
        {
            var nepali = string.Equals(uiCulture.TwoLetterISOLanguageName, "ne", StringComparison.OrdinalIgnoreCase);
            return nepali
                ? new ArgumentFormat(CreateNumbers("ne-NP"), devanagariDigits: true)
                : new ArgumentFormat(IndianNumbers, devanagariDigits: false);
        }

        /// <summary>Formats one raw argument value.</summary>
        public string Format(object? value)
        {
            var text = value switch
            {
                null => string.Empty,
                decimal amount => FormatNumber(amount),
                int whole => FormatNumber(whole),
                long whole => FormatNumber(whole),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            };

            return DevanagariDigits ? ToDevanagariDigits(text) : text;
        }

        /// <summary>Formats a number with at most two decimals and no trailing zeros.</summary>
        private string FormatNumber(decimal value)
        {
            var separator = Numbers.NumberDecimalSeparator;
            var text = value.ToString("N2", Numbers).TrimEnd('0');
            return text.EndsWith(separator, StringComparison.Ordinal) ? text[..^separator.Length] : text;
        }

        private static NumberFormatInfo CreateNumbers(string culture)
        {
            var numbers = (NumberFormatInfo)new CultureInfo(culture).NumberFormat.Clone();
            numbers.NumberGroupSizes = [3, 2];
            return numbers;
        }

        private static string ToDevanagariDigits(string text)
            => new(text.Select(character => character is >= '0' and <= '9' ? (char)('०' + (character - '0')) : character).ToArray());
    }
}
