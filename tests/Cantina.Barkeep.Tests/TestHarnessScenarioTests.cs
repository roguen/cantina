// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text.Json;
using Cantina.TestHarness;

namespace Cantina.Barkeep.Tests;

public sealed class TestHarnessScenarioTests
{
    public static TheoryData<string> ScenarioNames =>
        new(ScenarioCatalog.Names.ToArray());

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public async Task EveryRegisteredScenarioPasses(string scenarioName)
    {
        var result = await ScenarioCatalog.RunAsync(scenarioName);

        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Failures));
    }

    [Fact]
    public async Task JsonReportIsValidAndDeterministic()
    {
        var first = HarnessRunner.SerializeJson(
            HarnessRunner.CreateReport(await ScenarioCatalog.RunAllAsync()));
        var second = HarnessRunner.SerializeJson(
            HarnessRunner.CreateReport(await ScenarioCatalog.RunAllAsync()));

        Assert.Equal(first, second);

        using var document = JsonDocument.Parse(first);
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.True(root.GetProperty("passed").GetBoolean());
        Assert.Equal(
            ScenarioCatalog.Names.Count,
            root.GetProperty("scenarios").GetArrayLength());
    }

    [Fact]
    public async Task ListCommandPrintsEveryScenario()
    {
        using var output = new StringWriter { NewLine = "\r\n" };
        using var error = new StringWriter { NewLine = "\r\n" };

        var exitCode = await HarnessRunner.RunCliAsync(["list"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Equal(
            string.Concat(ScenarioCatalog.Names.Select(name => $"{name}\n")),
            output.ToString());
    }

    [Fact]
    public async Task JsonCommandUsesLiteralLineFeeds()
    {
        using var output = new StringWriter { NewLine = "\r\n" };
        using var error = new StringWriter { NewLine = "\r\n" };

        var exitCode = await HarnessRunner.RunCliAsync(
            ["run", "all", "--format", "json"],
            output,
            error);

        var json = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("passed").GetBoolean());
    }

    [Fact]
    public async Task UnknownScenarioReturnsUsageExitCode()
    {
        using var output = new StringWriter { NewLine = "\r\n" };
        using var error = new StringWriter { NewLine = "\r\n" };

        var exitCode = await HarnessRunner.RunCliAsync(
            ["run", "does-not-exist"],
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal("Unknown scenario: does-not-exist\n", error.ToString());
    }
}
