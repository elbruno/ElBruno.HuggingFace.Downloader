using System.CommandLine;
using ElBruno.HuggingFace;
using ElBruno.HuggingFace.Cli.Models;
using ElBruno.HuggingFace.Cli.Services;
using Spectre.Console;

namespace ElBruno.HuggingFace.Cli.Commands;

/// <summary>
/// The <c>config</c> command — show, set, or reset persistent CLI configuration.
/// </summary>
public static class ConfigCommand
{
    /// <summary>
    /// Creates the <c>config</c> <see cref="Command"/> with subcommands: show, set, reset.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("config", "Show or modify configuration");
        command.Add(CreateShowCommand());
        command.Add(CreateSetCommand());
        command.Add(CreateResetCommand());
        return command;
    }

    private static Command CreateShowCommand()
    {
        var command = new Command("show", "Display current configuration");

        command.SetAction((_, _) =>
        {
            var manager = new ConfigManager();
            var config = manager.Load();
            var configPath = ConfigManager.GetConfigPath();

            var table = new Table();
            table.AddColumn("Key");
            table.AddColumn("Value");

            foreach (var (key, description) in CliConfig.KnownKeys)
            {
                var value = ConfigManager.GetValue(config, key) ?? "(unknown)";
                table.AddRow(
                    $"[bold]{Markup.Escape(key)}[/] [dim]({Markup.Escape(description)})[/]",
                    Markup.Escape(value));
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Config file:[/] {Markup.Escape(configPath)}");
            AnsiConsole.MarkupLine($"[dim]Cache dir:[/]   {Markup.Escape(config.CacheDirectory ?? DefaultPathHelper.GetDefaultCacheDirectory("hfdownload"))}");

            return Task.FromResult(0);
        });

        return command;
    }

    private static Command CreateSetCommand()
    {
        var keyArg = new Argument<string>("key")
        {
            Description = "Configuration key to set"
        };
        keyArg.AcceptOnlyFromAmong([.. CliConfig.KnownKeys.Keys]);

        var valueArg = new Argument<string>("value")
        {
            Description = "New value for the configuration key"
        };

        var command = new Command("set", "Set a configuration value");
        command.Add(keyArg);
        command.Add(valueArg);

        command.SetAction((parseResult, _) =>
        {
            var key = parseResult.GetValue(keyArg)!;
            var value = parseResult.GetValue(valueArg)!;

            var manager = new ConfigManager();
            var config = manager.Load();

            if (!manager.SetValue(config, key, value))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid value [bold]{Markup.Escape(value)}[/] for key [bold]{Markup.Escape(key)}[/]");
                return Task.FromResult(1);
            }

            manager.Save(config);
            AnsiConsole.MarkupLine($"[green]✓[/] Set [bold]{Markup.Escape(key)}[/] = {Markup.Escape(value)}");
            return Task.FromResult(0);
        });

        return command;
    }

    private static Command CreateResetCommand()
    {
        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Skip confirmation prompt"
        };

        var command = new Command("reset", "Reset configuration to defaults");
        command.Add(forceOption);

        command.SetAction((parseResult, _) =>
        {
            var force = parseResult.GetValue(forceOption);

            if (!force)
            {
                if (!AnsiConsole.Confirm("Reset all configuration to defaults?", defaultValue: false))
                {
                    AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                    return Task.FromResult(0);
                }
            }

            var manager = new ConfigManager();
            if (manager.Reset())
            {
                AnsiConsole.MarkupLine("[green]✓[/] Configuration reset to defaults.");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]No config file found — already using defaults.[/]");
            }

            return Task.FromResult(0);
        });

        return command;
    }
}
