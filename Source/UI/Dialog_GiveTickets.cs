using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Simple numeric-input dialog. The player types how many meal tickets to give to (or take from) a prisoner.
    /// </summary>
    public class Dialog_GiveTickets : Window
    {
        private readonly Pawn _prisoner;
        private readonly bool _give;     // true = give, false = take
        private string _buffer = "1";

        public override Vector2 InitialSize => new Vector2(300f, 180f);

        public Dialog_GiveTickets(Pawn prisoner, bool give)
        {
            _prisoner = prisoner;
            _give = give;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            var titleRect = new Rect(0f, 0f, inRect.width, 32f);
            Widgets.Label(titleRect, (_give ? "PPTE2_GiveDialogTitle" : "PPTE2_TakeDialogTitle").Translate(PPTEName.Ticket));
            Text.Font = GameFont.Small;

            var labelRect = new Rect(0f, 42f, inRect.width, 24f);
            var balance = PrisonersPayToEat2Manager.Current?.Balance(_prisoner) ?? 0f;
            Widgets.Label(labelRect, "PPTE2_CurrentBalance".Translate(balance.ToString("0.##")));

            var fieldRect = new Rect(0f, 72f, inRect.width, 32f);
            GUI.SetNextControlName("TicketField");
            _buffer = Widgets.TextField(fieldRect, _buffer);

            var btnRect = new Rect(0f, inRect.height - 35f, inRect.width, 35f);
            bool accept = Widgets.ButtonText(btnRect, "OK".Translate())
                || (Event.current.type == EventType.KeyDown
                    && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter));

            if (!accept) return;

            if (!float.TryParse(_buffer ?? "", out float nf) || nf <= 0f)
            {
                Messages.Message("PPTE2_InvalidNumber".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            var mgr = PrisonersPayToEat2Manager.Current;
            float signed = _give ? +nf : -nf;
            float before = mgr.Balance(_prisoner);
            mgr.AddTickets(_prisoner, signed);
            float after = mgr.Balance(_prisoner);
            // when taking more than balance, AddTickets clamps at 0; report the actual deduction.
            float actual = after - before;

            var msgKey = _give ? "PPTE2_GaveTickets" : "PPTE2_TookTickets";
            Messages.Message(msgKey.Translate(_prisoner.LabelShortCap, actual.ToString("0.##"), PPTEName.Ticket), _prisoner, MessageTypeDefOf.NeutralEvent);
            Close();
        }
    }
}