using System.Collections;
using System.Runtime.InteropServices;
using PSPad.Backup.Configuration;
using PSPad.Backup.GoogleDrive;
using PSPad.Backup.Runs;
using PSPad.Backup.Scheduling;

namespace PSPad.Backup;

public static class BackupCli
{
    const string Usage = "run | check | now | list | restore <name|latest> [--from local|s3|gdrive] [--yes] | healthcheck | gdrive-login";

    public static async Task<int> RunAsync(string[] args)
    {
        var log = new BackupLog(Console.Error);
        using var stop = new CancellationTokenSource();
        using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            stop.Cancel();
        });
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stop.Cancel();
        };

        try
        {
            await DispatchAsync(args.FirstOrDefault() ?? "run", args.Skip(1).ToArray(), log, stop.Token);
            return 0;
        }
        catch (BackupFailure failure)
        {
            log.Info(failure.Message);
            return 1;
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception)
        {
            log.Info($"unexpected failure: {exception.Message}");
            return 1;
        }
    }

    static async Task DispatchAsync(string command, string[] rest, BackupLog log, CancellationToken ct)
    {
        var clock = TimeProvider.System;

        if (command == "gdrive-login")
        {
            await LoginToDriveAsync(clock, ct);
            return;
        }

        if (command == "healthcheck")
        {
            var health = new Health.HealthStamps(BackupSettings.StartedStamp, BackupSettings.SuccessStamp, clock);
            if (!health.IsHealthy())
            {
                throw new BackupFailure("no successful backup in the last 26 hours");
            }

            return;
        }

        var settings = BackupSettings.Read(Environment());
        var composition = new BackupComposition(settings, clock, log);
        var schedule = new DailySchedule(settings.At, settings.TimeZone);

        switch (command)
        {
            case "check":
                await composition.RequireRecipientAsync(ct);
                Console.WriteLine($"{schedule.Describe()}, next run {schedule.NextAfter(clock.GetUtcNow()):u}, destinations: {Destinations(settings)}");
                break;
            case "now":
                await composition.RequireRecipientAsync(ct);
                await composition.Backup().RunAsync(ct);
                break;
            case "run":
                await composition.RequireRecipientAsync(ct);
                await ScheduleAsync(composition, schedule, settings, clock, log, ct);
                break;
            case "list":
                foreach (var store in new[] { composition.Local }.Concat(composition.Remotes))
                {
                    Console.WriteLine($"{store.Name}:");
                    foreach (var file in (await store.ListAsync(ct)).Order(StringComparer.Ordinal))
                    {
                        Console.WriteLine($"  {file}");
                    }
                }

                break;
            case "restore":
                await composition.Restore().RestoreAsync(ParseRestore(rest), ct);
                break;
            default:
                throw new BackupFailure($"unknown command '{command}' ({Usage})");
        }
    }

    static async Task ScheduleAsync(
        BackupComposition composition, DailySchedule schedule, BackupSettings settings, TimeProvider clock, BackupLog log, CancellationToken ct)
    {
        composition.Health.MarkStarted();
        log.Info($"{schedule.Describe()}, destinations: {Destinations(settings)}");

        while (!ct.IsCancellationRequested)
        {
            var next = schedule.NextAfter(clock.GetUtcNow());
            await Task.Delay(next - clock.GetUtcNow(), clock, ct);

            try
            {
                await composition.Backup().RunAsync(ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                log.Info(exception.Message);
            }
        }
    }

    static RestoreRequest ParseRestore(string[] rest)
    {
        var backup = "latest";
        var from = "local";
        var confirmed = false;

        for (var index = 0; index < rest.Length; index++)
        {
            switch (rest[index])
            {
                case "--from" when index + 1 < rest.Length:
                    from = rest[++index];
                    break;
                case "--yes":
                    confirmed = true;
                    break;
                case var option when option.StartsWith('-'):
                    throw new BackupFailure($"unknown option {option} ({Usage})");
                default:
                    backup = rest[index];
                    break;
            }
        }

        return new RestoreRequest(backup, from, confirmed);
    }

    static async Task LoginToDriveAsync(TimeProvider clock, CancellationToken ct)
    {
        var env = Environment();
        var clientId = env.GetValueOrDefault("BACKUP_GDRIVE_CLIENT_ID") ?? throw new BackupFailure("BACKUP_GDRIVE_CLIENT_ID is not set");
        var clientSecret = env.GetValueOrDefault("BACKUP_GDRIVE_CLIENT_SECRET") ?? throw new BackupFailure("BACKUP_GDRIVE_CLIENT_SECRET is not set");

        var token = await new DriveDeviceLogin(new HttpClient(), clock, Console.Error).RefreshTokenAsync(clientId, clientSecret, ct);
        Console.WriteLine($"BACKUP_GDRIVE_REFRESH_TOKEN={token}");
    }

    static string Destinations(BackupSettings settings) =>
        string.Join(", ", new[] { "local", settings.S3 is null ? null : "s3", settings.Drive is null ? null : "gdrive" }.OfType<string>());

    static Dictionary<string, string?> Environment() =>
        System.Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value as string))
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value);
}
