// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cantina.TestHarness;

public sealed record HarnessEventReport(
    int Run,
    int Sequence,
    string State,
    string? Detail);

public sealed record HarnessScenarioResult(
    string Name,
    bool Passed,
    string Outcome,
    IReadOnlyList<HarnessEventReport> Events,
    IReadOnlyList<string> Calls,
    IReadOnlyList<string> Failures);

public sealed record HarnessReport(
    int SchemaVersion,
    bool Passed,
    IReadOnlyList<HarnessScenarioResult> Scenarios);

public static class HarnessRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static async Task<int> RunCliAsync(
        IReadOnlyList<string> arguments,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (arguments.Count == 1 &&
            string.Equals(arguments[0], "list", StringComparison.Ordinal))
        {
            foreach (var name in ScenarioCatalog.Names)
            {
                await output.WriteAsync(name);
                await output.WriteAsync("\n");
            }

            return 0;
        }

        var target = "all";
        var format = "text";
        if (arguments.Count > 0)
        {
            if (!string.Equals(arguments[0], "run", StringComparison.Ordinal) ||
                arguments.Count < 2)
            {
                return await WriteUsageErrorAsync(error);
            }

            target = arguments[1];
            if (arguments.Count == 4 &&
                string.Equals(arguments[2], "--format", StringComparison.Ordinal))
            {
                format = arguments[3];
            }
            else if (arguments.Count != 2)
            {
                return await WriteUsageErrorAsync(error);
            }
        }

        if (!string.Equals(format, "text", StringComparison.Ordinal) &&
            !string.Equals(format, "json", StringComparison.Ordinal))
        {
            await error.WriteAsync("Format must be 'text' or 'json'.\n");
            return 2;
        }

        IReadOnlyList<HarnessScenarioResult> scenarios;
        if (string.Equals(target, "all", StringComparison.Ordinal))
        {
            scenarios = await ScenarioCatalog.RunAllAsync(cancellationToken);
        }
        else if (ScenarioCatalog.Contains(target))
        {
            scenarios =
            [
                await ScenarioCatalog.RunAsync(target, cancellationToken),
            ];
        }
        else
        {
            await error.WriteAsync($"Unknown scenario: {target}\n");
            return 2;
        }

        var report = CreateReport(scenarios);
        if (string.Equals(format, "json", StringComparison.Ordinal))
        {
            await output.WriteAsync(SerializeJson(report));
            await output.WriteAsync("\n");
        }
        else
        {
            await output.WriteAsync(RenderText(report));
        }

        return report.Passed ? 0 : 1;
    }

    public static HarnessReport CreateReport(
        IReadOnlyList<HarnessScenarioResult> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        return new HarnessReport(1, scenarios.All(scenario => scenario.Passed), scenarios);
    }

    public static string SerializeJson(HarnessReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, JsonOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    public static string RenderText(HarnessReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var text = new StringBuilder();
        foreach (var scenario in report.Scenarios)
        {
            text.Append(scenario.Passed ? "PASS " : "FAIL ")
                .Append(scenario.Name)
                .Append(" (")
                .Append(scenario.Outcome)
                .AppendLine(")");

            foreach (var workflowEvent in scenario.Events)
            {
                text.Append("  ")
                    .Append(workflowEvent.Run)
                    .Append('.')
                    .Append(workflowEvent.Sequence.ToString("D2", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(' ')
                    .Append(workflowEvent.State);
                if (workflowEvent.Detail is not null)
                {
                    text.Append(": ").Append(workflowEvent.Detail);
                }

                text.AppendLine();
            }

            foreach (var failure in scenario.Failures)
            {
                text.Append("  assertion: ").AppendLine(failure);
            }
        }

        text.Append(report.Passed ? "All " : "Failed: ")
            .Append(report.Scenarios.Count)
            .AppendLine(report.Passed ? " scenarios passed." : " scenarios evaluated.");

        return text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static async Task<int> WriteUsageErrorAsync(TextWriter error)
    {
        await error.WriteAsync(
            "Usage: Cantina.TestHarness list | run <scenario|all> [--format text|json]\n");
        return 2;
    }
}
