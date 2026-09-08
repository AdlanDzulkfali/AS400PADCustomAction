using System.ComponentModel;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_WaitForScreenReady",
        Category = CategoryName,
        FriendlyName = "Wait for AS400 Screen Ready",
        Description = "Waits until the AS400 host processing and keyboard-inhibit states clear and the terminal is receptive to input.")]
    [Throws("SessionNotFound", FriendlyName = "Session Not Found", Description = "The specified SessionId does not exist or was closed.")]
    [Throws("SessionFaulted", FriendlyName = "Session Faulted", Description = "The terminal session is disconnected.")]
    public class WaitForScreenReadyAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle from Connect AS400 Session.", Order = 1)]
        public string SessionId { get; set; }

        [InputArgument(FriendlyName = "Timeout (seconds)", Description = "Maximum time in seconds to wait for screen ready state (default 10).", Order = 2), DefaultValue(10)]
        public int TimeoutSeconds { get; set; } = 10;

        [OutputArgument(FriendlyName = "Is Ready", Description = "Returns true if the terminal screen is ready for user input.", Order = 1)]
        public bool IsReady { get; set; }

        public override void Execute(ActionContext context)
        {
            ExecuteGuarded(() =>
            {
                var driver = SessionRegistry.Instance.Get(SessionId);
                IsReady = driver.WaitForScreenReady(TimeoutSeconds);
            }, nameof(WaitForScreenReadyAction), SessionId);
        }
    }
}
