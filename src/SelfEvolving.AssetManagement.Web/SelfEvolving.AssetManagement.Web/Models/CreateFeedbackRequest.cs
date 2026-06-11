namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record CreateFeedbackRequest(
    string Source,
    string Subject,
    string Message);
