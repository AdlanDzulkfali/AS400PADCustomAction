using System;
using System.ComponentModel;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_WriteText",
        Category = CategoryName,
        FriendlyName = "Write AS400 Text",
        Description = "Writes text into the terminal screen at specified coordinates or current cursor position without submitting. Allows filling multiple fields before submitting.")]
    [Throws("SessionNotFound", FriendlyName = "Session Not Found", Description = "The specified SessionId does not exist or was closed.")]
    [Throws("SessionFaulted", FriendlyName = "Session Faulted", Description = "The terminal session is disconnected.")]
    [Throws("InvalidCoordinate", FriendlyName = "Invalid Coordinates", Description = "The specified row or column is out of the 24x80 presentation space bounds.")]
    public class WriteTextAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle from Connect AS400 Session.", Order = 1)]
        public string SessionId { get; set; }

        [InputArgument(FriendlyName = "Text to Write", Description = "Text value to write into the field.", Order = 2)]
        public string Text { get; set; }

        [InputArgument(FriendlyName = "Row (Optional)", Description = "1-based target row (1-24). If omitted, writes at the current cursor row.", Order = 3)]
        public int? Row { get; set; }

        [InputArgument(FriendlyName = "Column (Optional)", Description = "1-based target column (1-80). If omitted, writes at the current cursor column.", Order = 4)]
        public int? Column { get; set; }

        [InputArgument(FriendlyName = "Erase Field Length", Description = "Number of characters to blank with spaces before writing new text (0 disables erasing).", Order = 5), DefaultValue(0)]
        public int EraseLength { get; set; } = 0;

        [OutputArgument(FriendlyName = "Success", Description = "Returns true if text was successfully written.", Order = 1)]
        public bool Success { get; set; }

        [OutputArgument(FriendlyName = "New Row", Description = "Cursor row position after writing text.", Order = 2)]
        public int NewRow { get; set; }

        [OutputArgument(FriendlyName = "New Column", Description = "Cursor column position after writing text.", Order = 3)]
        public int NewCol { get; set; }

        public override void Execute(ActionContext context)
        {
            ExecuteGuarded(() =>
            {
                var driver = SessionRegistry.Instance.Get(SessionId);
                driver.WriteText(Text, Row, Column, EraseLength);

                var cursor = driver.GetCursor();
                NewRow = cursor.Item1;
                NewCol = cursor.Item2;
                Success = true;

                driver.NotifyActionProgress("WriteText", $"\"{Text}\" at R:{NewRow} C:{NewCol}");
            }, nameof(WriteTextAction), SessionId);
        }
    }
}
