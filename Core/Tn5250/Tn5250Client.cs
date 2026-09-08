using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AS400PADCustomAction.Core.Tn5250
{
    /// <summary>
    /// High-performance, built-in direct TN5250 Telnet socket client (RFC 1205 / RFC 2877).
    /// Connects directly to IBM i / AS400 over standard TCP or SSL/TLS without external emulators.
    /// Features guaranteed teardown and resource cleanup on timeouts and connection failures.
    /// </summary>
    public class Tn5250Client : ITn5250Driver
    {
        public string SessionId { get; }
        public string Host { get; private set; }
        public int Port { get; private set; } = 23;
        public bool UseSsl { get; private set; }

        private TcpClient _tcpClient;
        private Stream _stream;
        private Thread _receiveThread;
        private CancellationTokenSource _cts;
        private readonly ManualResetEventSlim _screenUpdatedEvent = new ManualResetEventSlim(false);
        private readonly Tn5250ScreenBuffer _screenBuffer = new Tn5250ScreenBuffer();
        private readonly List<PendingFieldWrite> _pendingWrites = new List<PendingFieldWrite>();

        private class PendingFieldWrite
        {
            public int Row { get; set; }
            public int Col { get; set; }
            public string Text { get; set; }
        }

        private readonly object _stateLock = new object();
        private volatile bool _isConnected;
        private bool _disposed;

        public bool IsConnected
        {
            get
            {
                lock (_stateLock)
                {
                    return _isConnected && _tcpClient != null && _tcpClient.Connected;
                }
            }
        }

        public Tn5250ScreenBuffer ScreenBuffer => _screenBuffer;

        public event Action<string, int, int> ScreenUpdated;
        public event Action<string> ActionProgressChanged;

        public void NotifyActionProgress(string actionName, string details)
        {
            try
            {
                string msg = string.IsNullOrEmpty(details) ? actionName : $"{actionName}: {details}";
                ActionProgressChanged?.Invoke(msg);
            }
            catch { }
        }

        public void FireScreenUpdated()
        {
            try
            {
                if (ScreenUpdated != null)
                {
                    string text = _screenBuffer.ReadBox(1, 1, 24, 80);
                    ScreenUpdated.Invoke(text, _screenBuffer.CursorRow, _screenBuffer.CursorCol);
                }
            }
            catch { }
        }

        public Tn5250Client(string sessionId = null)
        {
            SessionId = !string.IsNullOrWhiteSpace(sessionId) ? sessionId : "AS400_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        /// <summary>
        /// Establishes direct TN5250 connection with strict timeout guards and optional SSL/TLS encryption.
        /// </summary>
        public void Connect(string host, int port, int timeoutSeconds, bool useSsl = false, bool acceptAnyCert = false)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new AS400Exception(AS400ErrorCode.ConnectionFailed, "AS400 Host cannot be null or empty.");
            if (port <= 0 || port > 65535)
                throw new AS400Exception(AS400ErrorCode.ConnectionFailed, $"Invalid port number '{port}'. Must be between 1 and 65535.");

            Host = host.Trim();
            Port = port;
            UseSsl = useSsl;
            int timeoutMs = Math.Max(1000, timeoutSeconds * 1000);

            lock (_stateLock)
            {
                if (IsConnected)
                {
                    Disconnect();
                }

                _cts = new CancellationTokenSource();
                _screenUpdatedEvent.Reset();
                _screenBuffer.Clear();

                try
                {
                    _tcpClient = new TcpClient
                    {
                        ReceiveTimeout = timeoutMs,
                        SendTimeout = timeoutMs,
                        NoDelay = true
                    };

                    // Guarded async connection with timeout
                    IAsyncResult ar = _tcpClient.BeginConnect(Host, Port, null, null);
                    using (WaitHandle wh = ar.AsyncWaitHandle)
                    {
                        if (!wh.WaitOne(timeoutMs, false))
                        {
                            SafeTeardown();
                            throw new AS400Exception(AS400ErrorCode.ConnectionTimeout,
                                $"Connection to AS400 host '{Host}:{Port}' timed out after {timeoutSeconds} seconds.");
                        }
                        _tcpClient.EndConnect(ar);
                    }

                    NetworkStream netStream = _tcpClient.GetStream();

                    if (useSsl)
                    {
                        RemoteCertificateValidationCallback certCallback = acceptAnyCert
                            ? (RemoteCertificateValidationCallback)((s, cert, chain, errors) => true)
                            : null;

                        SslStream sslStream = new SslStream(netStream, false, certCallback);
                        sslStream.AuthenticateAsClient(Host);
                        _stream = sslStream;
                    }
                    else
                    {
                        _stream = netStream;
                    }

                    _isConnected = true;

                    // Launch background receive thread
                    _receiveThread = new Thread(ReceiveWorker)
                    {
                        IsBackground = true,
                        Name = $"TN5250_Receiver_{SessionId}"
                    };
                    _receiveThread.Start();
                }
                catch (Exception ex)
                {
                    SafeTeardown();
                    if (ex is AS400Exception) throw;
                    throw new AS400Exception(AS400ErrorCode.ConnectionFailed,
                        $"Failed to establish connection to AS400 host '{Host}:{Port}': {ex.Message}", null, ex);
                }
            }

            // Wait for initial presentation space update after Telnet negotiation
            bool receivedInitialScreen = _screenUpdatedEvent.Wait(TimeSpan.FromSeconds(timeoutSeconds));
            if (!receivedInitialScreen)
            {
                lock (_stateLock)
                {
                    if (!IsConnected)
                    {
                        SafeTeardown();
                        throw new AS400Exception(AS400ErrorCode.ConnectionTimeout,
                            $"AS400 host '{Host}:{Port}' connection dropped during initial Telnet negotiation.");
                    }
                }
            }

            FireScreenUpdated();
        }

        public void Connect(string host, int port, int timeoutSeconds)
        {
            Connect(host, port, timeoutSeconds, false, false);
        }

        /// <summary>
        /// Safely tears down the connection, closing sockets and stopping threads.
        /// Thread-safe and idempotent.
        /// </summary>
        private void SafeTeardown()
        {
            lock (_stateLock)
            {
                _isConnected = false;

                try { _cts?.Cancel(); } catch { }
                try { _stream?.Close(); } catch { }
                try { _tcpClient?.Close(); } catch { }

                _stream = null;
                _tcpClient = null;
                _screenUpdatedEvent.Reset();
                _pendingWrites.Clear();
            }
        }

        /// <summary>
        /// Disconnects the active session and cleans up resources.
        /// </summary>
        public void Disconnect()
        {
            SafeTeardown();
        }

        /// <summary>
        /// Writes text into the presentation space at target coordinates or current cursor without submitting.
        /// </summary>
        public void WriteText(string text, int? row = null, int? col = null, int eraseLength = 0)
        {
            if (!IsConnected)
            {
                SafeTeardown();
                throw new AS400Exception(AS400ErrorCode.SessionFaulted,
                    $"Session '{SessionId}' is disconnected. Cannot write text.");
            }

            int targetRow = row ?? _screenBuffer.CursorRow;
            int targetCol = col ?? _screenBuffer.CursorCol;

            if (eraseLength > 0)
            {
                _screenBuffer.EraseAt(targetRow, targetCol, eraseLength);
            }

            if (!string.IsNullOrEmpty(text))
            {
                _screenBuffer.WriteAt(targetRow, targetCol, text);

                lock (_stateLock)
                {
                    string textToSend = text;
                    if (eraseLength > textToSend.Length)
                    {
                        textToSend = textToSend.PadRight(eraseLength, ' ');
                    }
                    _pendingWrites.Add(new PendingFieldWrite
                    {
                        Row = targetRow,
                        Col = targetCol,
                        Text = textToSend
                    });
                }
            }
            else if (row.HasValue && col.HasValue)
            {
                _screenBuffer.SetCursor(targetRow, targetCol);
            }

            FireScreenUpdated();
        }

        /// <summary>
        /// Sets terminal cursor to 1-based (row, col) coordinates.
        /// </summary>
        public void SetCursor(int row, int col)
        {
            if (!IsConnected)
            {
                SafeTeardown();
                throw new AS400Exception(AS400ErrorCode.SessionFaulted,
                    $"Session '{SessionId}' is disconnected. Cannot set cursor.");
            }

            _screenBuffer.SetCursor(row, col);
            FireScreenUpdated();
        }

        /// <summary>
        /// Retrieves the current cursor coordinates.
        /// </summary>
        public Tuple<int, int> GetCursor()
        {
            if (!IsConnected)
            {
                SafeTeardown();
                throw new AS400Exception(AS400ErrorCode.SessionFaulted,
                    $"Session '{SessionId}' is disconnected. Cannot get cursor.");
            }

            return _screenBuffer.GetCursor();
        }

        /// <summary>
        /// Searches for text across the presentation space.
        /// </summary>
        public bool FindText(string searchText, bool caseSensitive, out int foundRow, out int foundCol, int startRow = 1, int startCol = 1)
        {
            if (!IsConnected)
            {
                SafeTeardown();
                throw new AS400Exception(AS400ErrorCode.SessionFaulted,
                    $"Session '{SessionId}' is disconnected. Cannot search screen.");
            }

            return _screenBuffer.FindText(searchText, caseSensitive, out foundRow, out foundCol, startRow, startCol);
        }

        /// <summary>
        /// Dynamically waits until target text appears on the presentation space or timeout expires.
        /// </summary>
        public bool WaitForText(string textToWait, int timeoutSeconds, int? row = null, int? col = null, bool caseSensitive = false)
        {
            if (!IsConnected)
            {
                SafeTeardown();
                throw new AS400Exception(AS400ErrorCode.SessionFaulted,
                    $"Session '{SessionId}' is disconnected. Cannot wait for text.");
            }

            if (string.IsNullOrEmpty(textToWait)) return true;

            Stopwatch sw = Stopwatch.StartNew();
            TimeSpan maxWait = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));

            while (sw.Elapsed < maxWait)
            {
                if (!IsConnected)
                {
                    SafeTeardown();
                    throw new AS400Exception(AS400ErrorCode.SessionFaulted, $"Session '{SessionId}' connection dropped while waiting for text.");
                }

                if (row.HasValue && col.HasValue)
                {
                    string actual = _screenBuffer.ReadSlice(row.Value, col.Value, textToWait.Length);
                    StringComparison comp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    if (string.Equals(actual, textToWait, comp))
                    {
                        return true;
                    }
                }
                else
                {
                    if (_screenBuffer.FindText(textToWait, caseSensitive, out int _, out int _))
                    {
                        return true;
                    }
                }

                // Wait briefly for incoming 5250 packets or polling interval
                _screenUpdatedEvent.Wait(200);
            }

            return false;
        }

        /// <summary>
        /// Waits until the screen update stream quiesces and is receptive to input.
        /// </summary>
        public bool WaitForScreenReady(int timeoutSeconds)
        {
            if (!IsConnected) return false;
            return _screenUpdatedEvent.Wait(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds))) || IsConnected;
        }

        /// <summary>
        /// Background worker listening for Telnet and 5250 data streams.
        /// </summary>
        private void ReceiveWorker()
        {
            byte[] rawBuffer = new byte[8192];
            List<byte> recordBuffer = new List<byte>(4096);

            try
            {
                while (_cts != null && !_cts.IsCancellationRequested && IsConnected)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = _stream.Read(rawBuffer, 0, rawBuffer.Length);
                    }
                    catch (IOException) { break; }
                    catch (ObjectDisposedException) { break; }

                    if (bytesRead <= 0)
                    {
                        // Server closed connection
                        break;
                    }

                    int i = 0;
                    while (i < bytesRead)
                    {
                        byte b = rawBuffer[i++];

                        if (b == Tn5250Constants.IAC)
                        {
                            if (i >= bytesRead) break;
                            byte cmd = rawBuffer[i++];

                            switch (cmd)
                            {
                                case Tn5250Constants.IAC:
                                    recordBuffer.Add(Tn5250Constants.IAC);
                                    break;

                                case Tn5250Constants.DO:
                                    if (i < bytesRead)
                                    {
                                        byte opt = rawBuffer[i++];
                                        HandleTelnetDo(opt);
                                    }
                                    break;

                                case Tn5250Constants.DONT:
                                    if (i < bytesRead) i++;
                                    break;

                                case Tn5250Constants.WILL:
                                    if (i < bytesRead)
                                    {
                                        byte opt = rawBuffer[i++];
                                        HandleTelnetWill(opt);
                                    }
                                    break;

                                case Tn5250Constants.WONT:
                                    if (i < bytesRead) i++;
                                    break;

                                case Tn5250Constants.SB:
                                    List<byte> sbData = new List<byte>();
                                    while (i < bytesRead)
                                    {
                                        byte sbByte = rawBuffer[i++];
                                        if (sbByte == Tn5250Constants.IAC && i < bytesRead && rawBuffer[i] == Tn5250Constants.SE)
                                        {
                                            i++;
                                            break;
                                        }
                                        sbData.Add(sbByte);
                                    }
                                    HandleTelnetSubnegotiation(sbData.ToArray());
                                    break;

                                case Tn5250Constants.EOR:
                                    if (recordBuffer.Count > 0)
                                    {
                                        Process5250Record(recordBuffer.ToArray());
                                        recordBuffer.Clear();
                                    }
                                    _screenUpdatedEvent.Set();
                                    FireScreenUpdated();
                                    break;
                            }
                        }
                        else
                        {
                            recordBuffer.Add(b);
                        }
                    }
                }
            }
            catch
            {
                // Network failure or abort
            }
            finally
            {
                SafeTeardown();
            }
        }

        private void SendRaw(byte[] bytes)
        {
            lock (_stateLock)
            {
                if (!IsConnected || _stream == null) return;
                try
                {
                    _stream.Write(bytes, 0, bytes.Length);
                    _stream.Flush();
                }
                catch (Exception)
                {
                    SafeTeardown();
                    throw;
                }
            }
        }

        private void HandleTelnetDo(byte option)
        {
            switch (option)
            {
                case Tn5250Constants.OPT_TERMINAL_TYPE:
                case Tn5250Constants.OPT_END_OF_RECORD:
                case Tn5250Constants.OPT_TRANSMIT_BINARY:
                    SendRaw(new byte[] { Tn5250Constants.IAC, Tn5250Constants.WILL, option });
                    break;
                default:
                    SendRaw(new byte[] { Tn5250Constants.IAC, Tn5250Constants.WONT, option });
                    break;
            }
        }

        private void HandleTelnetWill(byte option)
        {
            switch (option)
            {
                case Tn5250Constants.OPT_END_OF_RECORD:
                case Tn5250Constants.OPT_TRANSMIT_BINARY:
                    SendRaw(new byte[] { Tn5250Constants.IAC, Tn5250Constants.DO, option });
                    break;
                default:
                    SendRaw(new byte[] { Tn5250Constants.IAC, Tn5250Constants.DONT, option });
                    break;
            }
        }

        private void HandleTelnetSubnegotiation(byte[] sbData)
        {
            if (sbData == null || sbData.Length < 2) return;

            byte option = sbData[0];
            byte qualifier = sbData[1];

            if (option == Tn5250Constants.OPT_TERMINAL_TYPE && qualifier == Tn5250Constants.QUAL_SEND)
            {
                byte[] modelBytes = Encoding.ASCII.GetBytes(Tn5250Constants.TERMINAL_MODEL_IBM3179);
                List<byte> response = new List<byte>
                {
                    Tn5250Constants.IAC,
                    Tn5250Constants.SB,
                    Tn5250Constants.OPT_TERMINAL_TYPE,
                    Tn5250Constants.QUAL_IS
                };
                response.AddRange(modelBytes);
                response.Add(Tn5250Constants.IAC);
                response.Add(Tn5250Constants.SE);

                SendRaw(response.ToArray());
            }
        }

        /// <summary>
        /// Parses an inbound 5250 Workstation Data record from the host and updates presentation space.
        /// </summary>
        private void Process5250Record(byte[] record)
        {
            if (record == null || record.Length == 0) return;

            int idx = 0;

            // Optional 5250 GDS Workstation Header (Length + 0x12A0 ...)
            if (record.Length >= 6 && record[2] == 0x12 && record[3] == 0xA0)
            {
                idx = 6;
                if (record.Length > idx + 3 && record[idx] == 0x04 && record[idx + 1] == 0x00)
                {
                    idx += 3;
                }
            }

            while (idx < record.Length)
            {
                byte b = record[idx++];

                if (b == Tn5250Constants.ESC)
                {
                    if (idx >= record.Length) break;
                    byte cmd = record[idx++];

                    switch (cmd)
                    {
                        case Tn5250Constants.CMD_CLEAR_UNIT:
                        case Tn5250Constants.CMD_CLEAR_UNIT_ALTERNATE:
                            _screenBuffer.Clear();
                            lock (_stateLock)
                            {
                                _pendingWrites.Clear();
                            }
                            break;

                        case Tn5250Constants.CMD_WRITE_TO_DISPLAY:
                        case Tn5250Constants.CMD_RESTORE_SCREEN:
                            if (idx + 1 < record.Length) idx += 2;
                            break;
                    }
                }
                else if (b == Tn5250Constants.ORDER_RA)
                {
                    if (idx + 2 < record.Length)
                    {
                        int targetRow = record[idx++];
                        int targetCol = record[idx++];
                        byte fillByte = record[idx++];
                        char fillChar = EbcdicCodec.ToChar(fillByte);
                        _screenBuffer.RepeatToAddress(targetRow, targetCol, fillChar);
                    }
                }
                else if (b == Tn5250Constants.ORDER_SBA)
                {
                    if (idx + 1 < record.Length)
                    {
                        int row = record[idx++];
                        int col = record[idx++];
                        _screenBuffer.SetCursor(row, col);
                    }
                }
                else if (b == Tn5250Constants.ORDER_IC)
                {
                    if (idx + 1 < record.Length)
                    {
                        int row = record[idx++];
                        int col = record[idx++];
                        _screenBuffer.SetCursor(row, col);
                    }
                }
                else if (b == Tn5250Constants.ORDER_SF)
                {
                    if (idx + 1 < record.Length)
                    {
                        idx += 2;
                    }
                    _screenBuffer.WriteCharAndAdvance(' ');
                }
                else if (b == Tn5250Constants.ORDER_SOH)
                {
                    if (idx < record.Length) idx++;
                }
                else
                {
                    char c = EbcdicCodec.ToChar(b);
                    _screenBuffer.WriteCharAndAdvance(c);
                }
            }
        }

        /// <summary>
        /// Transmits keystrokes and control mnemonics to the AS400 screen.
        /// </summary>
        public void SendKeys(string text, bool sendEnterKey, int waitSeconds)
        {
            if (!IsConnected)
            {
                SafeTeardown();
                throw new AS400Exception(AS400ErrorCode.SessionFaulted,
                    $"Session '{SessionId}' is not connected. Operation SendKeys aborted.");
            }

            try
            {
                _screenUpdatedEvent.Reset();

                byte aidCode = sendEnterKey ? Tn5250Constants.AID_ENTER : Tn5250Constants.AID_NO_AID;
                string processedText = text ?? string.Empty;

                // Parse mnemonics (e.g. @E for Enter, @1-@24 for F1-F24, @C for Clear)
                Match m = Regex.Match(processedText, @"(@[A-Z0-9]{1,2}|\[[A-Z0-9]+\])", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    string tag = m.Value.ToUpperInvariant();
                    switch (tag)
                    {
                        case "@E":
                        case "[ENTER]":
                            aidCode = Tn5250Constants.AID_ENTER;
                            break;
                        case "@C":
                        case "[CLEAR]":
                            aidCode = Tn5250Constants.AID_CLEAR;
                            break;
                        case "@H":
                        case "[HELP]":
                            aidCode = Tn5250Constants.AID_HELP;
                            break;
                        case "[PAGEUP]":
                        case "@U":
                            aidCode = Tn5250Constants.AID_PAGE_UP;
                            break;
                        case "[PAGEDOWN]":
                        case "@D":
                            aidCode = Tn5250Constants.AID_PAGE_DOWN;
                            break;
                        default:
                            if (Regex.IsMatch(tag, @"^(@|\[PF)([1-9]|1[0-9]|2[0-4])\]?$"))
                            {
                                string numStr = Regex.Match(tag, @"\d+").Value;
                                if (int.TryParse(numStr, out int pfNum))
                                {
                                    aidCode = GetPfKeyAid(pfNum);
                                }
                            }
                            break;
                    }
                    processedText = processedText.Replace(m.Value, string.Empty);
                }

                if (string.IsNullOrEmpty(processedText) && aidCode == Tn5250Constants.AID_NO_AID && sendEnterKey)
                {
                    aidCode = Tn5250Constants.AID_ENTER;
                }

                byte[] textBytes = EbcdicCodec.ToEbcdicBytes(processedText);

                byte[] wireBytes = BuildRFC1205InboundRecord(
                    aidCode != Tn5250Constants.AID_NO_AID ? aidCode : Tn5250Constants.AID_ENTER,
                    null,
                    textBytes.Length > 0 ? textBytes : null);

                SendRaw(wireBytes);

                // Wait for presentation space update or timeout
                int waitMs = Math.Max(500, waitSeconds * 1000);
                _screenUpdatedEvent.Wait(waitMs);
            }
            catch (Exception ex)
            {
                SafeTeardown();
                throw new AS400Exception(AS400ErrorCode.SessionFaulted,
                    $"Failed to send keystrokes to AS400 on session '{SessionId}'. Connection has been terminated: {ex.Message}", null, ex);
            }
        }

        private static byte GetPfKeyAid(int pfNumber)
        {
            if (pfNumber >= 1 && pfNumber <= 12)
            {
                return (byte)(Tn5250Constants.AID_PF1 + (pfNumber - 1));
            }
            if (pfNumber >= 13 && pfNumber <= 24)
            {
                return (byte)(Tn5250Constants.AID_PF13 + (pfNumber - 13));
            }
            return Tn5250Constants.AID_ENTER;
        }

        /// <summary>
        /// Constructs an RFC 1205 compliant 5250 Inbound Workstation Data Record.
        /// Fixed Header (6 bytes: Length, 0x12A0, 0x0000) + Variable Header (4 bytes: 0x04, Flags 0x0000, Opcode 0x00)
        /// + 5250 Inbound Stream (CursorRow, CursorCol, AID, [Field Data...]) + IAC EOR.
        /// </summary>
        private byte[] BuildRFC1205InboundRecord(byte aidCode, IEnumerable<PendingFieldWrite> fieldWrites = null, byte[] extraPayload = null)
        {
            List<byte> streamData = new List<byte>();

            // 5250 Inbound Workstation Data Stream:
            // Byte 10: Cursor Row (1-based)
            streamData.Add((byte)_screenBuffer.CursorRow);
            // Byte 11: Cursor Col (1-based)
            streamData.Add((byte)_screenBuffer.CursorCol);
            // Byte 12: Attention Identifier (AID) Code
            streamData.Add(aidCode);

            // Append pending field writes (SBA order 0x11, Row, Col, EBCDIC text)
            if (fieldWrites != null)
            {
                foreach (var write in fieldWrites)
                {
                    if (!string.IsNullOrEmpty(write.Text))
                    {
                        streamData.Add(Tn5250Constants.ORDER_SBA);
                        streamData.Add((byte)write.Row);
                        streamData.Add((byte)write.Col);
                        byte[] ebcdic = EbcdicCodec.ToEbcdicBytes(write.Text);
                        streamData.AddRange(ebcdic);
                    }
                }
            }

            if (extraPayload != null && extraPayload.Length > 0)
            {
                streamData.AddRange(extraPayload);
            }

            // 10-byte GDS Header:
            int logicalRecordLength = 10 + streamData.Count;
            List<byte> packet = new List<byte>(logicalRecordLength + 4);

            // 1. Logical Record Length (16 bits, big-endian)
            packet.Add((byte)((logicalRecordLength >> 8) & 0xFF));
            packet.Add((byte)(logicalRecordLength & 0xFF));

            // 2. SNA Record Type: '12A0'X (General Data Stream)
            packet.Add(0x12);
            packet.Add(0xA0);

            // 3. Reserved (16 bits)
            packet.Add(0x00);
            packet.Add(0x00);

            // 4. Variable Header Length: 4 octets
            packet.Add(0x04);

            // 5. Flags (16 bits): 0x0000
            packet.Add(0x00);
            packet.Add(0x00);

            // 6. Opcode (8 bits): 0x00 (No Operation)
            packet.Add(0x00);

            // 7. 5250 Inbound Data Stream
            packet.AddRange(streamData);

            // 8. Escape IAC (0xFF) in packet payload before appending <IAC><EOR>
            List<byte> wireBytes = new List<byte>(packet.Count + 4);
            for (int p = 0; p < packet.Count; p++)
            {
                byte b = packet[p];
                wireBytes.Add(b);
                if (b == Tn5250Constants.IAC)
                {
                    wireBytes.Add(Tn5250Constants.IAC); // Doubled IAC per RFC 854/1205
                }
            }

            // 9. Telnet End-Of-Record (RFC 885)
            wireBytes.Add(Tn5250Constants.IAC);
            wireBytes.Add(Tn5250Constants.EOR);

            return wireBytes.ToArray();
        }

        /// <summary>
        /// Transmits a function key or control key (Enter, Transmit, F1-F24, PageUp/Down, Clear, etc.) to the AS400
        /// along with any accumulated field text entered via WriteText.
        /// </summary>
        public void SendKey(AS400Key key, int waitSeconds)
        {
            if (!IsConnected)
            {
                SafeTeardown();
                throw new AS400Exception(AS400ErrorCode.SessionFaulted,
                    $"Session '{SessionId}' is not connected. Operation SendKey aborted.");
            }

            try
            {
                _screenUpdatedEvent.Reset();

                byte aidCode = MapKeyToAid(key);

                List<PendingFieldWrite> writesToSend;
                lock (_stateLock)
                {
                    writesToSend = new List<PendingFieldWrite>(_pendingWrites);
                    _pendingWrites.Clear();
                }

                byte[] packet = BuildRFC1205InboundRecord(aidCode, writesToSend);
                SendRaw(packet);

                // Wait for presentation space update or timeout
                int waitMs = Math.Max(500, waitSeconds * 1000);
                _screenUpdatedEvent.Wait(waitMs);
                FireScreenUpdated();
            }
            catch (Exception ex)
            {
                SafeTeardown();
                throw new AS400Exception(AS400ErrorCode.SessionFaulted,
                    $"Failed to send special key '{key}' to AS400 on session '{SessionId}'. Connection has been terminated: {ex.Message}", null, ex);
            }
        }

        /// <summary>
        /// Backward compatibility alias for SendKey.
        /// </summary>
        public void SendSpecialKey(AS400Key key, int waitSeconds) => SendKey(key, waitSeconds);

        /// <summary>
        /// Maps an AS400Key enum value to its corresponding 5250 Attention Identification (AID) byte.
        /// </summary>
        public static byte MapKeyToAid(AS400Key key)
        {
            switch (key)
            {
                case AS400Key.Enter:
                case AS400Key.Transmit:
                    return Tn5250Constants.AID_ENTER;
                case AS400Key.F1:
                case AS400Key.F2:
                case AS400Key.F3:
                case AS400Key.F4:
                case AS400Key.F5:
                case AS400Key.F6:
                case AS400Key.F7:
                case AS400Key.F8:
                case AS400Key.F9:
                case AS400Key.F10:
                case AS400Key.F11:
                case AS400Key.F12:
                    return (byte)(Tn5250Constants.AID_PF1 + (key - AS400Key.F1));
                case AS400Key.F13:
                case AS400Key.F14:
                case AS400Key.F15:
                case AS400Key.F16:
                case AS400Key.F17:
                case AS400Key.F18:
                case AS400Key.F19:
                case AS400Key.F20:
                case AS400Key.F21:
                case AS400Key.F22:
                case AS400Key.F23:
                case AS400Key.F24:
                    return (byte)(Tn5250Constants.AID_PF13 + (key - AS400Key.F13));
                case AS400Key.PageUp:
                    return Tn5250Constants.AID_PAGE_UP;
                case AS400Key.PageDown:
                    return Tn5250Constants.AID_PAGE_DOWN;
                case AS400Key.Clear:
                    return Tn5250Constants.AID_CLEAR;
                case AS400Key.Help:
                    return Tn5250Constants.AID_HELP;
                case AS400Key.Print:
                    return Tn5250Constants.AID_PRINT;
                case AS400Key.RecordBackspace:
                    return Tn5250Constants.AID_RECORD_BACKSPACE;
                default:
                    return Tn5250Constants.AID_ENTER;
            }
        }

        public string ReadScreen(int startRow, int startCol, int length)
        {
            if (!IsConnected)
            {
                SafeTeardown();
                throw new AS400Exception(AS400ErrorCode.SessionFaulted,
                    $"Session '{SessionId}' is disconnected. Cannot read screen.");
            }

            return _screenBuffer.ReadSlice(startRow, startCol, length);
        }

        public string ReadScreenBox(int startRow, int startCol, int endRow, int endCol)
        {
            if (!IsConnected)
            {
                SafeTeardown();
                throw new AS400Exception(AS400ErrorCode.SessionFaulted,
                    $"Session '{SessionId}' is disconnected. Cannot read screen.");
            }

            return _screenBuffer.ReadBox(startRow, startCol, endRow, endCol);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            SafeTeardown();

            if (disposing)
            {
                _screenUpdatedEvent?.Dispose();
                _cts?.Dispose();
            }
        }

        ~Tn5250Client()
        {
            Dispose(false);
        }
    }
}
