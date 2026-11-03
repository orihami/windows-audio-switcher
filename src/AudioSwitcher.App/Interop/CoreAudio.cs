using System.Runtime.InteropServices;

namespace AudioSwitcher.App.Interop;

// Windows の Core Audio(MMDevice API)。出力デバイスの一覧・接続状態・既定の出力先を扱う。
// 既定の出力先を変える IPolicyConfig は公開されていないが、Windows 7〜11 で同じ形で使われているもの。

public enum EDataFlow { Render = 0, Capture = 1, All = 2 }
public enum ERole { Console = 0, Multimedia = 1, Communications = 2 }

public static class DeviceState
{
    public const int Active = 0x1, Disabled = 0x2, NotPresent = 0x4, Unplugged = 0x8, All = 0xF;
}

[StructLayout(LayoutKind.Sequential)]
public struct PropertyKey
{
    public Guid fmtid;
    public int pid;
    public PropertyKey(Guid f, int p) { fmtid = f; pid = p; }
}

[StructLayout(LayoutKind.Explicit)]
public struct PropVariant
{
    [FieldOffset(0)] public ushort vt;
    [FieldOffset(8)] public IntPtr pointer;
    [FieldOffset(8)] public uint uintValue;
}

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IMMDeviceCollection devices);
    [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient client);
    [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceCollection
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int Item(uint index, out IMMDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    [PreserveSig] int OpenPropertyStore(int stgmAccess, out IPropertyStore properties);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetState(out int state);
}

[ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPropertyStore
{
    [PreserveSig] int GetCount(out int count);
    [PreserveSig] int GetAt(int index, out PropertyKey key);
    [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
    [PreserveSig] int Commit();
}

[ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMNotificationClient
{
    void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int newState);
    void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
    void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
    void OnDefaultDeviceChanged(EDataFlow flow, ERole role, [MarshalAs(UnmanagedType.LPWStr)] string? defaultDeviceId);
    void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PropertyKey key);
}

[ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat(IntPtr a, IntPtr b);
    [PreserveSig] int GetDeviceFormat(IntPtr a, int b, IntPtr c);
    [PreserveSig] int ResetDeviceFormat(IntPtr a);
    [PreserveSig] int SetDeviceFormat(IntPtr a, IntPtr b, IntPtr c);
    [PreserveSig] int GetProcessingPeriod(IntPtr a, int b, IntPtr c, IntPtr d);
    [PreserveSig] int SetProcessingPeriod(IntPtr a, IntPtr b);
    [PreserveSig] int GetShareMode(IntPtr a, IntPtr b);
    [PreserveSig] int SetShareMode(IntPtr a, IntPtr b);
    [PreserveSig] int GetPropertyValue(IntPtr a, IntPtr b, IntPtr c);
    [PreserveSig] int SetPropertyValue(IntPtr a, IntPtr b, IntPtr c);
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
    [PreserveSig] int SetEndpointVisibility(IntPtr a, int b);
}

public static class CoreAudioClasses
{
    public static readonly Guid MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    public static readonly Guid PolicyConfigClient = new("870af99c-171d-4f9e-af0d-e63df40c2bc9");

    public static readonly PropertyKey FriendlyName = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    public static readonly PropertyKey FormFactor = new(new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e"), 0);

    public static T Create<T>(Guid clsid) => (T)Activator.CreateInstance(Type.GetTypeFromCLSID(clsid, true)!)!;

    [DllImport("ole32.dll")]
    public static extern int PropVariantClear(ref PropVariant pvar);
}

/// <summary>出力デバイスの種類(アイコンを選ぶのに使う)。Windows の EndpointFormFactor と同じ番号。</summary>
public enum FormFactor
{
    RemoteNetwork = 0, Speakers = 1, LineLevel = 2, Headphones = 3, Microphone = 4, Headset = 5, Handset = 6,
    UnknownDigitalPassthrough = 7, SPDIF = 8, DigitalAudioDisplayDevice = 9, Unknown = 10,
}
