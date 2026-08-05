using System.Text.Json;
using GrainTrade.Abstractions;
using GrainTrade.ApiHost.Streaming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GrainTrade.ApiHost;

// Settles the demo account on a short interval and pushes its state over SSE when
// it changes, so a background fill — e.g. the market maker lifting a resting order —
// updates holdings, cash and pending orders without a page reload.
//
// Single hardcoded account, matching the frontend's ACCOUNT_ID: this is a mock.
// A real app would key this per authenticated user and stream per-user.
public sealed class AccountLive(
    IClusterClient client,
    MarketFeed feed,
    ILogger<AccountLive> logger) : BackgroundService
{
    private static readonly Guid DemoAccount = new("11111111-1111-1111-1111-111111111111");
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1.5);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string _lastPushed = "";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }

                var account = client.GetGrain<IAccountGrain>(DemoAccount);
                // Settle claims any fills the account hasn't picked up yet.
                var update = new AccountUpdate(await account.Settle(), await account.GetOpenOrders());

                // Push only when the state actually changed.
                var signature = JsonSerializer.Serialize(update, Json);
                if (signature != _lastPushed)
                {
                    _lastPushed = signature;
                    feed.PublishAccount(update);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Account live tick failed");
            }
        }
    }
}
