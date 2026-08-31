#if UNITY_STANDALONE_WIN && !UNITY_EDITOR_WIN
#define VSMC_NDOF_WINDOWS_RAWINPUT_ACTIVE
#endif

using System;
using UnityEngine;

#if VSMC_NDOF_WINDOWS_RAWINPUT_ACTIVE
using System.Runtime.InteropServices;
using B83.Win32;
#endif

namespace VSMC.NDOF
{
    //Raw Input against the "Generic Desktop / Multi-axis Controller" HID usage - same device class
    //Blender's GHOST_SystemWin32 targets - instead of the proprietary 3Dconnexion SDK.
    //
    //Unity doesn't expose WM_INPUT, so this reuses the same trick the project's existing file drag & drop
    //support (UnityDragAndDropHook, Assets/External/UnityWindowsFileDrag-Drop) already relies on: a
    //WH_GETMESSAGE hook via SetWindowsHookEx on the main thread's message queue, rather than touching
    //Unity's own WndProc. Same reason that feature is standalone-build-only, this is too
    //(UNITY_STANDALONE_WIN && !UNITY_EDITOR_WIN) - the Editor's Play window doesn't pump messages the
    //same way. Everywhere else IsAvailable just comes back false.
    //
    //No RIDEV_INPUTSINK on registration, so WM_INPUT only arrives while the window has focus - that's
    //what makes losing focus/alt-tab stop motion, no extra bookkeeping needed. Report parsing follows the
    //unofficial but widely-used 3Dconnexion convention (report ID 1 = translation, or all six axes
    //combined on some models; ID 2 = rotation; ID 3 = button bitmask; little-endian Int16 fields) rather
    //than a full HidP_* preparsed-data parse, matching what most open-source SpaceMouse integrations do.
    public sealed class WindowsRawInputNDOFDevice : INDOFDevice
    {
        public bool IsAvailable { get; private set; }

        //Unused for now - buttons were out of scope for this pass - kept for a later feature.
        public uint LastButtonMask { get; private set; }

        //For one-off manual verification against real hardware; not meant to stay on.
        public bool LogRawReports = false;

        const float StaleTimeoutSeconds = 0.2f;

        public WindowsRawInputNDOFDevice()
        {
#if VSMC_NDOF_WINDOWS_RAWINPUT_ACTIVE
            IsAvailable = TryInitialize();
#else
            IsAvailable = false;
#endif
        }

        public void Poll()
        {
            //Input arrives via the hook as messages are pumped - nothing to do here. Kept so a future
            //backend that does need explicit polling (e.g. reading an evdev fd on Linux) has a place for it.
        }

        public bool TryGetLatestSample(out NDOFRawSample sample)
        {
#if VSMC_NDOF_WINDOWS_RAWINPUT_ACTIVE
            if (!IsAvailable)
            {
                sample = default;
                return false;
            }

            float now = Time.realtimeSinceStartup;
            sample = new NDOFRawSample
            {
                Tx = tx,
                Ty = ty,
                Tz = tz,
                TranslationFresh = (now - translationTimestamp) <= StaleTimeoutSeconds,
                Rx = rx,
                Ry = ry,
                Rz = rz,
                RotationFresh = (now - rotationTimestamp) <= StaleTimeoutSeconds,
            };
            return true;
#else
            sample = default;
            return false;
#endif
        }

        public void Dispose()
        {
#if VSMC_NDOF_WINDOWS_RAWINPUT_ACTIVE
            Shutdown();
#endif
        }

#if VSMC_NDOF_WINDOWS_RAWINPUT_ACTIVE

        //Written from the hook callback - main thread, synchronous with the message pump, so no locking needed.
        float tx, ty, tz;
        float translationTimestamp = float.NegativeInfinity;
        float rx, ry, rz;
        float rotationTimestamp = float.NegativeInfinity;

        IntPtr hookHandle = IntPtr.Zero;
        IntPtr targetWindow = IntPtr.Zero;

        const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        const ushort HID_USAGE_GENERIC_MULTI_AXIS_CONTROLLER = 0x08;
        const uint RIDEV_REMOVE = 0x00000001;
        const uint RID_INPUT = 0x10000003;
        const uint RIM_TYPEHID = 2;

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        bool TryInitialize()
        {
            targetWindow = FindMainWindow();
            if (targetWindow == IntPtr.Zero)
            {
                Debug.LogWarning("[NDOF] Could not locate the main window handle - SpaceMouse support disabled.");
                return false;
            }

            RAWINPUTDEVICE device = new RAWINPUTDEVICE
            {
                usUsagePage = HID_USAGE_PAGE_GENERIC,
                usUsage = HID_USAGE_GENERIC_MULTI_AXIS_CONTROLLER,
                //No RIDEV_INPUTSINK - losing focus should stop input arriving at all, not need extra handling.
                dwFlags = 0,
                hwndTarget = targetWindow
            };

            if (!RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            {
                Debug.LogWarning("[NDOF] RegisterRawInputDevices failed - SpaceMouse support disabled.");
                return false;
            }

            uint threadId = WinAPI.GetCurrentThreadId();
            IntPtr hModule = WinAPI.GetModuleHandle(null);
            hookHandle = WinAPI.SetWindowsHookEx(HookType.WH_GETMESSAGE, HookCallback, hModule, threadId);
            if (hookHandle == IntPtr.Zero)
            {
                Debug.LogWarning("[NDOF] SetWindowsHookEx failed - SpaceMouse support disabled.");
                UnregisterRawInput();
                return false;
            }

            instance = this;
            return true;
        }

        void Shutdown()
        {
            if (hookHandle != IntPtr.Zero)
            {
                WinAPI.UnhookWindowsHookEx(hookHandle);
                hookHandle = IntPtr.Zero;
            }
            UnregisterRawInput();
            if (instance == this) instance = null;
        }

        void UnregisterRawInput()
        {
            if (targetWindow == IntPtr.Zero) return;
            RAWINPUTDEVICE device = new RAWINPUTDEVICE
            {
                usUsagePage = HID_USAGE_PAGE_GENERIC,
                usUsage = HID_USAGE_GENERIC_MULTI_AXIS_CONTROLLER,
                dwFlags = RIDEV_REMOVE,
                hwndTarget = IntPtr.Zero
            };
            RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        }

        //Only written during TryInitialize on the main thread, so a static scratch field is safe.
        static IntPtr foundWindowScratch;

        static IntPtr FindMainWindow()
        {
            IntPtr handle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (handle != IntPtr.Zero) return handle;

            //Static callback, not a lambda - IL2CPP can't marshal instance/closure delegates as native callbacks.
            foundWindowScratch = IntPtr.Zero;
            uint threadId = WinAPI.GetCurrentThreadId();
            Window.EnumThreadWindows(threadId, EnumWindowCallback, IntPtr.Zero);
            return foundWindowScratch;
        }

        [AOT.MonoPInvokeCallback(typeof(EnumThreadDelegate))]
        static bool EnumWindowCallback(IntPtr hwnd, IntPtr lParam)
        {
            if (Window.IsWindowVisible(hwnd))
            {
                foundWindowScratch = hwnd;
                return false;
            }
            return true;
        }

        [AOT.MonoPInvokeCallback(typeof(HookProc))]
        static IntPtr HookCallback(int code, IntPtr wParam, ref MSG lParam)
        {
            if (code == 0 && lParam.message == WM.INPUT && instance != null)
            {
                instance.HandleRawInput(lParam.lParam);
            }
            return WinAPI.CallNextHookEx(instance?.hookHandle ?? IntPtr.Zero, code, wParam, ref lParam);
        }

        //Same IL2CPP constraint as above - the hook callback is static, so it needs a way back to instance
        //state. NDOFManager only ever creates one of these, so a single static reference is fine.
        static WindowsRawInputNDOFDevice instance;

        void HandleRawInput(IntPtr hRawInput)
        {
            uint headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
            uint size = 0;
            GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref size, headerSize);
            if (size == 0) return;

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                uint written = GetRawInputData(hRawInput, RID_INPUT, buffer, ref size, headerSize);
                if (written == unchecked((uint)-1)) return;

                RAWINPUTHEADER header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
                if (header.dwType != RIM_TYPEHID) return;

                //RAWHID immediately follows RAWINPUTHEADER: { uint dwSizeHid; uint dwCount; } then dwCount * dwSizeHid raw bytes.
                IntPtr hidPtr = IntPtr.Add(buffer, (int)headerSize);
                int dwSizeHid = Marshal.ReadInt32(hidPtr);
                if (dwSizeHid <= 0 || dwSizeHid > 64) return; //Sanity bound - real SpaceMouse reports are well under this.

                IntPtr reportPtr = IntPtr.Add(hidPtr, 8);
                byte[] report = new byte[dwSizeHid];
                Marshal.Copy(reportPtr, report, 0, dwSizeHid);

                ParseReport(report);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        void ParseReport(byte[] report)
        {
            if (LogRawReports)
            {
                Debug.Log("[NDOF] Raw report: " + BitConverter.ToString(report));
            }

            if (report.Length < 1) return;
            byte reportId = report[0];
            float now = Time.realtimeSinceStartup;

            //Some compact/enterprise SpaceMouse models combine all six axes into a single report ID 1.
            if (reportId == 1 && report.Length >= 13)
            {
                tx = ReadAxis(report, 1);
                ty = ReadAxis(report, 3);
                tz = ReadAxis(report, 5);
                translationTimestamp = now;
                rx = ReadAxis(report, 7);
                ry = ReadAxis(report, 9);
                rz = ReadAxis(report, 11);
                rotationTimestamp = now;
            }
            else if (reportId == 1 && report.Length >= 7)
            {
                tx = ReadAxis(report, 1);
                ty = ReadAxis(report, 3);
                tz = ReadAxis(report, 5);
                translationTimestamp = now;
            }
            else if (reportId == 2 && report.Length >= 7)
            {
                rx = ReadAxis(report, 1);
                ry = ReadAxis(report, 3);
                rz = ReadAxis(report, 5);
                rotationTimestamp = now;
            }
            else if (reportId == 3 && report.Length >= 2)
            {
                //Button reports vary in length by model; take up to 4 bytes as a bitmask.
                uint mask = 0;
                for (int i = 1; i < report.Length && i <= 4; i++)
                {
                    mask |= (uint)report[i] << ((i - 1) * 8);
                }
                LastButtonMask = mask;
            }
        }

        static short ReadAxis(byte[] report, int offset)
        {
            return (short)(report[offset] | (report[offset + 1] << 8));
        }

#endif
    }
}
