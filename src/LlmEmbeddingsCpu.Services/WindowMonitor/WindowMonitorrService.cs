using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.WindowMonitorStorage;
using Microsoft.Extensions.Logging;

namespace LlmEmbeddingsCpu.Services.WindowMonitor
{
    /// <summary>
    /// Monitors for changes in the active foreground window and logs the window information.
    /// </summary>
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

        private readonly WindowMonitorStorageService _windowMonitorStorageService;
        private readonly ILogger<WindowMonitorrService> _logger;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="WindowMonitorrService"/> class.
        /// </summary>
        public WindowMonitorrService(
            ILogger<WindowMonitorrService> logger,
            WindowMonitorStorageService windowMonitorStorageService)
        {
            _logger = logger;
            // Keep the delegate instance to prevent it from being garbage collected
            _eventDelegate = new WinEventDelegate(WinEventProc);
            _windowMonitorStorageService = windowMonitorStorageService;
        }

        /// <summary>
        /// Starts monitoring for active window changes.
        /// </summary>
        public void StartTracking()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                // Already started
                return;
            }

            _hookHandle = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _eventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

            if (_hookHandle == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to set WinEventHook.");
            }
            _logger.LogInformation("Window tracking started...");
        }

        /// <summary>
        /// Stops monitoring for active window changes.
        /// </summary>
        public void StopTracking()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
            _logger.LogInformation("Window tracking stopped.");
        }

        /// <summary>
        /// The callback method for the window event hook.
        /// </summary>
        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // Filter for EVENT_SYSTEM_FOREGROUND
            if (eventType == EVENT_SYSTEM_FOREGROUND && idObject == 0 && idChild == 0)
            {
                try
                {
                    var windowInfo = GetActiveWindowInfo(hwnd, _logger);

                    Task.Run(async () =>
                    {
                        try
                        {
                            if (windowInfo != null)
                            {
                                await _windowMonitorStorageService.SaveLogAsync(windowInfo);
                                _logger.LogDebug("Successfully saved log for: {WindowTitle}", windowInfo.WindowTitle);
                            }
                        }
                        catch (Exception storageEx)
                        {
                            _logger.LogError("Error during storage operation: {ErrorMessage}", storageEx.Message);
                            _logger.LogError("Error during storage operation: {ErrorMessage}", storageEx.StackTrace);
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error in WinEventProc: {ErrorMessage}", ex.Message);
                    _logger.LogError("Error in WinEventProc: {ErrorMessage}", ex.StackTrace);
                }
            }
        }

        /// <summary>
        /// Gets information about a specific window handle.
        /// </summary>
        /// <param name="hWnd">The handle of the window.</param>
        /// <returns>An <see cref="ActiveWindowLog"/> containing information about the window.</returns>
        private static ActiveWindowLog? GetActiveWindowInfo(IntPtr hWnd, ILogger logger)
        {
            if (hWnd == IntPtr.Zero) return null;

            // 1. Log the current window that is used
            StringBuilder windowTitleBuilder = new StringBuilder(256);
            int result = GetWindowText(hWnd, windowTitleBuilder, windowTitleBuilder.Capacity);
            string windowTitle = result > 0 ? windowTitleBuilder.ToString() : string.Empty;

            uint threadId = GetWindowThreadProcessId(hWnd, out uint processIdRaw);
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
                process.Dispose();
            }
            catch (ArgumentException)
            {
                logger.LogError("Process with this ID might not be running or accessible");
            }
            catch (Exception ex)
            {
                logger.LogError("Error getting process info for PID {ProcessId}: {ErrorMessage}", processId, ex.Message);
            }

            return new ActiveWindowLog
            {
                WindowHandle = hWnd,
                WindowTitle = windowTitle,
                ProcessName = processName,
            };
        }

        /// <summary>
        /// Disposes the window monitor and stops tracking.
        /// </summary>
        public void Dispose()
        {
            StopTracking();
           
            GC.SuppressFinalize(this);
        }
    }
}