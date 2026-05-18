using StrykerRepro.Services;

namespace StrykerRepro.Tests;

public class NotificationServiceTests
{
    private readonly NotificationService _sut = new();

    [Fact]
    public void Send_ValidArguments_ReturnsSentNotification()
    {
        var result = _sut.Send(id: 1, recipient: "alice@example.com", body: "Hello");

        Assert.Equal(1, result.Id);
        Assert.Equal("alice@example.com", result.Recipient);
        Assert.Equal("Hello", result.Body);
        Assert.False(result.IsUrgent);
    }

    [Fact]
    public void Send_UrgentFlag_SetsIsUrgentTrue()
    {
        var result = _sut.Send(id: 2, recipient: "bob@example.com", body: "Urgent!", urgent: true);

        Assert.True(result.IsUrgent);
    }

    [Fact]
    public void Send_EmptyRecipient_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _sut.Send(id: 3, recipient: "", body: "Hello"));
    }

    [Fact]
    public void GetSent_AfterTwoSends_ReturnsBothNotifications()
    {
        _sut.Send(1, "a@example.com", "First");
        _sut.Send(2, "b@example.com", "Second");

        Assert.Equal(2, _sut.GetSent().Count);
    }
}
