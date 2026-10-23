using System;
using System.Runtime.InteropServices;

namespace WinTaskbar.Native
{
    public static class AudioHelper
    {
        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAudioEndpointVolume
        {
            [PreserveSig]
            int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
            [PreserveSig]
            int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
            int GetChannelCount(out uint pnChannelCount);
            int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            int GetMasterVolumeLevel(out float pfLevelDB);
            [PreserveSig]
            int GetMasterVolumeLevelScalar(out float pfLevel);
            int SetMute(bool bMute, ref Guid pguidEventContext);
            [PreserveSig]
            int GetMute(out bool pbMute);
        }

        [Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAudioEndpointVolumeCallback
        {
            [PreserveSig]
            int OnNotify(IntPtr pNotify);
        }

        private class AudioNotificationCallback : IAudioEndpointVolumeCallback
        {
            private readonly Action<float, bool> _callback;

            public AudioNotificationCallback(Action<float, bool> callback)
            {
                _callback = callback;
            }

            public int OnNotify(IntPtr pNotify)
            {
                try
                {
                    float vol = GetMasterVolume();
                    bool muted = IsMuted();
                    _callback?.Invoke(vol, muted);
                }
                catch { }
                return 0;
            }
        }

        private static IAudioEndpointVolume? _cachedVolumeEndpoint;
        private static AudioNotificationCallback? _cachedCallback;

        private static IAudioEndpointVolume? GetEndpoint()
        {
            if (_cachedVolumeEndpoint != null) return _cachedVolumeEndpoint;
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                if (enumerator.GetDefaultAudioEndpoint(0, 1, out var dev) == 0 && dev != null)
                {
                    var iid = typeof(IAudioEndpointVolume).GUID;
                    if (dev.Activate(ref iid, 1, IntPtr.Zero, out var epvObj) == 0 && epvObj is IAudioEndpointVolume epv)
                    {
                        _cachedVolumeEndpoint = epv;
                        return _cachedVolumeEndpoint;
                    }
                }
            }
            catch { }
            return null;
        }

        public static float GetMasterVolume()
        {
            try
            {
                var epv = GetEndpoint();
                if (epv != null && epv.GetMasterVolumeLevelScalar(out float level) == 0)
                {
                    return level;
                }
            }
            catch { }
            return 0.5f;
        }

        public static bool IsMuted()
        {
            try
            {
                var epv = GetEndpoint();
                if (epv != null && epv.GetMute(out bool muted) == 0)
                {
                    return muted;
                }
            }
            catch { }
            return false;
        }

        public static void RegisterNotification(Action<float, bool> onVolumeChanged)
        {
            try
            {
                var epv = GetEndpoint();
                if (epv != null)
                {
                    _cachedCallback = new AudioNotificationCallback(onVolumeChanged);
                    epv.RegisterControlChangeNotify(_cachedCallback);
                }
            }
            catch { }
        }
    }
}
