using System.ComponentModel;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_FindText",
        Category = CategoryName,
        FriendlyName = "Find Text on AS400 Screen",
        Description = "Searches the 24x80 presentation space for text and outputs its 1-based (Row, Column) coordinates.")]
    [Throws("SessionNotFound", FriendlyName = "Session Not Found", Description = "The specified SessionId does not exist or was closed.")]
    [Throws("SessionFaulted", FriendlyName = "Session Faulted", Description = "The terminal session is disconnected.")]
    public class FindTextAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle from Connect AS400 Session.", Order = 1)]
        public string SessionId { get; set; }

        [InputArgument(FriendlyName = "Search Text", Description = "Text string to search for on the terminal screen.", Order = 2)]
        public string SearchText { get; set; }

        [InputArgument(FriendlyName = "Start Row", Description = "1-based starting row to begin searching from (default 1).", Order = 3), DefaultValue(1)]
        public int StartRow { get; set; } = 1;

        [InputArgument(FriendlyName = "Start Column", Description = "1-based starting column to begin searching from (default 1).", Order = 4), DefaultValue(1)]
        public int StartCol { get; set; } = 1;

        [InputArgument(FriendlyName = "Case Sensitive", Description = "Whether to match text with exact casing.", Order = 5), DefaultValue(false)]
        public bool CaseSensitive { get; set; } = false;

        [OutputArgument(FriendlyName = "Found", Description = "Returns true if text exists on the current screen.", Order = 1)]
        public bool Found { get; set; }

        [OutputArgument(FriendlyName = "Found Row", Description = "1-based row index where text starts (0 if not found).", Order = 2)]
        public int FoundRow { get; set; }

        [OutputArgument(FriendlyName = "Found Column", Description = "1-based column index where text starts (0 if not found).", Order = 3)]
        public int FoundCol { get; set; }

        public override void Execute(ActionContext context)
        {
            ExecuteGuarded(() =>
            {
                var driver = SessionRegistry.Instance.Get(SessionId);
                Found = driver.FindText(SearchText, CaseSensitive, out int r, out int c, StartRow, StartCol);
                FoundRow = r;
                FoundCol = c;
            }, nameof(FindTextAction), SessionId);
        }
    }
}
