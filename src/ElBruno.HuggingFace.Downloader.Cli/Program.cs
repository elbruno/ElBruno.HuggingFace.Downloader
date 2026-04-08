using System.CommandLine;

var rootCommand = new RootCommand(
    "HuggingFace Downloader CLI — download, manage, and inspect cached models from Hugging Face Hub.");

Command[] subcommands =
[
    new("download", "Download files from a Hugging Face repository"),
    new("check", "Check if files exist in the local cache"),
    new("list", "List downloaded models in the cache directory"),
    new("info", "Show details of a cached model"),
    new("delete", "Delete a cached model and all its files"),
    new("delete-file", "Delete a single file from a cached model"),
    new("purge", "Delete all cached models"),
    new("config", "Show or modify configuration"),
];

foreach (var command in subcommands)
{
    var name = command.Name;
    command.SetAction(_ =>
    {
        Console.WriteLine($"[{name}] Not yet implemented.");
    });

    rootCommand.Add(command);
}

var config = new CommandLineConfiguration(rootCommand);
return await config.InvokeAsync(args);
