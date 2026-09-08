using System;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    /// <summary>
    /// Base class for AS400 Custom Actions providing unified exception handling,
    /// error code translation, and automatic cleanup of faulted sessions.
    /// </summary>
    public abstract class ActionBaseAS400 : ActionBase
    {
        public const string CategoryName = "AS400Automation";

        /// <summary>
        /// Executes an action body within a resilient guard.
        /// If a connection failure or timeout occurs, ensures proper disconnection
        /// and converts the error into a descriptive ActionException for PAD.
        /// </summary>
        protected void ExecuteGuarded(Action action, string actionName, string sessionId = null)
        {
            try
            {
                action();
            }
            catch (AS400Exception as400Ex)
            {
                // Auto-cleanup faulted or timed out sessions
                if (!string.IsNullOrWhiteSpace(sessionId) &&
                    (as400Ex.ErrorCode == AS400ErrorCode.ConnectionTimeout ||
                     as400Ex.ErrorCode == AS400ErrorCode.OperationTimeout ||
                     as400Ex.ErrorCode == AS400ErrorCode.SessionFaulted ||
                     as400Ex.ErrorCode == AS400ErrorCode.ConnectionFailed))
                {
                    SessionRegistry.Instance.SafeDisconnectAndRemove(sessionId);
                }

                ActionException actionException = new ActionException(as400Ex.ErrorCode.ToString(), as400Ex.Message, as400Ex)
                {
                    RemediationSteps = as400Ex.RemediationSteps
                };
                throw actionException;
            }
            catch (Exception ex)
            {
                // Auto-cleanup on unexpected socket or system errors
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    SessionRegistry.Instance.SafeDisconnectAndRemove(sessionId);
                }

                ActionException actionException = new ActionException("GeneralError", $"[{actionName}] Unexpected error: {ex.Message}", ex)
                {
                    RemediationSteps = "Check AS400 host availability and action parameters."
                };
                throw actionException;
            }
        }
    }
}
