// Polyfills for C# 9–10 features when targeting netstandard2.1.
// These types exist in .NET 5+ but not in netstandard2.1.
// Declaring them here lets the Roslyn compiler emit the corresponding language features.

namespace System.Runtime.CompilerServices
{
    // Required for C# 9 record types and init-only properties (added in .NET 5).
    internal static class IsExternalInit { }
}
