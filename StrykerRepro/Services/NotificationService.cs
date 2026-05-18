using StrykerRepro.Messages;

namespace StrykerRepro.Services;

public class NotificationService
{
    private readonly List<Notification> _sent = [];

    /// <summary>
    /// Demonstrates Bug 3: ObjectCreationMutator + required members.
    ///
    /// Stryker generates 'new Notification {}' as a mutation of the initialiser
    /// below. This is invalid because Id, Recipient, and Body are required, so
    /// the mutated compilation fails.
    /// </summary>
    public Notification Send(int id, string recipient, string body, bool urgent = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        var notification = new Notification
        {
            Id = id,
            Recipient = recipient,
            Body = body,
            IsUrgent = urgent,
        };

        _sent.Add(notification);
        return notification;
    }

    public IReadOnlyList<Notification> GetSent() => _sent.AsReadOnly();
}
