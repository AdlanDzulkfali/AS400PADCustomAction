using System.ComponentModel;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_SetCursor",
        Category = CategoryName,
        FriendlyName = "Set AS400 Cursor Position",
        Description = "Positions the terminal cursor at specific 1-based (Row, Column) coordinates.")]
    [Throws("SessionNotFound", FriendlyName = "Session Not Found", Description = "The specified SessionId does not exist or was closed.")]
    [Throws("SessionFaulted", FriendlyName = "Session Faulted", Description = "The terminal session is disconnected.")]
    [Throws("InvalidCoordinate", FriendlyName = "Invalid Coordinates", Description = "Row must be between 1 and 24, and Column between 1 and 80.")]
    public class SetCursorAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle from Connect AS400 Session.", Order = 1)]
        public string SessionId { get; set; }

        [InputArgument(FriendlyName = "Row", Description = "1-based target row coordinate (1 to 24).", Order = 2), DefaultValue(1)]
        public int Row { get; set; } = 1;

        [InputArgument(FriendlyName = "Column", Description = "1-based target column coordinate (1 to 80).", Order = 3), DefaultValue(1)]
        public int Column { get; set; } = 1;

        [OutputArgument(FriendlyName = "Success", Description = "Returns true if cursor was successfully positioned.", Order = 1)]
        public bool Success { get; set; }

        public override void Execute(ActionContext context)
        {
            ExecuteGuarded(() =>
            {
                var driver = SessionRegistry.Instance.Get(SessionId);
                driver.SetCursor(Row, Column);
                Success = true;
            }, nameof(SetCursorAction), SessionId);
        }
    }
}
