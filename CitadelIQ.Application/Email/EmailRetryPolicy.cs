using System.Data.Common;
using CitadelIQ.Application.Common;

namespace CitadelIQ.Application.Email;

/// <summary>Which failures are worth retrying (model, mailbox, network, database); anything else fails at once.</summary>
public static class EmailRetryPolicy
{
    public static bool IsTransient(Exception ex) => ex switch
    {
        AnswerGenerationException or MailboxUnavailableException or TimeoutException or IOException
            or DbException or HttpRequestException => true,
        OperationCanceledException => true, // a timeout inside a client, not our own shutdown (callers filter that)
        AggregateException { InnerExceptions.Count: > 0 } aggregate => aggregate.InnerExceptions.All(IsTransient),
        _ => ex.InnerException is not null && IsTransient(ex.InnerException)
    };
}
