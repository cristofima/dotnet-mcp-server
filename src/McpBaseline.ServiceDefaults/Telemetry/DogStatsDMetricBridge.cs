using System.Diagnostics.Metrics;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StatsdClient;

namespace McpBaseline.ServiceDefaults.Telemetry;

/// <summary>
/// Bridges System.Diagnostics.Metrics to Datadog via DogStatsD named pipes.
/// Activates when no OTLP endpoint is configured. See ServiceDefaults README for details.
/// </summary>
public sealed class DogStatsDMetricBridge : IHostedService, IDisposable
{
    private readonly ILogger<DogStatsDMetricBridge> _logger;
    private readonly HashSet<string> _meterNames;
    private MeterListener? _listener;
    private bool _dogStatsDInitialized;

    public DogStatsDMetricBridge(ILogger<DogStatsDMetricBridge> logger, IEnumerable<string> meterNames)
    {
        _logger = logger;
        _meterNames = new HashSet<string>(meterNames, StringComparer.OrdinalIgnoreCase);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var config = new StatsdConfig();

            // Read named pipe from Datadog extension env vars (client doesn't auto-detect).
            var pipeName = Environment.GetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME")
                        ?? Environment.GetEnvironmentVariable("DD_DOGSTATSD_WINDOWS_PIPE_NAME");

            if (!string.IsNullOrEmpty(pipeName))
            {
                config.PipeName = pipeName;
                _logger.LogInformation("DogStatsD using named pipe transport: {PipeName}", pipeName);
            }

            DogStatsd.Configure(config);
            _dogStatsDInitialized = true;
            _logger.LogInformation("DogStatsD metric bridge initialized — listening to meters: {Meters}",
                string.Join(", ", _meterNames));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to initialize DogStatsD \u2014 custom metrics will NOT be reported");
            return Task.CompletedTask;
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Failed to initialize DogStatsD — custom metrics will NOT be reported");
            return Task.CompletedTask;
        }

        _listener = new MeterListener
        {
            InstrumentPublished = OnInstrumentPublished
        };
        _listener.SetMeasurementEventCallback<long>(OnMeasurementLong);
        _listener.SetMeasurementEventCallback<double>(OnMeasurementDouble);
        _listener.Start();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _listener?.Dispose();
        _listener = null;

        if (_dogStatsDInitialized)
        {
            DogStatsd.Dispose();
            _dogStatsDInitialized = false;
        }

        _logger.LogInformation("DogStatsD metric bridge stopped");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _listener?.Dispose();
        if (!_dogStatsDInitialized)
        {
            return;
        }

        DogStatsd.Dispose();
        _dogStatsDInitialized = false;
    }

    private void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        if (!_meterNames.Contains(instrument.Meter.Name))
        {
            return;
        }

        if (instrument is not (Counter<long> or Histogram<double> or Histogram<long>))
        {
            return;
        }

        listener.EnableMeasurementEvents(instrument);
        _logger.LogDebug("DogStatsD bridge subscribed to {MeterName}/{InstrumentName} ({Type})",
            instrument.Meter.Name, instrument.Name, instrument.GetType().Name);
    }

    private void OnMeasurementLong(Instrument instrument, long measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (!_dogStatsDInitialized)
        {
            return;
        }

        var ddTags = ConvertTags(tags);

        if (instrument is Counter<long> or Histogram<long>)
        {
            DogStatsd.Distribution(instrument.Name, (double)measurement, tags: ddTags);
        }
    }

    private void OnMeasurementDouble(Instrument instrument, double measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (!_dogStatsDInitialized)
        {
            return;
        }

        var ddTags = ConvertTags(tags);

        if (instrument is Histogram<double>)
        {
            DogStatsd.Distribution(instrument.Name, measurement, tags: ddTags);
        }
    }

    /// <summary>
    /// Converts OTel-style tags to DogStatsD "key:value" string array.
    /// Returns null when there are no tags — DogStatsd.Distribution accepts string[]?.
    /// </summary>
    private static string[]? ConvertTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (tags.IsEmpty)
        {
            return null;
        }

        var result = new string[tags.Length];
        for (var i = 0; i < tags.Length; i++)
        {
            var tag = tags[i];
            result[i] = $"{tag.Key}:{tag.Value}";
        }
        return result;
    }
}
