using System.Runtime.InteropServices;
using AudioSwitcher.App.Interop;
using AudioSwitcher.Core;

namespace AudioSwitcher.App.Services;

/// <summary>Windows の出力デバイスを調べる・既定の出力先を切り替える。変化があれば Changed を UI のスレッドで出す。</summary>
public sealed class AudioService : IDisposable
{
    readonly IMMDeviceEnumerator _enumerator;
    readonly Notifier _notifier;
    readonly Dictionary<string, FormFactor> _kinds = new();

    /// <summary>デバイスがつながった・外れた・既定が変わったとき(少しまとめてから呼ぶ)。</summary>
    public event Action? Changed;

    public AudioService(System.Windows.Threading.Dispatcher dispatcher)
    {
        _enumerator = CoreAudioClasses.Create<IMMDeviceEnumerator>(CoreAudioClasses.MMDeviceEnumerator);
        _notifier = new Notifier(this, dispatcher);
        _enumerator.RegisterEndpointNotificationCallback(_notifier);
    }

    /// <summary>出力デバイスを全部(切断中・無効なども含めて)。</summary>
    public List<SystemDevice> GetDevices()
    {
        var list = new List<SystemDevice>();
        if (_enumerator.EnumAudioEndpoints(EDataFlow.Render, DeviceState.All, out var col) != 0) return list;
        col.GetCount(out var n);
        for (uint i = 0; i < n; i++)
        {
            if (col.Item(i, out var dev) != 0) continue;
            try
            {
                dev.GetId(out var id);
                dev.GetState(out var state);
                var (name, kind) = ReadProps(dev);
                _kinds[id] = kind;
                // 無効にしたデバイスは Windows の一覧からも消えるので、つながっていない扱い
                list.Add(new SystemDevice(id, name, (state & DeviceState.Active) != 0));
            }
            catch (Exception e) { Log.Write("デバイスを読めない: " + e.Message); }
            finally { Marshal.ReleaseComObject(dev); }
        }
        Marshal.ReleaseComObject(col);
        return list;
    }

    public FormFactor KindOf(string id) => _kinds.TryGetValue(id, out var k) ? k : FormFactor.Unknown;

    static (string Name, FormFactor Kind) ReadProps(IMMDevice dev)
    {
        if (dev.OpenPropertyStore(0 /* STGM_READ */, out var store) != 0) return ("", FormFactor.Unknown);
        try
        {
            string name = "";
            var key = CoreAudioClasses.FriendlyName;
            if (store.GetValue(ref key, out var v) == 0)
            {
                if (v.vt == 31 /* VT_LPWSTR */) name = Marshal.PtrToStringUni(v.pointer) ?? "";
                CoreAudioClasses.PropVariantClear(ref v);
            }
            var kind = FormFactor.Unknown;
            key = CoreAudioClasses.FormFactor;
            if (store.GetValue(ref key, out var f) == 0)
            {
                if (f.vt == 19 /* VT_UI4 */) kind = (FormFactor)f.uintValue;
                CoreAudioClasses.PropVariantClear(ref f);
            }
            return (name, kind);
        }
        finally { Marshal.ReleaseComObject(store); }
    }

    /// <summary>今の既定の出力先(なければ null)。</summary>
    public string? GetDefaultId()
    {
        if (_enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var dev) != 0) return null;
        try
        {
            dev.GetId(out var id);
            return id;
        }
        finally { Marshal.ReleaseComObject(dev); }
    }

    /// <summary>既定の出力先を変える。roles=All なら通話用も同じにする。</summary>
    public bool SetDefault(string id, SwitchRoles roles)
    {
        try
        {
            var policy = CoreAudioClasses.Create<IPolicyConfig>(CoreAudioClasses.PolicyConfigClient);
            try
            {
                int hr = policy.SetDefaultEndpoint(id, ERole.Console);
                hr |= policy.SetDefaultEndpoint(id, ERole.Multimedia);
                if (roles == SwitchRoles.All) hr |= policy.SetDefaultEndpoint(id, ERole.Communications);
                if (hr != 0) Log.Write($"切り替えに失敗: 0x{hr:X8}");
                return hr == 0;
            }
            finally { Marshal.ReleaseComObject(policy); }
        }
        catch (Exception e)
        {
            Log.Write("切り替えに失敗: " + e);
            return false;
        }
    }

    public void Dispose()
    {
        try { _enumerator.UnregisterEndpointNotificationCallback(_notifier); } catch { }
        Marshal.ReleaseComObject(_enumerator);
    }

    // Windows からの知らせは別のスレッドで来るので、UI のスレッドへ移し、短い間の連続はまとめる
    sealed class Notifier : IMMNotificationClient
    {
        readonly AudioService _owner;
        readonly System.Windows.Threading.DispatcherTimer _timer;

        public Notifier(AudioService owner, System.Windows.Threading.Dispatcher d)
        {
            _owner = owner;
            _timer = new System.Windows.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(250), System.Windows.Threading.DispatcherPriority.Normal, (_, _) => Fire(), d);
            _timer.Stop();
        }

        void Fire()
        {
            _timer.Stop();
            _owner.Changed?.Invoke();
        }

        void Poke() => _timer.Dispatcher.BeginInvoke(() =>
        {
            _timer.Stop();
            _timer.Start();
        });

        public void OnDeviceStateChanged(string deviceId, int newState) => Poke();
        public void OnDeviceAdded(string deviceId) => Poke();
        public void OnDeviceRemoved(string deviceId) => Poke();
        public void OnDefaultDeviceChanged(EDataFlow flow, ERole role, string? defaultDeviceId)
        {
            if (flow == EDataFlow.Render) Poke();
        }
        public void OnPropertyValueChanged(string deviceId, PropertyKey key)
        {
            // 名前が変わったときだけ
            if (key.fmtid == CoreAudioClasses.FriendlyName.fmtid && key.pid == CoreAudioClasses.FriendlyName.pid) Poke();
        }
    }
}
