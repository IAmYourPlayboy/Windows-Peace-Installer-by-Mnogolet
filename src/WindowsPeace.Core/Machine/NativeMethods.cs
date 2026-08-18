using System.Runtime.InteropServices;

namespace WindowsPeace.Core.Machine;

/// <summary>
/// Объявления вызовов Windows для снимка среды. Память спрашивается напрямую
/// у ядра, а не через WMI: в WinPE управляемый код до WMI не достаёт —
/// проверено опытом, см. docs/superpowers/notes/2026-08-14-step-b-pe-experiments.md.
/// </summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryStatusEx
    {
        internal uint Length;
        internal uint MemoryLoad;
        internal ulong TotalPhys;
        internal ulong AvailPhys;
        internal ulong TotalPageFile;
        internal ulong AvailPageFile;
        internal ulong TotalVirtual;
        internal ulong AvailVirtual;
        internal ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
