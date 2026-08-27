using CommandLine;
using Newtonsoft.Json;
using RayTracer.Commands;
using RayTracer.Options;

JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    Formatting = Formatting.Indented
};

Parser.Default.ParseArguments<RenderOptions, FontsOptions, LibrariesOptions>(args)
    .WithParsed<RenderOptions>(RenderCommand.Render)
    .WithParsed<FontsOptions>(FontsCommand.ManageFonts)
    .WithParsed<LibrariesOptions>(LibrariesCommand.ManageLibraries)
    // A command line that was not understood -- a verb misspelled, a file that is not there -- is
    // reported by the parser itself and then leaves us with nothing to do.  Saying so in the exit
    // code as well is what lets a script tell "it ran" from "it did not", which the message alone
    // cannot do.  Asking for help or for the version is not a failure, and both arrive here.
    .WithNotParsed(errors =>
    {
        if (!errors.Any(error => error is HelpRequestedError or HelpVerbRequestedError or
                VersionRequestedError))
            Environment.ExitCode = 1;
    });
