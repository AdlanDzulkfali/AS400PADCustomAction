using System.ComponentModel;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_WaitForTextToAppear",
        Category = CategoryName,
        FriendlyName = "Wait for Text to Appear",
        Description = "Waits until specified text appears anywhere on the current AS400 screen. Eliminates fragile static sleep delays.")]
    [Throws("SessionNotFound", FriendlyName = "Session Not Found", Description = "The specified SessionId does not exist or was closed.")]
    [Throws("SessionFaulted", FriendlyName = "Session Faulted", Description = "The terminal session is disconnected.")]
    [Throws("OperationTimeout", FriendlyName = "Operation Timeout", Description = "Target text did not appear on the screen within the specified timeout duration.")]
    public class WaitForTextToAppearAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle from Connect AS400 Session.", Order = 1)]
        public string SessionId { get; set; }

        [InputArgument(FriendlyName = "Text to Wait For", Description = "Text string to wait for on the screen (e.g., 'Customer Inquiry', 'Sign On', 'MAIN MENU').", Order = 2)]
        public string TextToWait { get; set; }

        [InputArgument(FriendlyName = "Timeout (seconds)", Description = "Maximum time in seconds to wait for text to appear (default 30).", Order = 3), DefaultValue(30)]
        public int TimeoutSeconds { get; set; } = 30;

        [InputArgument(FriendlyName = "Case Sensitive", Description = "Whether to match text casing strictly (default false).", Order = 4), DefaultValue(false)]
        public bool CaseSensitive { get; set; } = false;

        [OutputArgument(FriendlyName = "Found", Description = "Returns true if text appeared before timeout.", Order = 1)]
        public bool Found { get; set; }

        [OutputArgument(FriendlyName = "Found Row", Description = "1-based row coordinate where text was matched.", Order = 2)]
        public int FoundRow { get; set; }

        [OutputArgument(FriendlyName = "Found Column", Description = "1-based column coordinate where text was matched.", Order = 3)]
        public int FoundCol { get; set; }

        public override void Execute(ActionContext context)
        {
            ExecuteGuarded(() =>
            {
                var driver = SessionRegistry.Instance.Get(SessionId);
                // Searches across the entire 24x80 screen (no row/col filter)
                Found = driver.WaitForText(TextToWait, TimeoutSeconds, null, null, CaseSensitive);

                if (!Found)
                {
                    throw new AS400Exception(AS400ErrorCode.OperationTimeout,
                        $"Text '{TextToWait}' did not appear on AS400 screen within {TimeoutSeconds} seconds.");
                }

                driver.FindText(TextToWait, CaseSensitive, out int r, out int c);
                FoundRow = r;
                FoundCol = c;
                driver.NotifyActionProgress("WaitForText", $"Matched \"{TextToWait}\" at R:{FoundRow} C:{FoundCol}");
            }, nameof(WaitForTextToAppearAction), SessionId);
        }
    }
}
