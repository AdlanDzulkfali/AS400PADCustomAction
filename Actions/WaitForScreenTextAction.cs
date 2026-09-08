using System;
using System.ComponentModel;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_WaitForText",
        Category = CategoryName,
        FriendlyName = "Wait for AS400 Text",
        Description = "Waits dynamically until specific text appears on the terminal screen. Eliminates fragile static sleep delays.")]
    [Throws("SessionNotFound", FriendlyName = "Session Not Found", Description = "The specified SessionId does not exist or was closed.")]
    [Throws("SessionFaulted", FriendlyName = "Session Faulted", Description = "The terminal session is disconnected.")]
    [Throws("OperationTimeout", FriendlyName = "Operation Timeout", Description = "Target text did not appear within the specified timeout duration.")]
    public class WaitForScreenTextAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle from Connect AS400 Session.", Order = 1)]
        public string SessionId { get; set; }

        [InputArgument(FriendlyName = "Text to Wait For", Description = "Text string that signifies screen arrival (e.g., 'Customer Inquiry', 'Sign On').", Order = 2)]
        public string TextToWait { get; set; }

        [InputArgument(FriendlyName = "Timeout (seconds)", Description = "Maximum time in seconds to wait for text to appear (default 30).", Order = 3), DefaultValue(30)]
        public int TimeoutSeconds { get; set; } = 30;

        [InputArgument(FriendlyName = "Target Row (Optional)", Description = "Check exact row (1-24). If omitted, searches anywhere on screen.", Order = 4)]
        public int? TargetRow { get; set; }

        [InputArgument(FriendlyName = "Target Column (Optional)", Description = "Check exact column (1-80). If omitted, searches anywhere on screen.", Order = 5)]
        public int? TargetCol { get; set; }

        [InputArgument(FriendlyName = "Case Sensitive", Description = "Whether to match text with exact casing.", Order = 6), DefaultValue(false)]
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
                Found = driver.WaitForText(TextToWait, TimeoutSeconds, TargetRow, TargetCol, CaseSensitive);

                if (!Found)
                {
                    throw new AS400Exception(AS400ErrorCode.OperationTimeout,
                        $"Text '{TextToWait}' did not appear on AS400 screen within {TimeoutSeconds} seconds.");
                }

                if (TargetRow.HasValue && TargetCol.HasValue)
                {
                    FoundRow = TargetRow.Value;
                    FoundCol = TargetCol.Value;
                }
                else
                {
                    driver.FindText(TextToWait, CaseSensitive, out int r, out int c);
                    FoundRow = r;
                    FoundCol = c;
                }
            }, nameof(WaitForScreenTextAction), SessionId);
        }
    }
}
