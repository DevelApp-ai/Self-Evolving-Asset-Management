namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record FeedbackRecord(
    int Id,
    string Source,
    string Subject,
    string Message,
    DateTime SubmittedUtc);
