using System;
using System.ComponentModel;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK;
using Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK.Attributes;
using AS400PADCustomAction.Core;
using AS400PADCustomAction.Core.Tn5250;

namespace AS400PADCustomAction.Actions
{
    [Action(
        Id = "AS400_Connect",
        Category = CategoryName,
        FriendlyName = "Connect AS400 Session",
        Description = "Establishes a direct TN5250 Telnet connection to an AS400 / IBM i host with optional SSL/TLS encryption.")]
    [Throws("ConnectionFailed", FriendlyName = "Connection Failed", Description = "Unable to reach or connect to the AS400 host on the specified port.")]
    [Throws("ConnectionTimeout", FriendlyName = "Connection Timeout", Description = "The AS400 host did not respond within the specified timeout duration.")]
    public class ConnectSessionAction : ActionBaseAS400
    {
        [InputArgument(FriendlyName = "Host Name / IP", Description = "AS400 host name or IP address to connect to.", Order = 1)]
        public string Host { get; set; }

        [InputArgument(FriendlyName = "Port", Description = "Telnet port for terminal communication (default 23, or 992 for SSL).", Order = 2), DefaultValue(23)]
        public int Port { get; set; } = 23;

        [InputArgument(FriendlyName = "Session Short Name / ID", Description = "Identifier prefix or session letter (e.g., 'A').", Order = 3), DefaultValue("A")]
        public string SessionName { get; set; } = "A";

        [InputArgument(FriendlyName = "Timeout (seconds)", Description = "Connection and initial screen render timeout in seconds (default 30).", Order = 4), DefaultValue(30)]
        public int TimeoutSeconds { get; set; } = 30;

        [InputArgument(FriendlyName = "Use SSL / TLS", Description = "Enable secure SSL/TLS communication (typically used with port 992).", Order = 5), DefaultValue(false)]
        public bool UseSsl { get; set; } = false;

        [InputArgument(FriendlyName = "Accept Any Certificate", Description = "If true, accepts self-signed or internal enterprise CA certificates without validation errors.", Order = 6), DefaultValue(false)]
        public bool AcceptAnyCertificate { get; set; } = false;

        [OutputArgument(FriendlyName = "Session ID", Description = "Unique session handle required for subsequent AS400 actions.", Order = 1)]
        public string SessionId { get; set; }

        [OutputArgument(FriendlyName = "Is Connected", Description = "Returns true if the terminal session is established and active.", Order = 2)]
        public bool IsConnected { get; set; }

        public override void Execute(ActionContext context)
        {
            string cleanPrefix = !string.IsNullOrWhiteSpace(SessionName) ? SessionName.Trim().ToUpperInvariant() : "A";
            string generatedId = $"{cleanPrefix}_{Guid.NewGuid():N}".Substring(0, 12);
            Tn5250Client client = null;

            ExecuteGuarded(() =>
            {
                client = new Tn5250Client(generatedId);
                client.Connect(Host, Port, TimeoutSeconds, UseSsl, AcceptAnyCertificate);

                SessionRegistry.Instance.Register(generatedId, client);
                SessionId = generatedId;
                IsConnected = client.IsConnected;
            }, nameof(ConnectSessionAction), generatedId);
        }
    }
}
