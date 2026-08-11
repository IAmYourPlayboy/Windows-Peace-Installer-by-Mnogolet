using System;
using System.Globalization;
using System.Management;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Чтение свойств WMI. Отсутствующее или неожиданного типа свойство —
/// обычное дело на чужом железе, поэтому здесь оно превращается
/// в значение по умолчанию, а не в исключение.
/// </summary>
internal static class WmiValue
{
    public static string? String(ManagementBaseObject source, string name)
    {
        var value = Read(source, name);
        return value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    public static ulong UInt64(ManagementBaseObject source, string name)
    {
        var value = Read(source, name);
        return value is null ? 0UL : Convert.ToUInt64(value, CultureInfo.InvariantCulture);
    }

    public static int Int32(ManagementBaseObject source, string name)
    {
        var value = Read(source, name);
        return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public static bool Boolean(ManagementBaseObject source, string name)
    {
        var value = Read(source, name);
        return value is not null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    public static char? Char(ManagementBaseObject source, string name)
    {
        var text = String(source, name);
        return string.IsNullOrWhiteSpace(text) || text![0] == '\0' ? null : text[0];
    }

    private static object? Read(ManagementBaseObject source, string name)
    {
        try
        {
            return source[name];
        }
        catch (ManagementException)
        {
            return null;
        }
    }
}
