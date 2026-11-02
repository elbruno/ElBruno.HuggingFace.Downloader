using System.CommandLine;
using ElBruno.HuggingFace.Cli.Commands;
using ElBruno.HuggingFace.Cli.Services;
using Spectre.Console;
using Xunit;

namespace ElBruno.HuggingFace.Cli.Tests;

/// <summary>
/// Tests configuration command output while preserving the existing configuration.
/// </summary>
[Collection("ConfigFile")]
public sealed class ConfigCommandTests : IDisposable
{
    private readonly string _configPath = ConfigManager.GetConfigPath();
    private readonly string? _originalContent;

    public ConfigCommandTests()
    {
        _originalContent = File.Exists(_configPath) ? File.ReadAllText(_configPath) : null;
    }

    public void Dispose()
    {
        if (_originalContent is not null)
            File.WriteAllText(_configPath, _originalContent);
        else if (File.Exists(_configPath))
            File.Delete(_configPath);
    }

    [Theory]
    [InlineData("default-token", "hf_test_secret_not_for_output", "****")]
    [InlineData("default-revision", "test-branch", "test-branch")]
    public async Task Set_PrintsDisplayValueWithoutExposingToken(string key, string value, string expectedDisplay)
    {
        using var output = new StringWriter();
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(output)
        });
        try
        {
            var root = new RootCommand();
            root.Add(ConfigCommand.Create());
            var configuration = new CommandLineConfiguration(root);

            Assert.Equal(0, await configuration.InvokeAsync(["config", "set", key, value]));
            Assert.Contains(expectedDisplay, output.ToString());
            if (key == "default-token")
            {
                Assert.DoesNotContain(value, output.ToString());
                Assert.Equal(value, new ConfigManager().Load().DefaultToken);
            }
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
