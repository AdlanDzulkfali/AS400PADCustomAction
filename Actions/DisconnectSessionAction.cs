using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_Disconnect",
        Category = CategoryName,
        FriendlyName = "Disconnect AS400 Session",
        Description = "Cleanly terminates the specified AS400 terminal session, closes TCP socket streams, and unregisters the session handle.")]
    public class DisconnectSessionAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle to disconnect.", Order = 1)]
        public string SessionId { get; set; }

        [OutputArgument(FriendlyName = "Success", Description = "Returns true if the session was successfully disconnected and cleaned up.", Order = 1)]
        public bool Success { get; set; }

        public override void Execute(ActionContext context)
        {
            ExecuteGuarded(() =>
            {
                if (!string.IsNullOrWhiteSpace(SessionId))
                {
                    UI.ViewerManager.CloseViewer(SessionId);
                    SessionRegistry.Instance.SafeDisconnectAndRemove(SessionId);
                }
                Success = true;
            }, nameof(DisconnectSessionAction), SessionId);
        }
    }
}
