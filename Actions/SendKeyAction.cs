using System.ComponentModel;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_SendKey",
        Category = CategoryName,
        FriendlyName = "Send Key to AS400",
        Description = "Transmits an AS400 function or control key (Enter, F1-F24, PageUp/Down, Clear, Help, Print) to an active terminal session from a drop-down menu.")]
    [Throws("SessionNotFound", FriendlyName = "Session Not Found", Description = "The specified SessionId does not exist or was closed.")]
    [Throws("SessionFaulted", FriendlyName = "Session Faulted", Description = "The terminal session lost connection during transmission.")]
    [Throws("OperationTimeout", FriendlyName = "Operation Timeout", Description = "The terminal presentation space did not respond within the timeout window.")]
    public class SendKeyAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle from Connect AS400 Session.", Order = 1)]
        public string SessionId { get; set; }

        [InputArgument(FriendlyName = "Key to Send", Description = "Select the AS400 function or control key to send from the drop-down menu.", Order = 2)]
        [DefaultValue(AS400Key.Enter)]
        public AS400Key Key { get; set; } = AS400Key.Enter;

        [InputArgument(FriendlyName = "Wait (seconds)", Description = "Duration in seconds to wait after sending key for the screen to refresh (default 2).", Order = 3), DefaultValue(2)]
        public int WaitSeconds { get; set; } = 2;

        [OutputArgument(FriendlyName = "Success", Description = "Returns true if the key was successfully transmitted to the AS400.", Order = 1)]
        public bool Success { get; set; }

        public override void Execute(ActionContext context)
        {
            ExecuteGuarded(() =>
            {
                var driver = SessionRegistry.Instance.Get(SessionId);
                driver.SendKey(Key, WaitSeconds);
                Success = true;
                driver.NotifyActionProgress("SendKey", $"Key: {Key}");
            }, nameof(SendKeyAction), SessionId);
        }
    }
}
