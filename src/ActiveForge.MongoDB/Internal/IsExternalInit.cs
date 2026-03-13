// Polyfill: 'init' accessors require this type, which only exists in .NET 5+.
// Declaring it here makes it available for netstandard2.0, netstandard2.1, and net472 targets.
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
