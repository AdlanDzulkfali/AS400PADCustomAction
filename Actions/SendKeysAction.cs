using System.ComponentModel;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_SendKeys",
        Category = CategoryName,
        FriendlyName = "Send Keys to AS400",
        Description = "Transmits text and mnemonic control keys (Enter, PF1-PF24, Clear, PageUp/Down) to an active AS400 terminal session.")]
    [Throws("SessionNotFound", FriendlyName = "Session Not Found", Description = "The specified SessionId does not exist or was closed.")]
    [Throws("SessionFaulted", FriendlyName = "Session Faulted", Description = "The terminal session lost connection during transmission.")]
    [Throws("OperationTimeout", FriendlyName = "Operation Timeout", Description = "The terminal presentation space did not respond within the timeout window.")]
    public class SendKeysAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle from Connect AS400 Session.", Order = 1)]
        public string SessionId { get; set; }

        [InputArgument(FriendlyName = "Text to Send", Description = "Text or mnemonics to send (e.g., 'MYUSER[TAB]MYPASS@E', '@3' for F3). Optional if only pressing Enter or Function keys.", Order = 2)]
        public string TextToSend { get; set; }

        [InputArgument(FriendlyName = "Send Enter Key", Description = "If true, automatically transmits Enter AID key after text (default true).", Order = 3), DefaultValue(true)]
        public bool SendEnterKey { get; set; } = true;

        [InputArgument(FriendlyName = "Wait (seconds)", Description = "Duration in seconds to wait after sending keys for the screen to refresh (default 1).", Order = 4), DefaultValue(1)]
        public int WaitSeconds { get; set; } = 1;

        [OutputArgument(FriendlyName = "Success", Description = "Returns true if keystrokes were successfully transmitted to the AS400.", Order = 1)]
        public bool Success { get; set; }

        public override void Execute(ActionContext context)
        {
            ExecuteGuarded(() =>
            {
                var driver = SessionRegistry.Instance.Get(SessionId);
                driver.SendKeys(TextToSend, SendEnterKey, WaitSeconds);
                Success = true;
            }, nameof(SendKeysAction), SessionId);
        }
    }
}
