using System;
using System.Collections.Concurrent;

namespace AS400PADCustomAction.Core
{
    /// <summary>
    /// Thread-safe in-memory registry for active AS400 / TN5250 sessions.
    /// Provides safe lifecycle methods and guaranteed cleanup for timed out or faulted sessions.
    /// </summary>
    public class SessionRegistry
    {
        private static readonly Lazy<SessionRegistry> _instance =
            new Lazy<SessionRegistry>(() => new SessionRegistry());

        public static SessionRegistry Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, ITn5250Driver> _sessions =
            new ConcurrentDictionary<string, ITn5250Driver>(StringComparer.OrdinalIgnoreCase);

        private SessionRegistry() { }

        /// <summary>
        /// Registers an active driver session.
        /// </summary>
        public void Register(string sessionId, ITn5250Driver driver)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentNullException(nameof(sessionId));
            if (driver == null)
                throw new ArgumentNullException(nameof(driver));

            _sessions.AddOrUpdate(sessionId, driver, (key, existing) =>
            {
                // Disconnect existing before replacing
                try { existing.Disconnect(); existing.Dispose(); } catch { }
                return driver;
            });
        }

        /// <summary>
        /// Retrieves an active driver session. Throws AS400Exception if not found or faulted.
        /// </summary>
        public ITn5250Driver Get(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new AS400Exception(AS400ErrorCode.SessionNotFound,
                    "SessionId cannot be null or empty. Provide a valid session handle from Connect AS400 Session.");
            }

            if (!_sessions.TryGetValue(sessionId, out ITn5250Driver driver) || driver == null)
            {
                throw new AS400Exception(AS400ErrorCode.SessionNotFound,
                    $"AS400 session '{sessionId}' was not found in the active session registry.");
            }

            if (!driver.IsConnected)
            {
                // Session died or dropped connection; clean up
                SafeDisconnectAndRemove(sessionId);
                throw new AS400Exception(AS400ErrorCode.SessionFaulted,
                    $"AS400 session '{sessionId}' is no longer connected. Reconnect using Connect AS400 Session.");
            }

            return driver;
        }

        /// <summary>
        /// Safely disconnects, disposes, and unregisters the specified session.
        /// Idempotent and exception-safe.
        /// </summary>
        public bool SafeDisconnectAndRemove(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return false;

            if (_sessions.TryRemove(sessionId, out ITn5250Driver driver))
            {
                try
                {
                    driver?.Disconnect();
                }
                catch { }

                try
                {
                    driver?.Dispose();
                }
                catch { }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Closes and cleans up all active AS400 sessions.
        /// </summary>
        public void CloseAll()
        {
            foreach (var kvp in _sessions)
            {
                try
                {
                    kvp.Value?.Disconnect();
                    kvp.Value?.Dispose();
                }
                catch { }
            }
            _sessions.Clear();
        }
    }
}
