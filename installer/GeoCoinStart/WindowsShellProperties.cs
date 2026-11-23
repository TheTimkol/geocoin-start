using System.Runtime.InteropServices;

namespace GeoCoinStart;

internal static class WindowsShellProperties
{
    private static readonly Guid AppUserModelIdFormatId = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    private static readonly Guid PropertyStoreInterfaceId = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct PropertyVariant
    {
        [FieldOffset(0)] public ushort VariantType;
        [FieldOffset(8)] public IntPtr PointerValue;
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint propertyCount);
        [PreserveSig] int GetAt(uint propertyIndex, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropertyVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropertyVariant value);
        [PreserveSig] int Commit();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHGetPropertyStoreFromParsingName(
        string parsingName,
        IntPtr bindContext,
        uint flags,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropertyVariant propertyVariant);

    public static void SetShortcutAppUserModelId(string shortcutPath, string appUserModelId)
    {
        IPropertyStore? store = null;
        var key = new PropertyKey { FormatId = AppUserModelIdFormatId, PropertyId = 5 };
        var value = new PropertyVariant
        {
            VariantType = 31, // VT_LPWSTR
            PointerValue = Marshal.StringToCoTaskMemUni(appUserModelId)
        };
        try
        {
            Guid interfaceId = PropertyStoreInterfaceId;
            SHGetPropertyStoreFromParsingName(shortcutPath, IntPtr.Zero, 2, ref interfaceId, out store); // GPS_READWRITE
            Marshal.ThrowExceptionForHR(store.SetValue(ref key, ref value));
            Marshal.ThrowExceptionForHR(store.Commit());
        }
        finally
        {
            PropVariantClear(ref value);
            if (store is not null && Marshal.IsComObject(store)) Marshal.FinalReleaseComObject(store);
        }
    }

    public static void EnsureStartMenuShortcutAppUserModelId()
    {
        string shortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs", "GeoCoin-Start", "GeoCoin-Start.lnk");
        if (File.Exists(shortcutPath))
            SetShortcutAppUserModelId(shortcutPath, AppPaths.ToastAppUserModelId);
    }
}
