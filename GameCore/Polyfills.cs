// Polyfills for C# 9–11 features when targeting netstandard2.1.
// These types exist in .NET 5+ but not in netstandard2.1.
// Declaring them here lets the Roslyn compiler emit the corresponding language features.

namespace System.Runtime.CompilerServices
{
    // Required for C# 9 record types and init-only properties (added in .NET 5).
    internal static class IsExternalInit { }

    // Required for C# 11 required members (added in .NET 7).
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct |
        AttributeTargets.Field | AttributeTargets.Property,
        Inherited = false, AllowMultiple = false)]
    internal sealed class RequiredMemberAttribute : Attribute { }

    // Required for C# 11 required members (added in .NET 7).
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct |
        AttributeTargets.Method | AttributeTargets.Constructor,
        Inherited = false, AllowMultiple = true)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }
        public string FeatureName { get; }
        public bool IsOptional { get; init; }
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    // Required for C# 11 required members on constructors (added in .NET 7).
    [AttributeUsage(AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute { }
}
