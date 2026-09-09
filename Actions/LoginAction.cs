using System.ComponentModel;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_Login",
        Category = CategoryName,
        FriendlyName = "Login to AS400 Session",
        Description = "Automates the complete AS400 sign-on process, entering credentials and automatically clearing post-sign-on informational and message screens until reaching the main menu or target application.")]
    [Throws("SessionNotFound", FriendlyName = "Session Not Found", Description = "The specified SessionId does not exist or was closed.")]
    [Throws("SessionFaulted", FriendlyName = "Session Faulted", Description = "The terminal session is disconnected.")]
    [Throws("GeneralError", FriendlyName = "Authentication Failed", Description = "Invalid credentials or user profile disabled.")]
    [Throws("OperationTimeout", FriendlyName = "Login Timeout", Description = "Timed out waiting for sign-on completion or target screen.")]
    public class LoginAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Session ID", Description = "Active session handle from Connect AS400 Session.", Order = 1)]
        public string SessionId { get; set; }

        [InputArgument(FriendlyName = "Username", Description = "AS400 user profile name to sign on with.", Order = 2)]
        public string Username { get; set; }

        [InputArgument(FriendlyName = "Password", Description = "AS400 user password.", Order = 3)]
        public string Password { get; set; }

        [InputArgument(FriendlyName = "Expected Success Text (Optional)", Description = "Text snippet confirming successful login (e.g., 'MAIN' or application title). If omitted, verifies sign-on screen is dismissed.", Order = 4)]
        public string ExpectedSuccessText { get; set; }

        [InputArgument(FriendlyName = "Timeout (seconds)", Description = "Maximum duration in seconds to wait for sign-on completion (default 30).", Order = 5), DefaultValue(30)]
        public int TimeoutSeconds { get; set; } = 30;

        [InputArgument(FriendlyName = "User Row (Optional)", Description = "Row position of the User field. If omitted, automatically detected from screen.", Order = 6)]
        public int? UserRow { get; set; }

        [InputArgument(FriendlyName = "User Column (Optional)", Description = "Column position of the User field. If omitted, automatically detected from screen.", Order = 7)]
        public int? UserCol { get; set; }

        [InputArgument(FriendlyName = "Password Row (Optional)", Description = "Row position of the Password field. If omitted, automatically detected from screen.", Order = 8)]
        public int? PasswordRow { get; set; }

        [InputArgument(FriendlyName = "Password Column (Optional)", Description = "Column position of the Password field. If omitted, automatically detected from screen.", Order = 9)]
        public int? PasswordCol { get; set; }

        [OutputArgument(FriendlyName = "Success", Description = "Returns true if sign-on succeeded and all post-sign-on screens were dismissed.", Order = 1)]
        public bool Success { get; set; }

        [OutputArgument(FriendlyName = "Screen Text", Description = "All text content of the active screen after completing sign-on.", Order = 2)]
        public string ScreenText { get; set; }

        public override void Execute(ActionContext context)
        {
            ExecuteGuarded(() =>
            {
                var driver = SessionRegistry.Instance.Get(SessionId);
                Success = driver.Login(Username, Password, UserRow, UserCol, PasswordRow, PasswordCol, ExpectedSuccessText, TimeoutSeconds);
                ScreenText = driver.ReadScreenBox(1, 1, 24, 80);
                driver.NotifyActionProgress("Login", "Sign-on completed successfully.");
            }, nameof(LoginAction), SessionId);
        }
    }
}
