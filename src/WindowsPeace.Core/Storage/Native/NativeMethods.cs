using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WindowsPeace.Core.Storage.Native;

/// <summary>
/// Объявления вызовов Windows. Отдельным файлом, потому что это не логика,
/// а перевод заголовочных файлов на C#: смотреть сюда придётся редко,
/// а мешать чтению рядом стоящего кода оно не должно.
/// </summary>
internal static class NativeMethods
{
    internal const uint FileShareRead = 0x00000001;
    internal const uint FileShareWrite = 0x00000002;
    internal const uint OpenExisting = 3;

    internal const int ErrorInsufficientBuffer = 122;
    internal const int ErrorMoreData = 234;
    internal const int ErrorNoMoreFiles = 18;

    /// <summary>Не показывать человеку системные окна об ошибках устройств.</summary>
    internal const uint SemFailCriticalErrors = 0x0001;

    // Коды управления устройствами. Собраны из winioctl.h; значения постоянны.
    internal const uint IoctlStorageQueryProperty = 0x002D1400;
    internal const uint IoctlDiskGetDriveGeometryEx = 0x000700A0;
    internal const uint IoctlDiskGetDriveLayoutEx = 0x00070050;
    internal const uint IoctlDiskGetDiskAttributes = 0x000700F0;
    internal const uint IoctlVolumeGetVolumeDiskExtents = 0x00560000;

    internal const int StorageDeviceProperty = 0;
    internal const int StorageDeviceSeekPenaltyProperty = 7;
    internal const int PropertyStandardQuery = 0;

    internal const ulong DiskAttributeOffline = 0x0000000000000001;
    internal const ulong DiskAttributeReadOnly = 0x0000000000000002;

    /// <summary>Бит 62 в свойствах раздела GPT: раздел скрыт от проводника.</summary>
    internal const ulong GptBasicDataAttributeHidden = 0x4000000000000000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        byte[]? inBuffer,
        int inBufferSize,
        byte[]? outBuffer,
        int outBufferSize,
        out int bytesReturned,
        IntPtr overlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int QueryDosDeviceW(string? deviceName, char[] targetPath, int max);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr FindFirstVolumeW(StringBuilder volumeName, int bufferLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FindNextVolumeW(IntPtr findVolume, StringBuilder volumeName, int bufferLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FindVolumeClose(IntPtr findVolume);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetVolumePathNamesForVolumeNameW(
        string volumeName,
        char[] buffer,
        int bufferLength,
        out int returnLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetVolumeInformationW(
        string rootPathName,
        StringBuilder volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        int fileSystemNameSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetDiskFreeSpaceExW(
        string directoryName,
        out ulong freeBytesAvailableToCaller,
        out ulong totalNumberOfBytes,
        out ulong totalNumberOfFreeBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetThreadErrorMode(uint newMode, out uint oldMode);
}
