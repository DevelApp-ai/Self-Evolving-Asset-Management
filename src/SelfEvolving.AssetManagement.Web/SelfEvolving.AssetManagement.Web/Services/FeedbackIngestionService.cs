using System.Collections.Concurrent;
using System.Threading;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class FeedbackIngestionService
{
    private readonly ConcurrentDictionary<int, FeedbackRecord> _feedbackById = new();
    private int _nextId;

    public IReadOnlyList<FeedbackRecord> GetAll() =>
        _feedbackById.Values
            .OrderByDescending(x => x.Id)
            .ToArray();

    public FeedbackRecord? GetById(int id) =>
        _feedbackById.TryGetValue(id, out var feedback) ? feedback : null;

    public FeedbackRecord Create(CreateFeedbackRequest request)
    {
        var source = request.Source?.Trim();
        var subject = request.Subject?.Trim();
        var message = request.Message?.Trim();

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Source is required.");
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Subject is required.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message is required.");
        }

        var id = Interlocked.Increment(ref _nextId);
        var created = new FeedbackRecord(id, source, subject, message, DateTime.UtcNow);
        _feedbackById[id] = created;
        return created;
    }
}
