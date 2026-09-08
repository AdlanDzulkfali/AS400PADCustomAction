using System.ComponentModel;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_ReadScreen",
        Category = CategoryName,
        FriendlyName = "Read AS400 Screen",
        Description = "Reads text from the active AS400 presentation space either linearly or as a rectangular bounding box.")]
    [Throws("SessionNotFound", FriendlyName = "Session Not Found", Description = "The specified SessionId does not exist or was closed.")]
    [Throws("SessionFaulted", FriendlyName = "Session Faulted", Description = "The terminal session is disconnected.")]
    [Throws("InvalidCoordinate", FriendlyName = "Invalid Coordinates", Description = "The specified row, column, or length exceeds the 24x80 presentation space boundaries.")]
    public class ReadScreenAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle from Connect AS400 Session.", Order = 1)]
        public string SessionId { get; set; }

        [InputArgument(FriendlyName = "Start Row", Description = "1-based starting row coordinate (1 to 24).", Order = 2), DefaultValue(1)]
        public int StartRow { get; set; } = 1;

        [InputArgument(FriendlyName = "Start Column", Description = "1-based starting column coordinate (1 to 80).", Order = 3), DefaultValue(1)]
        public int StartCol { get; set; } = 1;

        [InputArgument(FriendlyName = "Length", Description = "Number of characters to read sequentially (1920 reads entire 24x80 screen).", Order = 4), DefaultValue(1920)]
        public int Length { get; set; } = 1920;

        [InputArgument(FriendlyName = "End Row (Optional)", Description = "Optional end row if reading a rectangular screen box.", Order = 5)]
        public int? EndRow { get; set; }

        [InputArgument(FriendlyName = "End Column (Optional)", Description = "Optional end column if reading a rectangular screen box.", Order = 6)]
        public int? EndCol { get; set; }

        [OutputArgument(FriendlyName = "Screen Text", Description = "Extracted text content from the terminal presentation space.", Order = 1)]
        public string ScreenText { get; set; }

        public override void Execute(ActionContext context)
        {
            ExecuteGuarded(() =>
            {
                var driver = SessionRegistry.Instance.Get(SessionId);

                if (EndRow.HasValue && EndCol.HasValue)
                {
                    ScreenText = driver.ReadScreenBox(StartRow, StartCol, EndRow.Value, EndCol.Value);
                }
                else
                {
                    ScreenText = driver.ReadScreen(StartRow, StartCol, Length);
                }
            }, nameof(ReadScreenAction), SessionId);
        }
    }
}
