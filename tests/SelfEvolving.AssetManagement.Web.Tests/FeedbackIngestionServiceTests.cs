using SelfEvolving.AssetManagement.Web.Models;
using SelfEvolving.AssetManagement.Web.Services;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class FeedbackIngestionServiceTests
{
    [Fact]
    public void Create_WithValidRequest_StoresFeedback()
    {
        var service = new FeedbackIngestionService();

        var created = service.Create(new CreateFeedbackRequest("UI", "Search usability", "Need better filtering"));

        Assert.Equal(1, created.Id);
        var all = service.GetAll();
        Assert.Single(all);
        Assert.Equal("Search usability", all[0].Subject);
    }

    [Fact]
    public void Create_WithMissingSource_ThrowsArgumentException()
    {
        var service = new FeedbackIngestionService();

        var action = () => service.Create(new CreateFeedbackRequest("", "Subject", "Message"));

        Assert.Throws<ArgumentException>(action);
    }
}
