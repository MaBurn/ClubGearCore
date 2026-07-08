using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ClubGear.Models.Feedback;

public sealed class ActionFeedbackViewModel
{
    public const string TempDataKindKey = "ActionFeedback.Kind";
    public const string TempDataMessageKey = "ActionFeedback.Message";
    public const string ViewDataKey = "ActionFeedback";

    public string Kind { get; }

    public string Message { get; }

    public string AlertCssClass => Kind switch
    {
        "success" => "alert-success",
        "warning" => "alert-warning",
        _ => "alert-danger"
    };

    public ActionFeedbackViewModel(string kind, string message)
    {
        Kind = NormalizeKind(kind);
        Message = message;
    }

    public static ActionFeedbackViewModel Success(string message)
        => new("success", message);

    public static ActionFeedbackViewModel Warning(string message)
        => new("warning", message);

    public static ActionFeedbackViewModel Error(string message)
        => new("error", message);

    public static ActionFeedbackViewModel? FromTempData(ITempDataDictionary tempData)
    {
        if (tempData[TempDataKindKey] is not string kind || tempData[TempDataMessageKey] is not string message)
        {
            return null;
        }

        return new ActionFeedbackViewModel(kind, message);
    }

    private static string NormalizeKind(string? kind)
    {
        return kind?.Trim().ToLowerInvariant() switch
        {
            "success" => "success",
            "warning" => "warning",
            _ => "error"
        };
    }
}