using System.CommandLine;
using Xunit;
using ElBruno.HuggingFace.Cli.Commands;

namespace ElBruno.HuggingFace.Cli.Tests;

/// <summary>
/// Tests for command registration, argument parsing, and structural correctness.
/// </summary>
public sealed class CommandParsingTests
{
    /// <summary>
    /// Builds the root command the same way Program.cs does.
    /// </summary>
    private static RootCommand BuildRootCommand()
    {
        var root = new RootCommand(
            "HuggingFace Downloader CLI — download, manage, and inspect cached models from Hugging Face Hub.");

        root.Add(DownloadCommand.Create());
        root.Add(CheckCommand.Create());
        root.Add(ListCommand.Create());
        root.Add(InfoCommand.Create());
        root.Add(DeleteCommand.Create());
        root.Add(DeleteFileCommand.Create());
        root.Add(PurgeCommand.Create());
        root.Add(ConfigCommand.Create());

        return root;
    }

    // ── Root command ────────────────────────────────────────────────

    [Fact]
    public void RootCommand_HasEightSubcommands()
    {
        var root = BuildRootCommand();

        Assert.Equal(8, root.Subcommands.Count);
    }

    [Theory]
    [InlineData("download")]
    [InlineData("check")]
    [InlineData("list")]
    [InlineData("info")]
    [InlineData("delete")]
    [InlineData("delete-file")]
    [InlineData("purge")]
    [InlineData("config")]
    public void RootCommand_ContainsExpectedSubcommand(string commandName)
    {
        var root = BuildRootCommand();

        Assert.Contains(root.Subcommands, c => c.Name == commandName);
    }

    // ── Download command ────────────────────────────────────────────

    [Fact]
    public void DownloadCommand_HasExpectedArguments()
    {
        var cmd = DownloadCommand.Create();

        Assert.Contains(cmd.Arguments, a => a.Name == "repo-id");
        Assert.Contains(cmd.Arguments, a => a.Name == "files");
    }

    [Fact]
    public void DownloadCommand_HasExpectedOptions()
    {
        var cmd = DownloadCommand.Create();
        var optionNames = GetOptionNames(cmd);

        Assert.Contains("--output", optionNames);
        Assert.Contains("--revision", optionNames);
        Assert.Contains("--token", optionNames);
        Assert.Contains("--optional", optionNames);
        Assert.Contains("--no-progress", optionNames);
        Assert.Contains("--quiet", optionNames);
    }

    // ── Check command ───────────────────────────────────────────────

    [Fact]
    public void CheckCommand_HasExpectedArguments()
    {
        var cmd = CheckCommand.Create();

        Assert.Contains(cmd.Arguments, a => a.Name == "repo-id");
        Assert.Contains(cmd.Arguments, a => a.Name == "files");
    }

    [Fact]
    public void CheckCommand_HasExpectedOptions()
    {
        var cmd = CheckCommand.Create();
        var optionNames = GetOptionNames(cmd);

        Assert.Contains("--output", optionNames);
        Assert.Contains("--revision", optionNames);
    }

    // ── List command ────────────────────────────────────────────────

    [Fact]
    public void ListCommand_HasExpectedOptions()
    {
        var cmd = ListCommand.Create();
        var optionNames = GetOptionNames(cmd);

        Assert.Contains("--cache-dir", optionNames);
        Assert.Contains("--format", optionNames);
    }

    // ── Info command ────────────────────────────────────────────────

    [Fact]
    public void InfoCommand_HasExpectedArgument()
    {
        var cmd = InfoCommand.Create();

        Assert.Contains(cmd.Arguments, a => a.Name == "repo-id");
    }

    [Fact]
    public void InfoCommand_HasExpectedOptions()
    {
        var cmd = InfoCommand.Create();
        var optionNames = GetOptionNames(cmd);

        Assert.Contains("--cache-dir", optionNames);
        Assert.Contains("--format", optionNames);
    }

    // ── Delete command ──────────────────────────────────────────────

    [Fact]
    public void DeleteCommand_HasExpectedArgument()
    {
        var cmd = DeleteCommand.Create();

        Assert.Contains(cmd.Arguments, a => a.Name == "repo-id");
    }

    [Fact]
    public void DeleteCommand_HasExpectedOptions()
    {
        var cmd = DeleteCommand.Create();
        var optionNames = GetOptionNames(cmd);

        Assert.Contains("--cache-dir", optionNames);
        Assert.Contains("--force", optionNames);
    }

    // ── DeleteFile command ──────────────────────────────────────────

    [Fact]
    public void DeleteFileCommand_HasExpectedArguments()
    {
        var cmd = DeleteFileCommand.Create();

        Assert.Contains(cmd.Arguments, a => a.Name == "repo-id");
        Assert.Contains(cmd.Arguments, a => a.Name == "file");
    }

    [Fact]
    public void DeleteFileCommand_HasExpectedOptions()
    {
        var cmd = DeleteFileCommand.Create();
        var optionNames = GetOptionNames(cmd);

        Assert.Contains("--cache-dir", optionNames);
        Assert.Contains("--force", optionNames);
    }

    // ── Purge command ───────────────────────────────────────────────

    [Fact]
    public void PurgeCommand_HasExpectedOptions()
    {
        var cmd = PurgeCommand.Create();
        var optionNames = GetOptionNames(cmd);

        Assert.Contains("--cache-dir", optionNames);
        Assert.Contains("--force", optionNames);
    }

    // ── Config command ──────────────────────────────────────────────

    [Fact]
    public void ConfigCommand_HasShowSetResetSubcommands()
    {
        var cmd = ConfigCommand.Create();

        Assert.Contains(cmd.Subcommands, c => c.Name == "show");
        Assert.Contains(cmd.Subcommands, c => c.Name == "set");
        Assert.Contains(cmd.Subcommands, c => c.Name == "reset");
    }

    [Fact]
    public void ConfigCommand_HasExactlyThreeSubcommands()
    {
        var cmd = ConfigCommand.Create();

        Assert.Equal(3, cmd.Subcommands.Count);
    }

    // ── Help text generation ────────────────────────────────────────

    [Fact]
    public void AllCommands_CreateWithoutThrowing()
    {
        // Ensures static Create() methods execute without errors
        var commands = new Command[]
        {
            DownloadCommand.Create(),
            CheckCommand.Create(),
            ListCommand.Create(),
            InfoCommand.Create(),
            DeleteCommand.Create(),
            DeleteFileCommand.Create(),
            PurgeCommand.Create(),
            ConfigCommand.Create(),
        };

        Assert.All(commands, cmd =>
        {
            Assert.NotNull(cmd);
            Assert.False(string.IsNullOrEmpty(cmd.Name));
            Assert.False(string.IsNullOrEmpty(cmd.Description));
        });
    }

    [Fact]
    public void RootCommand_HelpTextGeneration_DoesNotThrow()
    {
        var root = BuildRootCommand();
        var config = new CommandLineConfiguration(root);

        // Invoking --help shouldn't throw — the return code is informational
        var exception = Record.Exception(() =>
        {
            config.InvokeAsync(["--help"]).GetAwaiter().GetResult();
        });

        Assert.Null(exception);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static List<string> GetOptionNames(Command cmd)
    {
        return cmd.Options.Select(o => o.Name).ToList();
    }
}
