using System;

namespace AS400PADCustomAction.Core
{
    /// <summary>
    /// Contract for the native TN5250 terminal driver.
    /// </summary>
    public interface ITn5250Driver : IDisposable
    {
        /// <summary>
        /// Gets the unique session identifier handle.
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// Gets the AS400 host name or IP address.
        /// </summary>
        string Host { get; }

        /// <summary>
        /// Gets the terminal port (default 23, or 992 for SSL).
        /// </summary>
        int Port { get; }

        /// <summary>
        /// Gets whether the terminal connection is active and presentation space is ready.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Establishes a direct TN5250 connection to the AS400 host and negotiates terminal parameters.
        /// Supports optional SSL/TLS encryption.
        /// Guaranteed teardown of socket and threads if connection or handshake fails/times out.
        /// </summary>
        void Connect(string host, int port, int timeoutSeconds, bool useSsl = false, bool acceptAnyCert = false);

        /// <summary>
        /// Cleanly terminates the terminal session, closes network streams, and frees all resources.
        /// Idempotent.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Writes text into the presentation space at coordinates without submitting the screen.
        /// </summary>
        void WriteText(string text, int? row = null, int? col = null, int eraseLength = 0);

        /// <summary>
        /// Transmits keystrokes and control mnemonics to the AS400 screen.
        /// </summary>
        void SendKeys(string text, bool sendEnterKey, int waitSeconds);

        /// <summary>
        /// Reads a contiguous string of characters from the 24x80 presentation space.
        /// </summary>
        string ReadScreen(int startRow, int startCol, int length);

        /// <summary>
        /// Reads a rectangular region from the 24x80 presentation space.
        /// </summary>
        string ReadScreenBox(int startRow, int startCol, int endRow, int endCol);

        /// <summary>
        /// Sets the terminal cursor to 1-based (row, col) coordinates.
        /// </summary>
        void SetCursor(int row, int col);

        /// <summary>
        /// Retrieves the current 1-based cursor coordinates (Row, Col).
        /// </summary>
        Tuple<int, int> GetCursor();

        /// <summary>
        /// Searches for text across the 24x80 presentation space starting from (startRow, startCol).
        /// </summary>
        bool FindText(string searchText, bool caseSensitive, out int foundRow, out int foundCol, int startRow = 1, int startCol = 1);

        /// <summary>
        /// Waits dynamically until target text appears on screen or timeout expires.
        /// </summary>
        bool WaitForText(string textToWait, int timeoutSeconds, int? row = null, int? col = null, bool caseSensitive = false);

        /// <summary>
        /// Waits until the screen update stream quiesces and is ready for input.
        /// </summary>
        bool WaitForScreenReady(int timeoutSeconds);
    }
}
