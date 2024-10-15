using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Tapper;
using TypedSignalR.Client.TypeScript.TypeMappers;

namespace TypedSignalR.Client.TypeScript;

public class App : ConsoleAppBase
{
    private readonly ILogger<App> _logger;

    public App(ILogger<App> logger)
    {
        _logger = logger;
    }

    [RootCommand]
    public async Task Transpile(
        [Option("p", "Path to the project file (XXX.csproj)")]
        string project,
        [Option("o", "Output directory")]
        string output,
        [Option("eol", "lf / crlf / cr")]
        NewLineOption newLine = NewLineOption.Lf,
        [Option("asm", "Flag whether to extend the transpile target to the referenced assembly.")]
        bool assemblies = false,
        [Option("s", "JSON / MessagePack : The output type will be suitable for the selected serializer.")]
        SerializerOption serializer = SerializerOption.Json,
        [Option("n", "camelCase (default) / PascalCase / none (The name in C# is used as it is.)")]
        NamingStyle namingStyle = NamingStyle.CamelCase,
        [Option("en", "value (default) / name / nameCamel / NamePascal / union / unionCamel / UnionPascal")]
        EnumStyle @enum = EnumStyle.Value,
        [Option("m", "camelCase (default) / PascalCase / none (The name in C# is used as it is.)")]
        MethodStyle method = MethodStyle.CamelCase,
        [Option("attr", "The flag whether attributes such as JsonPropertyName should affect transpilation.")]
        bool attribute = true,
        [Option("rxjs", "The flag to generate an RxJS Receiver")]
        bool rxjs = false)
    {
        _logger.Log(LogLevel.Information, "Start loading the csproj of {path}.", Path.GetFullPath(project));

        output = Path.GetFullPath(output);

        try
        {
            var compilation = await this.CreateCompilationAsync(project);

            await TranspileCore(compilation, output, newLine, 4, assemblies, serializer, namingStyle, @enum, method, attribute, rxjs);

            _logger.Log(LogLevel.Information, "======== Transpilation is completed. ========");
            _logger.Log(LogLevel.Information, "Please check the output folder: {output}", output);
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Information, "======== Exception ========");
            _logger.Log(LogLevel.Error, "{ex}", ex);
        }
    }

    private async Task<Compilation> CreateCompilationAsync(string projectPath)
    {
        var logger = new ConsoleLogger(LoggerVerbosity.Quiet);
        using var workspace = MSBuildWorkspace.Create();

        var msBuildProject = await workspace.OpenProjectAsync(projectPath, logger, null, this.Context.CancellationToken);

        _logger.Log(LogLevel.Information, "Create Compilation...");
        var compilation = await msBuildProject.GetCompilationAsync(this.Context.CancellationToken);

        if (compilation is null)
        {
            throw new InvalidOperationException("Failed to create compilation.");
        }

        return compilation;
    }

    private async Task TranspileCore(
        Compilation compilation,
        string outputDir,
        NewLineOption newLine,
        int indent,
        bool referencedAssembliesTranspilation,
        SerializerOption serializerOption,
        NamingStyle namingStyle,
        EnumStyle enumStyle,
        MethodStyle methodStyle,
        bool enableAttributeReference,
        bool enableRxJSReceiverGeneration)
    {
        var typeMapperProvider = new DefaultTypeMapperProvider(compilation, referencedAssembliesTranspilation);

        typeMapperProvider.AddTypeMapper(new TaskTypeMapper(compilation));
        typeMapperProvider.AddTypeMapper(new GenericTaskTypeMapper(compilation));

        // By default, netstandard2.0 does not include IAsyncEnumerable<T> and ChannelReader<T>
        var asyncEnumerableTypeMapper = new AsyncEnumerableTypeMapper(compilation);

        if (asyncEnumerableTypeMapper.IsSupported())
        {
            typeMapperProvider.AddTypeMapper(asyncEnumerableTypeMapper);
        }

        var channelReaderTypeMapper = new ChannelReaderTypeMapper(compilation);

        if (channelReaderTypeMapper.IsSupported())
        {
            typeMapperProvider.AddTypeMapper(channelReaderTypeMapper);
        }

        var options = new TypedSignalRTranspilationOptions(
            compilation,
            typeMapperProvider,
            serializerOption,
            namingStyle,
            enumStyle,
            methodStyle,
            newLine,
            indent,
            referencedAssembliesTranspilation,
            enableRxJSReceiverGeneration,
            enableAttributeReference
        );

        // Tapper
        var transpiler = new Transpiler(compilation, options, _logger);

        var generatedSourceCodes = transpiler.Transpile();

        // TypedSignalR.Client.TypeScript
        var signalrCodeGenerator = new TypedSignalRCodeGenerator(compilation, options, _logger);

        var generatedSignalRSourceCodes = signalrCodeGenerator.Generate();

        await OutputToFiles(outputDir, generatedSourceCodes.Concat(generatedSignalRSourceCodes), newLine);
    }

    private async Task OutputToFiles(string outputDir, IEnumerable<GeneratedSourceCode> generatedSourceCodes, NewLineOption newLine)
    {
        if (Directory.Exists(outputDir))
        {
            var tsFiles = Directory.GetFiles(outputDir, "*.ts");

            _logger.Log(LogLevel.Information, "Cleanup old files...");

            foreach (var tsFile in tsFiles)
            {
                File.Delete(tsFile);
            }

            var signalrDir = Path.Join(outputDir, "TypedSignalR.Client");

            if (Directory.Exists(signalrDir))
            {
                var tsSignalRFiles = Directory.GetFiles(signalrDir, "*.ts");

                foreach (var tsFile in tsSignalRFiles)
                {
                    File.Delete(tsFile);
                }
            }
            else
            {
                Directory.CreateDirectory(Path.Join(outputDir, "TypedSignalR.Client"));
            }
        }
        else
        {
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(Path.Join(outputDir, "TypedSignalR.Client"));
        }

        var newLineString = newLine.ToNewLineString();

        foreach (var generatedSourceCode in generatedSourceCodes)
        {
            await using var fs = File.Create(Path.Join(outputDir, generatedSourceCode.SourceName));
            await fs.WriteAsync(Encoding.UTF8.GetBytes(generatedSourceCode.Content.NormalizeNewLines(newLineString)));
        }
    }
}
