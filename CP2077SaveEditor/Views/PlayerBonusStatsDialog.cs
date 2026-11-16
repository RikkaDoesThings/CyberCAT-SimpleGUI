using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CP2077SaveEditor.Utils;
using static WolvenKit.RED4.Types.Enums;

namespace CP2077SaveEditor.Views
{
    public class PlayerBonusStatsDialog : Form
    {
        private readonly SaveFileHelper _save;
        private readonly Dictionary<gamedataStatType, NumericUpDown> _inputs = new();

        public PlayerBonusStatsDialog(SaveFileHelper save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));

            Text = "Bonus Player Stats";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(410, 395);

            var hint = new Label
            {
                Text = "Set additive bonus stats directly on V (persists across level-ups):",
                AutoSize = true,
                Location = new Point(14, 12)
            };
            Controls.Add(hint);

            var y = 40;
            foreach (var def in PlayerBonusStatsHelper.SupportedStats)
            {
                var label = new Label
                {
                    Text = $"{def.Label} ({def.Unit}):",
                    AutoSize = true,
                    Location = new Point(16, y + 4)
                };

                var current = (decimal)PlayerBonusStatsHelper.GetBonusStat(_save, def.StatType);
                var upDown = new NumericUpDown
                {
                    Minimum = Math.Min(def.Min, current),
                    Maximum = Math.Max(def.Max, current),
                    Increment = def.Step,
                    DecimalPlaces = 0,
                    Width = 130,
                    Location = new Point(260, y),
                    Value = Math.Clamp(Math.Round(current), Math.Min(def.Min, current), Math.Max(def.Max, current))
                };

                _inputs[def.StatType] = upDown;
                Controls.Add(label);
                Controls.Add(upDown);
                y += 34;
            }

            var resetBtn = new Button
            {
                Text = "Reset All to 0",
                Size = new Size(115, 28),
                Location = new Point(16, y + 12)
            };
            resetBtn.Click += (_, _) =>
            {
                foreach (var input in _inputs.Values)
                {
                    input.Value = 0;
                }
            };

            var applyBtn = new Button
            {
                Text = "Apply",
                Size = new Size(95, 28),
                Location = new Point(195, y + 12),
                DialogResult = DialogResult.OK
            };
            applyBtn.Click += Apply_Click;

            var cancelBtn = new Button
            {
                Text = "Cancel",
                Size = new Size(95, 28),
                Location = new Point(296, y + 12),
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[] { resetBtn, applyBtn, cancelBtn });
            AcceptButton = applyBtn;
            CancelButton = cancelBtn;

            Theme.Apply(this);
        }

        private void Apply_Click(object sender, EventArgs e)
        {
            foreach (var (stat, input) in _inputs)
            {
                PlayerBonusStatsHelper.SetBonusStat(_save, stat, (float)input.Value);
            }
        }
    }
}
