using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CP2077SaveEditor.Utils;
using WolvenKit.RED4.Save;
using WolvenKit.RED4.Save.IO;

namespace CP2077SaveEditor.Views
{
    public class SaveDiffDialog : Form
    {
        private readonly SaveFileHelper _currentSave;
        private readonly TextBox _filterBox;
        private readonly TextBox _outputBox;
        private readonly Label _statusLabel;
        private readonly Button _copyButton;
        private readonly Button _exportButton;
        private SaveDiffReport _report;

        public SaveDiffDialog(SaveFileHelper currentSave)
        {
            _currentSave = currentSave ?? throw new ArgumentNullException(nameof(currentSave));

            Text = "Compare With Another Save";
            Size = new Size(760, 560);
            MinimumSize = new Size(560, 400);
            StartPosition = FormStartPosition.CenterParent;

            var pickButton = new Button { Text = "Select sav.dat...", Size = new Size(125, 26), Location = new Point(12, 10) };
            pickButton.Click += async (_, _) => await PickAndCompareAsync();

            _filterBox = new TextBox { PlaceholderText = "Filter results...", Width = 240, Location = new Point(148, 12), BorderStyle = BorderStyle.FixedSingle };
            _filterBox.TextChanged += (_, _) => RenderReport();

            _copyButton = new Button { Text = "Copy", Size = new Size(80, 26), Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _copyButton.Click += (_, _) =>
            {
                if (!string.IsNullOrEmpty(_outputBox.Text))
                {
                    Clipboard.SetText(_outputBox.Text);
                }
            };

            _exportButton = new Button { Text = "Export .txt...", Size = new Size(95, 26), Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _exportButton.Click += (_, _) => ExportReport();

            var top = new Panel { Dock = DockStyle.Top, Height = 46 };
            _copyButton.Location = new Point(top.Width - 190, 10);
            _exportButton.Location = new Point(top.Width - 104, 10);
            top.Controls.AddRange(new Control[] { pickButton, _filterBox, _copyButton, _exportButton });

            _statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Padding = new Padding(12, 2, 12, 2),
                Text = "Select another sav.dat to compare against the currently loaded save."
            };

            _outputBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font(Theme.MonoFontFamily, 9.5f, FontStyle.Regular)
            };

            Controls.Add(_outputBox);
            Controls.Add(_statusLabel);
            Controls.Add(top);

            Theme.Apply(this);
        }

        private async Task PickAndCompareAsync()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select Save File to Compare (sav.dat)",
                Filter = "Cyberpunk 2077 Save (sav.dat)|sav.dat|All Files (*.*)|*.*",
                InitialDirectory = MainForm.SavesRootDirectory
            };

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            _statusLabel.Text = "Loading and comparing saves...";
            _outputBox.Text = string.Empty;
            _copyButton.Enabled = _exportButton.Enabled = false;

            try
            {
                var path = dialog.FileName;
                var report = await Task.Run(() =>
                {
                    using var stream = File.OpenRead(path);
                    using var reader = new CyberpunkSaveReader(stream);
                    var status = reader.ReadFile(out var otherSave);
                    if (status != EFileReadErrorCodes.NoError || otherSave == null)
                    {
                        throw new InvalidOperationException($"Could not read save file ({status}).");
                    }

                    var otherHelper = new SaveFileHelper { SaveFile = otherSave };
                    return SaveDiffHelper.Compare(_currentSave, otherHelper);
                });

                _report = report;
                _statusLabel.Text = $"Found {report.Entries.Count} difference(s) vs {Path.GetFileName(Path.GetDirectoryName(path))}\\{Path.GetFileName(path)}.";
                RenderReport();
                _copyButton.Enabled = _exportButton.Enabled = true;
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Comparison failed.";
                MessageBox.Show($"Failed to compare saves:\n{ex.Message}", "Save Diff", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RenderReport()
        {
            if (_report == null)
            {
                return;
            }

            var filter = _filterBox.Text.Trim();
            if (string.IsNullOrEmpty(filter))
            {
                _outputBox.Text = _report.FormatText();
                return;
            }

            var filtered = new SaveDiffReport();
            filtered.Entries.AddRange(_report.Entries.Where(e =>
                e.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                e.Before.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                e.After.Contains(filter, StringComparison.OrdinalIgnoreCase)));
            _outputBox.Text = filtered.FormatText();
        }

        private void ExportReport()
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Export Save Diff Report",
                Filter = "Text File (*.txt)|*.txt",
                FileName = "save-diff.txt"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllText(dialog.FileName, _outputBox.Text);
            }
        }
    }
}
