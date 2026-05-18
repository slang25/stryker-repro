namespace StrykerRepro.Messages;

/// <summary>
/// Bug 3: ObjectCreationMutator + required members.
///
/// Stryker's ObjectCreationMutator generates a mutation for every 'new T { … }'
/// expression that strips all initialiser properties: 'new T {}'. When T
/// declares 'required' members this empty initialiser violates the C# compiler
/// constraint "Required member must be set in the object initializer" and
/// causes a compilation failure during mutation.
/// </summary>
public class Notification
{
    public required int Id { get; init; }
    public required string Recipient { get; init; }
    public required string Body { get; init; }

    public bool IsUrgent { get; init; }
}
