using Microsoft.CodeAnalysis;
using Tapper;

namespace TypedSignalR.Client.TypeScript;

public class TypedSignalRTranspilationOptions : TranspilationOptions, ITypedSignalRTranspilationOptions
{
    public MethodStyle MethodStyle { get; }
    public bool GenerateRxJSReceiver { get; }

    public TypedSignalRTranspilationOptions(Compilation compilation,
        ITypeMapperProvider typeMapperProvider,
        SerializerOption serializerOption,
        NamingStyle namingStyle,
        EnumStyle enumStyle,
        MethodStyle methodStyle,
        NewLineOption newLineOption,
        int indent,
        bool referencedAssembliesTranspilation,
        bool generateRxJSReceiver,
        bool enableAttributeReference) : base(
            compilation,
            typeMapperProvider,
            serializerOption,
            namingStyle,
            enumStyle,
            newLineOption,
            indent,
            referencedAssembliesTranspilation,
            enableAttributeReference)
    {
        MethodStyle = methodStyle;
        GenerateRxJSReceiver = generateRxJSReceiver;
    }
}
