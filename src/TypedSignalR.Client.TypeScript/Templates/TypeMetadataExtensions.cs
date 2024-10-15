using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TypedSignalR.Client.TypeScript.CodeAnalysis;

namespace TypedSignalR.Client.TypeScript.Templates;
public static class TypeMetadataExtensions
{
    internal static string FormatInterfaceNameAsConcrete(this TypeMetadata typeMetadata)
    {
        if (typeMetadata.Name.StartsWith("I"))
        {
            return typeMetadata.Name[1..];
        }
        return typeMetadata.Name;
    }
}
