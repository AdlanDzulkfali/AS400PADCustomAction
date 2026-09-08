using System.ComponentModel;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_ReadAllScreenText",
        Category = CategoryName,
        FriendlyName = "Read All Screen Text",
        Description = "Reads all text from the current 24x80 AS400 screen.")]
    [Throws("SessionNotFound", FriendlyName = "Session Not Found", Description = "The specified SessionId does not exist or was closed.")]
    [Throws("SessionFaulted", FriendlyName = "Session Faulted", Description = "The terminal session is disconnected.")]
    public class ReadAllScreenTextAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle from Connect AS400 Session.", Order = 1)]
        public string SessionId { get; set; }

        [InputArgument(FriendlyName = "Preserve Line Breaks", Description = "If true, formats the 24 rows with line breaks. If false, returns a continuous single string (default true).", Order = 2), DefaultValue(true)]
        public bool PreserveLineBreaks { get; set; } = true;

        [OutputArgument(FriendlyName = "Screen Text", Description = "All text content extracted from the current AS400 screen.", Order = 1)]
        public string ScreenText { get; set; }

        public override void Execute(ActionContext context)
        {
            ExecuteGuarded(() =>
            {
                var driver = SessionRegistry.Instance.Get(SessionId);
                if (PreserveLineBreaks)
                {
                    ScreenText = driver.ReadScreenBox(1, 1, 24, 80);
                }
                else
                {
                    ScreenText = driver.ReadScreen(1, 1, 1920);
                }
            }, nameof(ReadAllScreenTextAction), SessionId);
        }
    }
}
