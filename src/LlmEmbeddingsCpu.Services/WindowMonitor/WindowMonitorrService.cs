using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Automation;
using System.Threading;
using LlmEmbeddingsCpu.Core.Models;
namespace LlmEmbeddingsCpu.Services.WindowMonitor
{
    public class WindowMonitorrService : IDisposable
    {
        // Delegate for the hook procedure
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        // P/Invoke declarations
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // Constants for SetWinEventHook
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000; // Events are ASYNC
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003; // Event for foreground window change

        private IntPtr _hookHandle = IntPtr.Zero;
        private readonly WinEventDelegate _eventDelegate; // Keep a reference to prevent GC

        public event EventHandler<ActiveWindowLog>? ActiveWindowChanged;

        public WindowMonitorrService()
        {
            // Keep the delegate instance to prevent it from being garbage collected
            _eventDelegate = new WinEventDelegate(WinEventProc);
        }

        public void StartTracking()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                // Already started
                return;
            }

            // Hook for foreground window changes.
            // WINEVENT_OUTOFCONTEXT means the DLL is not mapped into the address space of the process that generates the event.
            // We listen to events from all processes (idProcess = 0) and all threads (idThread = 0).
            _hookHandle = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _eventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

            if (_hookHandle == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to set WinEventHook.");
            }

            // Optionally, trigger an event for the current window immediately
            // This requires running GetCurrentWindowInfo on the same thread that calls Start(),
            // or carefully managing thread context if you make WinEventProc more complex.
            // For simplicity here, we'll rely on the first window change event.
            // Or you could call a method like this:
            // TriggerInitialEvent();
        }

        public void StopTracking()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // Filter for EVENT_SYSTEM_FOREGROUND
            // idObject must be OBJID_WINDOW and idChild must be CHILDID_SELF for foreground changes.
            if (eventType == EVENT_SYSTEM_FOREGROUND && idObject == 0 && idChild == 0) // Simplified check, OBJID_WINDOW = 0
            {
                try
                {
                    var windowInfo = GetActiveWindowInfo(hwnd);
                    ActiveWindowChanged?.Invoke(this, windowInfo);
                }
                catch (Exception ex)
                {
                    // Log or handle exceptions during info gathering or event invocation
                    Console.Error.WriteLine($"Error in WinEventProc: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Gets information about the currently active foreground window.
        /// Can be called independently if needed.
        /// </summary>
        public static ActiveWindowLog GetCurrentActiveWindowInfo()
        {
             IntPtr currentHwnd = GetForegroundWindow();
             if (currentHwnd == IntPtr.Zero) return null;
             return GetActiveWindowInfo(currentHwnd);
        }


        private static ActiveWindowLog GetActiveWindowInfo(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;

            // 1. Log the current window that is used
            StringBuilder windowTitleBuilder = new StringBuilder(256);
            int result = GetWindowText(hWnd, windowTitleBuilder, windowTitleBuilder.Capacity);
            string windowTitle = result > 0 ? windowTitleBuilder.ToString() : string.Empty;

            uint processIdRaw;
            uint threadId = GetWindowThreadProcessId(hWnd, out processIdRaw);
            if (threadId == 0)
            {
                return null;
            }
            int processId = (int)processIdRaw;
            string processName = "N/A";
            try
            {
                Process process = Process.GetProcessById(processId);
                processName = process.ProcessName;
                process.Dispose(); // Dispose the Process object
            }
            catch (ArgumentException) // Process might have exited
            {
                // Process with this ID might not be running or accessible
            }
            catch (Exception ex) // Other potential exceptions
            {
                 Console.Error.WriteLine($"Error getting process info for PID {processId}: {ex.Message}");
            }

            return new ActiveWindowLog
            {
                WindowHandle = hWnd,
                WindowTitle = windowTitle,
                ProcessName = processName,
            };
        }

        public void Dispose()
        {
            StopTracking();
           
            GC.SuppressFinalize(this);
        }
    }
}