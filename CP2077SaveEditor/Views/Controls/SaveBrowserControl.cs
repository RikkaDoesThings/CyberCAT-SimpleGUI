using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using CP2077SaveEditor.Utils;

namespace CP2077SaveEditor.Views.Controls
{
    /// <summary>
    /// Lists the saves in the game's save folder with their screenshot and metadata, double click opens one.
    /// </summary>
    public class SaveBrowserControl : UserControl, IGameControl
    {
        private static readonly Size s_thumbnailSize = new(160, 90);

        private readonly MainForm _parentForm;

        private readonly TextBox _search;
        private readonly CheckBox _showAutoSaves;
        private readonly ComboBox _lifePathFilter;
        private readonly Button _deleteSelectedButton;
        private readonly ListView _list;
        private readonly ImageList _thumbnails;
        private readonly Label _emptyLabel;

        private List<SaveEntry> _saves = new();
        private int _refreshVersion;

        private record SaveEntry(string Folder, string SavPath, DateTime Modified, string Name, string Details, string Version, bool IsAutoSave, string LifePath);

        public SaveBrowserControl(MainForm parentForm)
        {
            _parentForm = parentForm;

            _search = new TextBox { PlaceholderText = "Search saves", Width = 200, Location = new Point(10, 8), BorderStyle = BorderStyle.FixedSingle };
            _search.TextChanged += (_, _) => FillList();

            _showAutoSaves = new CheckBox { Text = "Show autosaves", AutoSize = true, Checked = true, Location = new Point(220, 9) };
            _showAutoSaves.CheckedChanged += (_, _) => FillList();

            _lifePathFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
            _lifePathFilter.Items.AddRange(new object[] { "All lifepaths", "Corporate", "Nomad", "StreetKid" });
            _lifePathFilter.SelectedIndex = 0;
            _lifePathFilter.SelectedIndexChanged += (_, _) => FillList();

            var refreshButton = new Button { Text = "Refresh", Size = new Size(80, 26), Location = new Point(392, 5) };
            refreshButton.Click += (_, _) => RefreshSaves();

            _deleteSelectedButton = new Button { Text = "Delete Selected", Size = new Size(108, 26), Enabled = false };
            _deleteSelectedButton.Click += (_, _) => DeleteSelected();

            var cleanAutosavesButton = new Button { Text = "Clean Autosaves...", Size = new Size(122, 26) };
            cleanAutosavesButton.Click += (_, _) => CleanOldAutosaves();

            var top = new Panel { Dock = DockStyle.Top, Height = 38 };
            top.Paint += (_, e) =>
            {
                if (!Theme.IsDark)
                {
                    return;
                }
                using var bottomPen = new Pen(Theme.NeonRed, 1f);
                using var yellowPen = new Pen(Theme.Accent, 2f);
                e.Graphics.DrawLine(bottomPen, 0, top.Height - 1, top.Width, top.Height - 1);
                e.Graphics.DrawLine(yellowPen, 10, top.Height - 1, 140, top.Height - 1);

                if (top.Width > 980)
                {
                    using var hudFont = new Font(Theme.MonoFontFamily, 8f, FontStyle.Bold);
                    TextRenderer.DrawText(e.Graphics, "// NIGHT CITY DATABANK :: DOUBLE-CLICK TO JACK IN", hudFont,
                        new Rectangle(cleanAutosavesButton.Right + 12, 0, top.Width - cleanAutosavesButton.Right - 22, top.Height - 2), Theme.Cyan,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            };
            top.Resize += (_, _) => top.Invalidate();

            // Position the row from the real control sizes, fixed pixel positions overlap on scaled displays
            top.Layout += (_, _) =>
            {
                var s = top.DeviceDpi / 96f;
                var gap = (int)(10 * s);
                _search.Width = (int)(190 * s);
                _search.Location = new Point((int)(10 * s), (top.Height - _search.Height) / 2);
                _showAutoSaves.Location = new Point(_search.Right + gap, (top.Height - _showAutoSaves.Height) / 2);
                _lifePathFilter.Width = (int)(115 * s);
                _lifePathFilter.Location = new Point(_showAutoSaves.Right + gap, (top.Height - _lifePathFilter.Height) / 2);
                refreshButton.Size = new Size((int)(78 * s), (int)(26 * s));
                refreshButton.Location = new Point(_lifePathFilter.Right + gap, (top.Height - refreshButton.Height) / 2);
                _deleteSelectedButton.Size = new Size((int)(108 * s), (int)(26 * s));
                _deleteSelectedButton.Location = new Point(refreshButton.Right + gap, (top.Height - _deleteSelectedButton.Height) / 2);
                cleanAutosavesButton.Size = new Size((int)(122 * s), (int)(26 * s));
                cleanAutosavesButton.Location = new Point(_deleteSelectedButton.Right + gap, (top.Height - cleanAutosavesButton.Height) / 2);
            };
            top.Controls.AddRange(new Control[] { _search, _showAutoSaves, _lifePathFilter, refreshButton, _deleteSelectedButton, cleanAutosavesButton });

            _thumbnails = new ImageList { ImageSize = s_thumbnailSize, ColorDepth = ColorDepth.Depth32Bit };

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Tile,
                TileSize = new Size(400, 100),
                LargeImageList = _thumbnails,
                MultiSelect = true,
                BorderStyle = BorderStyle.None
            };
            // Tile view shows the item text plus one line per extra column
            _list.Columns.Add("Name");
            _list.Columns.Add("Details");
            _list.Columns.Add("Version");
            _list.ItemActivate += (_, _) => OpenSelected();
            _list.SelectedIndexChanged += (_, _) => _deleteSelectedButton.Enabled = _list.SelectedItems.Count > 0;

            _emptyLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            Controls.Add(_list);
            Controls.Add(_emptyLabel);
            Controls.Add(top);

            RefreshSaves();
        }

        public string GameControlName => "Saves";
        public string Icon => "";

        public void RefreshSaves()
        {
            _saves = ReadSaves();
            FillList();
            LoadThumbnailsAsync();
        }

        private static List<SaveEntry> ReadSaves()
        {
            var saves = new List<SaveEntry>();
            if (!Directory.Exists(MainForm.SavesRootDirectory))
            {
                return saves;
            }

            foreach (var folder in Directory.GetDirectories(MainForm.SavesRootDirectory))
            {
                var savPath = Path.Combine(folder, "sav.dat");
                if (!File.Exists(savPath))
                {
                    continue;
                }

                var folderName = Path.GetFileName(folder);
                saves.Add(ReadMetadata(folder, savPath) ?? new SaveEntry(folder, savPath, File.GetLastWriteTime(savPath), folderName, "", "",
                    folderName.StartsWith("AutoSave", StringComparison.OrdinalIgnoreCase), ""));
            }

            return saves.OrderByDescending(x => x.Modified).ToList();
        }

        private static SaveEntry ReadMetadata(string folder, string savPath)
        {
            try
            {
                var metadataPath = Path.Combine(folder, "metadata.9.json");
                if (!File.Exists(metadataPath))
                {
                    return null;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
                var metadata = document.RootElement.GetProperty("Data").GetProperty("metadata");

                string GetString(string name) => metadata.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : "";
                double GetNumber(string name) => metadata.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;
                bool GetBool(string name) => metadata.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

                var lifePath = GetString("lifePath");
                var playTime = TimeSpan.FromSeconds(GetNumber("playthroughTime"));
                var details = $"Level {GetNumber("level"):0} · {lifePath} · {GetString("bodyGender")} · {(int)playTime.TotalHours}h{playTime.Minutes:00}m played";
                var version = $"Patch {GetString("buildPatch")} · {File.GetLastWriteTime(savPath).ToString("g", CultureInfo.CurrentCulture)}" +
                              (GetBool("isModded") ? " · modded" : "");

                return new SaveEntry(folder, savPath, File.GetLastWriteTime(savPath), Path.GetFileName(folder), details, version,
                    Path.GetFileName(folder).StartsWith("AutoSave", StringComparison.OrdinalIgnoreCase), lifePath);
            }
            catch (Exception e)
            {
                // A broken metadata file shouldn't hide the save
                Logger.Error($"Failed to read the metadata of {folder}", e);
                return null;
            }
        }

        private void FillList()
        {
            var terms = _search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var selectedLifePath = _lifePathFilter.SelectedIndex > 0 ? _lifePathFilter.SelectedItem?.ToString() : null;
            var visible = _saves
                .Where(x => _showAutoSaves.Checked || !x.IsAutoSave)
                .Where(x => selectedLifePath == null || string.Equals(x.LifePath, selectedLifePath, StringComparison.OrdinalIgnoreCase))
                .Where(x => terms.All(t => (x.Name + " " + x.Details).Contains(t, StringComparison.CurrentCultureIgnoreCase)))
                .ToList();

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var save in visible)
            {
                _list.Items.Add(new ListViewItem(new[] { save.Name, save.Details, save.Version })
                {
                    Tag = save,
                    ImageKey = save.Folder
                });
            }
            _list.EndUpdate();

            _emptyLabel.Text = _saves.Count == 0
                ? $"No saves found in{Environment.NewLine}{MainForm.SavesRootDirectory}{Environment.NewLine}{Environment.NewLine}Use \"Load Save\" to open one from somewhere else."
                : "No saves match the search.";
            _emptyLabel.Visible = visible.Count == 0;
            _list.Visible = visible.Count > 0;
        }

        private async void LoadThumbnailsAsync()
        {
            // A newer refresh makes this one obsolete
            var version = ++_refreshVersion;
            _thumbnails.Images.Clear();

            foreach (var save in _saves)
            {
                var thumbnail = await Task.Run(() => CreateThumbnail(Path.Combine(save.Folder, "screenshot.png")));
                if (version != _refreshVersion || IsDisposed)
                {
                    thumbnail?.Dispose();
                    return;
                }

                if (thumbnail != null)
                {
                    _thumbnails.Images.Add(save.Folder, thumbnail);
                }
            }

            _list.Invalidate();
        }

        private static Bitmap CreateThumbnail(string screenshotPath)
        {
            try
            {
                if (!File.Exists(screenshotPath))
                {
                    return null;
                }

                using var image = Image.FromFile(screenshotPath);
                var w = s_thumbnailSize.Width;
                var h = s_thumbnailSize.Height;
                var thumbnail = new Bitmap(w, h);
                using var graphics = Graphics.FromImage(thumbnail);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(image, 0, 0, w, h);

                if (Theme.IsDark)
                {
                    using var borderPen = new Pen(Theme.NeonRed, 1f);
                    using var cyanPen = new Pen(Theme.Cyan, 2f);
                    using var yellowBrush = new SolidBrush(Theme.Accent);
                    graphics.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
                    graphics.FillRectangle(yellowBrush, 0, 0, 3, h);
                    graphics.DrawLine(cyanPen, w - 12, 0, w - 1, 0);
                    graphics.DrawLine(cyanPen, w - 1, 0, w - 1, 12);
                    graphics.DrawLine(cyanPen, 0, h - 1, 12, h - 1);
                }
                return thumbnail;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void OpenSelected()
        {
            var target = _list.FocusedItem ?? (_list.SelectedItems.Count > 0 ? _list.SelectedItems[0] : null);
            if (target?.Tag is SaveEntry save)
            {
                _parentForm.OpenSave(save.SavPath);
            }
        }

        private void DeleteSelected()
        {
            var selected = _list.SelectedItems.Cast<ListViewItem>()
                .Select(x => x.Tag)
                .OfType<SaveEntry>()
                .ToList();
            if (selected.Count == 0)
            {
                return;
            }

            if (MessageBox.Show(
                $"Send {selected.Count} selected save folder(s) to the Recycle Bin?",
                "Delete Saves",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            DeleteSaveEntries(selected);
        }

        private void CleanOldAutosaves()
        {
            const int keepNewest = 5;
            var oldAutosaves = _saves
                .Where(x => x.IsAutoSave)
                .OrderByDescending(x => x.Modified)
                .Skip(keepNewest)
                .ToList();

            if (oldAutosaves.Count == 0)
            {
                MessageBox.Show($"You have {keepNewest} or fewer autosaves. Nothing to clean.", "Clean Autosaves", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(
                $"Send {oldAutosaves.Count} older autosave(s) to the Recycle Bin, keeping the {keepNewest} newest?",
                "Clean Autosaves",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            DeleteSaveEntries(oldAutosaves);
        }

        private void DeleteSaveEntries(List<SaveEntry> entries)
        {
            var deleted = 0;
            foreach (var entry in entries)
            {
                try
                {
                    if (Directory.Exists(entry.Folder))
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                            entry.Folder,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        deleted++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to delete save folder {entry.Folder}", ex);
                }
            }

            RefreshSaves();
            MessageBox.Show($"Sent {deleted} save folder(s) to the Recycle Bin.", "Save Browser", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
