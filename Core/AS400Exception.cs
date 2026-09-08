using System;

namespace AS400PADCustomAction.Core
{
    /// <summary>
    /// Enumerates specific error categories for AS400 terminal automation operations.
    /// </summary>
    public enum AS400ErrorCode
    {
        GeneralError,
        ConnectionFailed,
        ConnectionTimeout,
        OperationTimeout,
        SessionNotFound,
        SessionFaulted,
        ProtocolError,
        InvalidCoordinate,
        KeystrokeError
    }

    /// <summary>
    /// Exception thrown when an AS400 terminal communication or protocol error occurs.
    /// </summary>
    [Serializable]
    public class AS400Exception : Exception
    {
        public AS400ErrorCode ErrorCode { get; }
        public string RemediationSteps { get; }

        public AS400Exception(AS400ErrorCode errorCode, string message, string remediationSteps = null, Exception innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            RemediationSteps = remediationSteps ?? GetDefaultRemediation(errorCode);
        }

        public AS400Exception(string message) : base(message)
        {
            ErrorCode = AS400ErrorCode.GeneralError;
            RemediationSteps = "Check terminal parameters and host availability.";
        }

        public AS400Exception(string message, Exception innerException) : base(message, innerException)
        {
            ErrorCode = AS400ErrorCode.GeneralError;
            RemediationSteps = "Check terminal parameters and host availability.";
        }

        private static string GetDefaultRemediation(AS400ErrorCode errorCode)
        {
            switch (errorCode)
            {
                case AS400ErrorCode.ConnectionFailed:
                    return "Verify that the AS400 host IP/DNS and Port (default 23) are reachable and accepting Telnet connections.";
                case AS400ErrorCode.ConnectionTimeout:
                    return "The connection or 5250 handshake timed out. Increase TimeoutSeconds or check network latency / firewall rules.";
                case AS400ErrorCode.OperationTimeout:
                    return "The terminal screen did not respond within the allocated timeout window. Verify terminal state and host responsiveness.";
                case AS400ErrorCode.SessionNotFound:
                    return "The specified SessionId does not exist or was already closed. Ensure Connect AS400 Session succeeded and is passing a valid SessionId.";
                case AS400ErrorCode.SessionFaulted:
                    return "The active session was abruptly terminated by the host or network. The session has been cleaned up. Reconnect using Connect AS400 Session.";
                case AS400ErrorCode.ProtocolError:
                    return "An unexpected 5250 Telnet stream packet was received from the host. Check terminal emulation settings.";
                case AS400ErrorCode.InvalidCoordinate:
                    return "Row must be between 1 and 24, Column between 1 and 80, and Length must not exceed presentation space boundaries.";
                case AS400ErrorCode.KeystrokeError:
                    return "Check keystroke string format. Valid mnemonics include @E (Enter), @C (Clear), @T (Tab), @1-@24 (PF1-PF24).";
                default:
                    return "Review error details and retry the action.";
            }
        }
    }
}
