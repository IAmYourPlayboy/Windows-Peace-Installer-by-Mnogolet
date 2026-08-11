#if NETFRAMEWORK
using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Нужен компилятору для свойств с init-сеттером. В .NET Framework отсутствует,
    /// поэтому объявляется здесь. Под .NET 8 берётся из среды выполнения.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
#endif
