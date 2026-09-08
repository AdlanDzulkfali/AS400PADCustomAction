using System.ComponentModel;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;
using AS400PADCustomAction.UI;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_ShowViewer",
        Category = CategoryName,
        FriendlyName = "Show AS400 Terminal Viewer",
        Description = "Opens, closes, or toggles the floating real-time AS400 live terminal viewer window for an active session.")]
    [Throws("SessionNotFound", FriendlyName = "Session Not Found", Description = "The specified SessionId does not exist or was closed.")]
    [Throws("SessionFaulted", FriendlyName = "Session Faulted", Description = "The terminal session is disconnected.")]
    public class ShowViewerAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle from Connect AS400 Session.", Order = 1)]
        public string SessionId { get; set; }

        [InputArgument(FriendlyName = "Show Viewer", Description = "If true, opens/focuses the live viewer. If false, closes it (default true).", Order = 2), DefaultValue(true)]
        public bool Show { get; set; } = true;

        [InputArgument(FriendlyName = "Always on Top", Description = "Pins the live viewer window above other desktop windows (default true).", Order = 3), DefaultValue(true)]
        public bool AlwaysOnTop { get; set; } = true;

        [OutputArgument(FriendlyName = "Success", Description = "Returns true if the viewer operation succeeded.", Order = 1)]
        public bool Success { get; set; }

        public override void Execute(ActionContext context)
        {
            ExecuteGuarded(() =>
            {
                var driver = SessionRegistry.Instance.Get(SessionId);

                if (Show)
                {
                    ViewerManager.ShowViewer(SessionId, AlwaysOnTop);
                    driver.NotifyActionProgress("Terminal Viewer", "Viewer window opened.");
                }
                else
                {
                    ViewerManager.CloseViewer(SessionId);
                }

                Success = true;
            }, nameof(ShowViewerAction), SessionId);
        }
    }
}
