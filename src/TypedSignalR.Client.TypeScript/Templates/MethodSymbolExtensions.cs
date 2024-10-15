using System.Linq;
using Microsoft.CodeAnalysis;
using Tapper.TypeMappers;

namespace TypedSignalR.Client.TypeScript.Templates;

internal static class MethodSymbolExtensions
{
    public static string TranslateReceiverMethodIntoRxJSSubjectSyntax(this IMethodSymbol receiverMethodSymbol, SpecialSymbols specialSymbols, ITypedSignalRTranspilationOptions options)
    {
        if (receiverMethodSymbol.Parameters.Length == 0)
        {
            return "new Subject$<void>()";
        }

        if (receiverMethodSymbol.Parameters.Length == 1
            // Ignore if the last parameter of a receiver's method is a CancellationToken.
            && SymbolEqualityComparer.Default.Equals(receiverMethodSymbol.Parameters[0].Type, specialSymbols.CancellationTokenSymbol))
        {
            return "new Subject$<void>()";
        }

        return $"new Subject$<{{{ParametersToTypeScriptString(receiverMethodSymbol, specialSymbols, options)}}}>()";
    }

    public static string TranslateReceiverMethodIntoRxJSConnectionNextSyntax(this IMethodSymbol receiverMethodSymbol, SpecialSymbols specialSymbols, ITypedSignalRTranspilationOptions options)
    {
        if (receiverMethodSymbol.Parameters.Length == 0)
        {
            return $"() => __{receiverMethodSymbol.Name.Format(options.NamingStyle)}$.next()";
        }

        if (receiverMethodSymbol.Parameters.Length == 1
            // Ignore if the last parameter of a receiver's method is a CancellationToken.
            && SymbolEqualityComparer.Default.Equals(receiverMethodSymbol.Parameters[0].Type, specialSymbols.CancellationTokenSymbol))
        {
            return $"() => __{receiverMethodSymbol.Name.Format(options.NamingStyle)}$.next()";
        }

        return $"({ParametersToTypeScriptString(receiverMethodSymbol, specialSymbols, options)}) => __{receiverMethodSymbol.Name.Format(options.NamingStyle)}$.next({{{ParametersToTypeScriptArgumentString(receiverMethodSymbol, specialSymbols, options, false)}}})";
    }

    public static string TranslateReceiverMethodIntoLambdaExpressionSyntax(this IMethodSymbol receiverMethodSymbol, SpecialSymbols specialSymbols, ITypedSignalRTranspilationOptions options)
    {
        if (receiverMethodSymbol.Parameters.Length == 0)
        {
            return $"() => receiver.{receiverMethodSymbol.Name.Format(options.MethodStyle)}()";
        }

        if (receiverMethodSymbol.Parameters.Length == 1
            // Ignore if the last parameter of a receiver's method is a CancellationToken.
            && SymbolEqualityComparer.Default.Equals(receiverMethodSymbol.Parameters[0].Type, specialSymbols.CancellationTokenSymbol))
        {
            return $"() => receiver.{receiverMethodSymbol.Name.Format(options.MethodStyle)}()";
        }

        var parameters = ParametersToTypeArray(receiverMethodSymbol, specialSymbols, options);

        return $"(...args: {parameters}) => receiver.{receiverMethodSymbol.Name.Format(options.MethodStyle)}(...args)";

        static string ParametersToTypeArray(IMethodSymbol methodSymbol, SpecialSymbols specialSymbols, ITypedSignalRTranspilationOptions options)
        {
            var methodParameters = SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters.Last().Type, specialSymbols.CancellationTokenSymbol)
                ? methodSymbol.Parameters.SkipLast(1) // Ignore if the last parameter of a receiver's method is a CancellationToken.
                : methodSymbol.Parameters;

            var parameters = methodParameters.Select(x => TypeMapper.MapTo(x.Type, options));

            return $"[{string.Join(", ", parameters)}]";
        }
    }

    public static string CreateMethodString(this IMethodSymbol methodSymbol, SpecialSymbols specialSymbols, ITypedSignalRTranspilationOptions options)
    {
        var methodType = methodSymbol.SelectHubMethodType(specialSymbols);
        return methodType switch
        {
            HubMethodType.Unary => CreateUnaryMethodString(methodSymbol, specialSymbols, options),
            HubMethodType.ServerToClientStreaming => CreateServerToClientStreamingMethodString(methodSymbol, specialSymbols, options),
            HubMethodType.ClientToServerStreaming => CreateClientToServerStreamingMethodString(methodSymbol, specialSymbols, options),
            _ => string.Empty
        };
    }

    private static string ParametersToTypeScriptString(this IMethodSymbol methodSymbol, SpecialSymbols specialSymbols, ITypedSignalRTranspilationOptions options)
    {
        var parameters = methodSymbol.Parameters
            .Where(x => !SymbolEqualityComparer.Default.Equals(x.Type, specialSymbols.CancellationTokenSymbol))
            .Select(x => $"{x.Name}: {TypeMapper.MapTo(x.Type, options)}");

        return string.Join(", ", parameters);
    }

    private static string ParametersToTypeScriptArgumentString(this IMethodSymbol methodSymbol, SpecialSymbols specialSymbols, ITypedSignalRTranspilationOptions options, bool includeStartingComma = true)
    {
        var args = methodSymbol.Parameters
            .Where(x => !SymbolEqualityComparer.Default.Equals(x.Type, specialSymbols.CancellationTokenSymbol))
            .ToArray();

        if (!includeStartingComma)
        {
            return args.Length != 0
            ? string.Join(", ", args.Select(x => x.Name))
            : string.Empty;
        }

        return args.Length != 0
            ? $", {string.Join(", ", args.Select(x => x.Name))}"
            : string.Empty;
    }

    private static string ReturnTypeToTypeScriptString(this IMethodSymbol methodSymbol, SpecialSymbols specialSymbols, ITypedSignalRTranspilationOptions options)
    {
        var returnType = methodSymbol.ReturnType;
        // for sever-to-client streaming
        // IAsyncEnumerable<T>
        if (SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, specialSymbols.AsyncEnumerableSymbol))
        {
            var typeArg = (returnType as INamedTypeSymbol)!.TypeArguments[0];

            return $"IStreamResult<{TypeMapper.MapTo(typeArg, options)}>";
        }

        // Task<IAsyncEnumerable<T>>, Task<ChannelReader<T>>
        if (SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, specialSymbols.GenericTaskSymbol))
        {
            var typeArg = (returnType as INamedTypeSymbol)!.TypeArguments[0];

            if (SymbolEqualityComparer.Default.Equals(typeArg.OriginalDefinition, specialSymbols.AsyncEnumerableSymbol)
                || SymbolEqualityComparer.Default.Equals(typeArg.OriginalDefinition, specialSymbols.ChannelReaderSymbol))
            {
                var typeArg2 = (typeArg as INamedTypeSymbol)!.TypeArguments[0];

                return $"IStreamResult<{TypeMapper.MapTo(typeArg2, options)}>";
            }
        }

        return TypeMapper.MapTo(methodSymbol.ReturnType, options);
    }

    private static string CreateUnaryMethodString(IMethodSymbol methodSymbol, SpecialSymbols specialSymbols, ITypedSignalRTranspilationOptions options)
    {
        var name = methodSymbol.Name.Format(options.MethodStyle);
        var parameters = methodSymbol.ParametersToTypeScriptString(specialSymbols, options);
        var returnType = methodSymbol.ReturnTypeToTypeScriptString(specialSymbols, options);
        var args = methodSymbol.ParametersToTypeScriptArgumentString(specialSymbols, options);

        return $@"
    public readonly {name} = async ({parameters}): {returnType} => {{
        return await this.connection.invoke(""{methodSymbol.Name}""{args});
    }}";
    }

    private static string CreateServerToClientStreamingMethodString(IMethodSymbol methodSymbol, SpecialSymbols specialSymbols, ITypedSignalRTranspilationOptions options)
    {
        var name = methodSymbol.Name.Format(options.MethodStyle);
        var parameters = methodSymbol.ParametersToTypeScriptString(specialSymbols, options);
        var returnType = methodSymbol.ReturnTypeToTypeScriptString(specialSymbols, options);
        var args = methodSymbol.ParametersToTypeScriptArgumentString(specialSymbols, options);

        return $@"
    public readonly {name} = ({parameters}): {returnType} => {{
        return this.connection.stream(""{methodSymbol.Name}""{args});
    }}";
    }

    private static string CreateClientToServerStreamingMethodString(IMethodSymbol methodSymbol, SpecialSymbols specialSymbols, ITypedSignalRTranspilationOptions options)
    {
        var name = methodSymbol.Name.Format(options.MethodStyle);
        var parameters = methodSymbol.ParametersToTypeScriptString(specialSymbols, options);
        var returnType = methodSymbol.ReturnTypeToTypeScriptString(specialSymbols, options);
        var args = methodSymbol.ParametersToTypeScriptArgumentString(specialSymbols, options);

        return $@"
    public readonly {name} = async ({parameters}): {returnType} => {{
        return await this.connection.send(""{methodSymbol.Name}""{args});
    }}";
    }
}
