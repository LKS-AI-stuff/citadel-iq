using CitadelIQ.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CitadelIQ.Infrastructure.Email.Hosting;

/// <summary>The runtime connection string the leader lock uses (pooling off: closing the session releases the lock).</summary>
public sealed record EmailAgentLeaderConnection(string ConnectionString);

/// <summary>
/// Only the instance holding a PostgreSQL session-level advisory lock runs the email loops (one IMAP IDLE connection
/// per mailbox). The lock is held on a dedicated connection that is pinged every 30 s; losing the connection loses
/// leadership, and another instance can take over. <c>SKIP LOCKED</c> claims keep work safe even during a hand-over.
/// </summary>
public sealed class EmailAgentLeader(EmailAgentLeaderConnection connection, IEmailAgentMonitor monitor, ILogger<EmailAgentLeader> logger) : BackgroundService
{
    /// <summary>"CIQEMAIL" as a 64-bit key.</summary>
    public const long LockKey = 0x4349_5145_4D41_494C;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private volatile bool _isLeader;

    public bool IsLeader => _isLeader;

    private void SetLeader(bool value)
    {
        _isLeader = value;
        monitor.RecordLeadership(value);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Pooling = false }.ConnectionString;
        NpgsqlConnection? session = null;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (session is null)
                    {
                        session = new NpgsqlConnection(connectionString);
                        await session.OpenAsync(stoppingToken);
                        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", session);
                        command.Parameters.AddWithValue("key", LockKey);
                        SetLeader((bool)(await command.ExecuteScalarAsync(stoppingToken))!);

                        if (_isLeader)
                        {
                            logger.LogInformation("This instance now runs the email agent");
                        }
                        else
                        {
                            await session.DisposeAsync();
                            session = null;
                        }
                    }
                    else
                    {
                        await using var ping = new NpgsqlCommand("SELECT 1", session);
                        await ping.ExecuteScalarAsync(stoppingToken);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    if (_isLeader)
                    {
                        logger.LogWarning("Email agent leadership lost ({ExceptionType})", ex.GetType().Name);
                    }

                    SetLeader(false);
                    if (session is not null)
                    {
                        await session.DisposeAsync();
                        session = null;
                    }
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            SetLeader(false);
            if (session is not null)
            {
                await session.DisposeAsync(); // ends the session, releasing the advisory lock
            }
        }
    }
}
