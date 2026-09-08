using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_GetCursor",
        Category = CategoryName,
        FriendlyName = "Get AS400 Cursor Position",
        Description = "Retrieves the current 1-based (Row, Column) coordinates of the terminal cursor.")]
    [Throws("SessionNotFound", FriendlyName = "Session Not Found", Description = "The specified SessionId does not exist or was closed.")]
    [Throws("SessionFaulted", FriendlyName = "Session Faulted", Description = "The terminal session is disconnected.")]
    public class GetCursorAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle from Connect AS400 Session.", Order = 1)]
        public string SessionId { get; set; }

        [OutputArgument(FriendlyName = "Row", Description = "Current 1-based cursor row coordinate (1 to 24).", Order = 1)]
        public int Row { get; set; }

        [OutputArgument(FriendlyName = "Column", Description = "Current 1-based cursor column coordinate (1 to 80).", Order = 2)]
        public int Column { get; set; }

        public override void Execute(ActionContext context)
        {
            ExecuteGuarded(() =>
            {
                var driver = SessionRegistry.Instance.Get(SessionId);
                var cursor = driver.GetCursor();
                Row = cursor.Item1;
                Column = cursor.Item2;
            }, nameof(GetCursorAction), SessionId);
        }
    }
}
