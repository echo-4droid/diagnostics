// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Timers;
using Microsoft.Diagnostics.Monitoring.EventPipe;

namespace Microsoft.Diagnostics.Tools.Counters.Exporters;

internal class JSONPickExporter : ICounterRenderer, IDisposable
{
    private readonly object _lock = new();
    private readonly string _output;
    private readonly TimeSpan _flushTimeout = TimeSpan.FromMilliseconds(500);
    private readonly Timer _flushTimer;
    private StringBuilder builder;

    public JSONPickExporter(string output)
    {
        if (output.EndsWith(".json"))
        {
            _output = output;
        }
        else
        {
            _output = output + ".json";
        }

        _flushTimer = new Timer(_flushTimeout.TotalMilliseconds)
        {
            AutoReset = false,
            Enabled = false,
        };

        _flushTimer.Elapsed += OnFlushElapsed;
    }

    public void Initialize()
    {
        if (File.Exists(_output))
        {
            Console.WriteLine($"[Warning] {_output} already exists. This file will be overwritten.");
            File.Delete(_output);
        }

        lock (_lock)
        {
            builder = new StringBuilder();
            builder
                .Append("{ \"Timestamp\": \"").Append(DateTime.Now.ToString("O")).Append("\",");
        }
    }

    public void EventPipeSourceConnected()
    {
        Console.WriteLine("Starting a counter session. Press Q to quit.");
    }

    public void SetErrorText(string errorText)
    {
        Console.WriteLine(errorText);
    }

    public void ToggleStatus(bool paused)
    {
        // Do nothing
    }

    public void CounterPayloadReceived(CounterPayload payload, bool _)
    {
        lock (_lock)
        {
            if (builder.Length == 0)
            {
                builder
                    .Append("{ \"Timestamp\": \"").Append(DateTime.Now.ToString("O")).Append("\",");
            }

            builder
                .Append(" \"").Append(JsonNameEscape(payload.DisplayName)).Append("\": ")
                .Append(payload.Value.ToString(CultureInfo.InvariantCulture)).Append(',');

            if (_flushTimer.Enabled)
            {
                _flushTimer.Stop();
            }

            _flushTimer.Start();
        }
    }

    public void CounterStopped(CounterPayload payload) { }

    public void Stop()
    {
        _flushTimer?.Stop();
        Flush();
        Console.WriteLine("File saved to " + _output);
    }

    private void OnFlushElapsed(object sender, ElapsedEventArgs e) => Flush();

    private void Flush()
    {
        lock (_lock)
        {
            builder.Remove(builder.Length - 1, 1); // Remove the last comma to ensure valid JSON format.
            builder.Append(" }");
            // Write all text to the file.
            File.WriteAllText(_output, builder.ToString());

            builder.Clear();
        }
    }

    private static string JsonNameEscape(string input)
    {
        return input.Replace(" ", "_").Replace("%", "Percent").Replace("(", "").Replace(")", "");
    }

    public void Dispose()
    {
        _flushTimer?.Close();
        _flushTimer?.Dispose();
    }
}
