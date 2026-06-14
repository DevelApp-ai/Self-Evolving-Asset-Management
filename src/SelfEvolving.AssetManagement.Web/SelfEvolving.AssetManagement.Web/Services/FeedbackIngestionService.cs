using System.Collections.Concurrent;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using SelfEvolving.AssetManagement.Web.Data;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class FeedbackIngestionService
{
    private readonly AssetManagementDbContext? _dbContext;
    private readonly ConcurrentDictionary<int, FeedbackRecord> _feedbackById = new();
    private int _nextId;

    public FeedbackIngestionService(AssetManagementDbContext? dbContext = null)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<FeedbackRecord> GetAll() =>
        _dbContext is null
            ? _feedbackById.Values.OrderByDescending(x => x.Id).ToArray()
            : _dbContext.Feedback.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Select(x => new FeedbackRecord(x.Id, x.Source, x.Subject, x.Message, x.SubmittedUtc))
                .ToArray();

    public FeedbackRecord? GetById(int id) =>
        _dbContext is null
            ? (_feedbackById.TryGetValue(id, out var feedback) ? feedback : null)
            : _dbContext.Feedback.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new FeedbackRecord(x.Id, x.Source, x.Subject, x.Message, x.SubmittedUtc))
                .SingleOrDefault();

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

        if (_dbContext is not null)
        {
            var entity = new FeedbackEntity
            {
                Source = source,
                Subject = subject,
                Message = message,
                SubmittedUtc = DateTime.UtcNow
            };
            _dbContext.Feedback.Add(entity);
            _dbContext.SaveChanges();
            return new FeedbackRecord(entity.Id, entity.Source, entity.Subject, entity.Message, entity.SubmittedUtc);
        }

        var id = Interlocked.Increment(ref _nextId);
        var created = new FeedbackRecord(id, source, subject, message, DateTime.UtcNow);
        _feedbackById[id] = created;
        return created;
    }
}
