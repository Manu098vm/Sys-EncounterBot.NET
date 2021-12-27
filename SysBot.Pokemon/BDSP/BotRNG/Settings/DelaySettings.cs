using System.ComponentModel;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class DelaySettings
    {
        private const string AutoRNG = nameof(AutoRNG);
        public override string ToString() => "Delay Calculation Settings";

        [Category(AutoRNG), Description("Action to perform to start the receiving/encounter process. Available options: A, B, X, Y, DUP, DDOWN, DLEFT, DRIGHT, PLUS, MINUS, RSTICK, LSTICK, L, R, ZL, ZR.")]
        public SwitchButton Action { get; set; } = SwitchButton.A;
    }
}