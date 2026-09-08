using System.ComponentModel;

namespace AS400PADCustomAction.Core
{
    /// <summary>
    /// Represents standard AS400 / IBM 5250 function and control keys.
    /// Used as an enum InputArgument in Power Automate Desktop actions to render a native drop-down picker.
    /// Includes Description and TypeConverter attributes to ensure display names render correctly in PAD UI.
    /// </summary>
    [TypeConverter(typeof(EnumConverter))]
    public enum AS400Key
    {
        [Description("Enter")]
        Enter = 0,

        [Description("F1")]
        F1 = 1,

        [Description("F2")]
        F2 = 2,

        [Description("F3")]
        F3 = 3,

        [Description("F4")]
        F4 = 4,

        [Description("F5")]
        F5 = 5,

        [Description("F6")]
        F6 = 6,

        [Description("F7")]
        F7 = 7,

        [Description("F8")]
        F8 = 8,

        [Description("F9")]
        F9 = 9,

        [Description("F10")]
        F10 = 10,

        [Description("F11")]
        F11 = 11,

        [Description("F12")]
        F12 = 12,

        [Description("F13")]
        F13 = 13,

        [Description("F14")]
        F14 = 14,

        [Description("F15")]
        F15 = 15,

        [Description("F16")]
        F16 = 16,

        [Description("F17")]
        F17 = 17,

        [Description("F18")]
        F18 = 18,

        [Description("F19")]
        F19 = 19,

        [Description("F20")]
        F20 = 20,

        [Description("F21")]
        F21 = 21,

        [Description("F22")]
        F22 = 22,

        [Description("F23")]
        F23 = 23,

        [Description("F24")]
        F24 = 24,

        [Description("Page Up (Roll Down)")]
        PageUp = 25,

        [Description("Page Down (Roll Up)")]
        PageDown = 26,

        [Description("Clear")]
        Clear = 27,

        [Description("Help")]
        Help = 28,

        [Description("Print")]
        Print = 29,

        [Description("Record Backspace")]
        RecordBackspace = 30,

        [Description("Transmit")]
        Transmit = 31
    }
}
