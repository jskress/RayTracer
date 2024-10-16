using CommandLine;
using Newtonsoft.Json;
using RayTracer.Commands;
using RayTracer.Options;

JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    Formatting = Formatting.Indented
};

Parser.Default.ParseArguments<RenderOptions>(args)
    .WithParsed(RenderCommand.Render);
