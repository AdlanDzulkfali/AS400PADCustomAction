using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Windows.Threading;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.UI
{
    /// <summary>
    /// Thread-safe manager for floating live terminal emulator windows.
    /// Manages STA thread lifecycle and wires live screen events to the WPF UI.
    /// </summary>
    public static class ViewerManager
    {
        private static readonly ConcurrentDictionary<string, WindowContext> ActiveViewers =
            new ConcurrentDictionary<string, WindowContext>(StringComparer.OrdinalIgnoreCase);

        private class WindowContext
        {
            public TerminalViewerWindow Window { get; set; }
            public Thread Thread { get; set; }
            public Dispatcher Dispatcher { get; set; }
        }

        /// <summary>
        /// Launches or brings to front the live terminal viewer for the specified session.
        /// Non-blocking, STA thread isolated.
        /// </summary>
        public static void ShowViewer(string sessionId, bool alwaysOnTop = true)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;

            ITn5250Driver driver;
            try
            {
                driver = SessionRegistry.Instance.Get(sessionId);
            }
            catch
            {
                return;
            }

            ActiveViewers.AddOrUpdate(sessionId,
                sid => CreateAndLaunchViewer(sid, driver, alwaysOnTop),
                (sid, existing) =>
                {
                    if (existing != null && existing.Dispatcher != null && !existing.Dispatcher.HasShutdownStarted)
                    {
                        try
                        {
                            existing.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (existing.Window != null)
                                {
                                    existing.Window.Topmost = alwaysOnTop;
                                    existing.Window.Show();
                                    existing.Window.Activate();
                                }
                            }));
                            return existing;
                        }
                        catch
                        {
                            // Stale context, relaunch
                        }
                    }
                    return CreateAndLaunchViewer(sid, driver, alwaysOnTop);
                });
        }

        private static WindowContext CreateAndLaunchViewer(string sessionId, ITn5250Driver driver, bool alwaysOnTop)
        {
            var initSignal = new ManualResetEventSlim(false);
            TerminalViewerWindow window = null;
            Dispatcher dispatcher = null;

            var thread = new Thread(() =>
            {
                try
                {
                    window = new TerminalViewerWindow(sessionId, alwaysOnTop);
                    dispatcher = window.Dispatcher;

                    // Event handlers
                    Action<string, int, int> onScreenUpdated = (screenText, r, c) =>
                    {
                        window?.UpdateScreen(screenText, r, c);
                    };

                    Action<string> onActionProgress = progress =>
                    {
                        window?.UpdateActionProgress(progress);
                    };

                    driver.ScreenUpdated += onScreenUpdated;
                    driver.ActionProgressChanged += onActionProgress;

                    // Populate initial screen state if session is already connected
                    try
                    {
                        if (driver.IsConnected)
                        {
                            string initialText = driver.ReadScreenBox(1, 1, 24, 80);
                            var cursor = driver.GetCursor();
                            window.UpdateScreen(initialText, cursor.Item1, cursor.Item2);
                            window.SetConnectionStatus(true);
                            window.UpdateActionProgress("Connected to AS400 host.");
                        }
                    }
                    catch
                    {
                        // Ignore presentation space read race conditions during startup
                    }

                    window.Closed += (s, e) =>
                    {
                        try
                        {
                            driver.ScreenUpdated -= onScreenUpdated;
                            driver.ActionProgressChanged -= onActionProgress;
                        }
                        catch { }

                        ActiveViewers.TryRemove(sessionId, out _);
                        dispatcher?.InvokeShutdown();
                    };

                    window.Show();
                    initSignal.Set();

                    Dispatcher.Run();
                }
                catch
                {
                    initSignal.Set();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            // Wait up to 2 seconds for STA window initialization
            initSignal.Wait(2000);

            return new WindowContext
            {
                Window = window,
                Thread = thread,
                Dispatcher = dispatcher
            };
        }

        /// <summary>
        /// Hides the live viewer window if open.
        /// </summary>
        public static void HideViewer(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;

            if (ActiveViewers.TryGetValue(sessionId, out var ctx) && ctx?.Dispatcher != null && !ctx.Dispatcher.HasShutdownStarted)
            {
                try
                {
                    ctx.Dispatcher.BeginInvoke(new Action(() => ctx.Window?.Hide()));
                }
                catch { }
            }
        }

        /// <summary>
        /// Closes and detaches the live viewer window for the specified session.
        /// </summary>
        public static void CloseViewer(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;

            if (ActiveViewers.TryRemove(sessionId, out var ctx) && ctx?.Dispatcher != null && !ctx.Dispatcher.HasShutdownStarted)
            {
                try
                {
                    ctx.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            ctx.Window?.Close();
                        }
                        catch { }
                    }));
                }
                catch { }
            }
        }
    }
}
