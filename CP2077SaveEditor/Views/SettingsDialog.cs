using System;
using System.Drawing;
using System.Windows.Forms;
using CP2077SaveEditor.Utils;

namespace CP2077SaveEditor.Views
{
    internal sealed class SettingsDialog : Form
    {
        private readonly ComboBox _themeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
        private readonly NumericUpDown _undoStepsBox = new() { Minimum = 0, Maximum = 50, Width = 100 };
        private readonly CheckBox _checkUpdatesBox = new() { Text = "Check for updates on startup", AutoSize = true };
        private readonly Button _saveButton = new() { Text = "Save", DialogResult = DialogResult.OK, Width = 100, Height = 32 };
        private readonly Button _cancelButton = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 100, Height = 32 };

        public SettingsDialog()
        {
            Text = "Editor Settings";
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 250);
            Padding = new Padding(16);

            _themeBox.Items.AddRange(new object[]
            {
                "Follow Windows setting",
                "Light (classic)",
                "Dark (Cyberpunk HUD)"
            });
            _themeBox.SelectedIndex = Math.Clamp(AppConfig.Theme, 0, 2);
            _undoStepsBox.Value = Math.Clamp(AppConfig.UndoSteps, 0, 50);
            _checkUpdatesBox.Checked = AppConfig.CheckForUpdates;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                AutoSize = true
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label { Text = "Theme:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) }, 0, 0);
            layout.Controls.Add(_themeBox, 1, 0);

            layout.Controls.Add(new Label { Text = "Undo steps (Ctrl+Z):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) }, 0, 1);
            layout.Controls.Add(_undoStepsBox, 1, 1);

            var undoHint = new Label
            {
                Text = "0 disables Undo/Redo (saves ~4 MB RAM per step and avoids pauses). Theme changes apply after restarting.",
                AutoSize = true,
                MaximumSize = new Size(390, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 4, 0, 10)
            };
            layout.SetColumnSpan(undoHint, 2);
            layout.Controls.Add(undoHint, 0, 2);

            layout.SetColumnSpan(_checkUpdatesBox, 2);
            _checkUpdatesBox.Margin = new Padding(0, 4, 0, 12);
            layout.Controls.Add(_checkUpdatesBox, 0, 3);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };
            buttons.Controls.Add(_cancelButton);
            buttons.Controls.Add(_saveButton);
            layout.SetColumnSpan(buttons, 2);
            layout.Controls.Add(buttons, 0, 4);

            Controls.Add(layout);
            AcceptButton = _saveButton;
            CancelButton = _cancelButton;

            _saveButton.Click += (_, _) =>
            {
                AppConfig.Save(_themeBox.SelectedIndex, (int)_undoStepsBox.Value, _checkUpdatesBox.Checked);
            };

            Utils.Theme.Apply(this);
        }
    }
}
