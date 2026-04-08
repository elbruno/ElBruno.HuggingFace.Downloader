using System.CommandLine;
using ElBruno.HuggingFace.Cli.Commands;

var rootCommand = new RootCommand(
    "HuggingFace Downloader CLI — download, manage, and inspect cached models from Hugging Face Hub.");

// Download & check commands
rootCommand.Add(DownloadCommand.Create());
rootCommand.Add(CheckCommand.Create());

// Cache management commands
rootCommand.Add(ListCommand.Create());
rootCommand.Add(InfoCommand.Create());
rootCommand.Add(DeleteCommand.Create());
rootCommand.Add(DeleteFileCommand.Create());
rootCommand.Add(PurgeCommand.Create());

// Configuration commands
rootCommand.Add(ConfigCommand.Create());

var config = new CommandLineConfiguration(rootCommand);
return await config.InvokeAsync(args);
