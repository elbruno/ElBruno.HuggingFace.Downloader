using System.CommandLine;
using ElBruno.HuggingFace.Cli.Commands;

var rootCommand = new RootCommand(
    "HuggingFace Downloader CLI — download, manage, and inspect cached models from Hugging Face Hub.");

// Download & check commands (Phase 2)
rootCommand.Add(DownloadCommand.Create());
rootCommand.Add(CheckCommand.Create());

// Cache management commands (Phase 3)
rootCommand.Add(ListCommand.Create());
rootCommand.Add(InfoCommand.Create());
rootCommand.Add(DeleteCommand.Create());
rootCommand.Add(DeleteFileCommand.Create());
rootCommand.Add(PurgeCommand.Create());

// Stub commands (deferred)
var configStub = new Command("config", "Show or modify configuration");
configStub.SetAction(_ => Console.WriteLine("[config] Not yet implemented."));
rootCommand.Add(configStub);

var config = new CommandLineConfiguration(rootCommand);
return await config.InvokeAsync(args);
