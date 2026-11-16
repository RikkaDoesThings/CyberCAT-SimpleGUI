using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using CP2077SaveEditor.Extensions;
using CP2077SaveEditor.Utils;
using WolvenKit.RED4.Save;

namespace CP2077SaveEditor.Views.Controls
{
    public partial class QuestFactsControl : UserControl, IGameControl
    {
        private readonly MainForm _parentForm;

        public QuestFactsControl(MainForm parentForm)
        {
            InitializeComponent();

            _parentForm = parentForm;
            _parentForm.PropertyChanged += OnParentFormPropertyChanged;

            var toolTip = new ToolTip();
            Theme.SetToolTip(toolTip, enableSecretEndingButton, "Sets sq032_johnny_friend = 1 so Johnny's secret ending ('Don't Fear the Reaper') is available in Nocturne Op55N1.");
            Theme.SetToolTip(toolTip, makeAllRomanceableButton, "Sets the romanceable flags for Judy, Panam, River and Kerry regardless of V's gender.");
            Theme.SetToolTip(toolTip, addFactButton, "Add a new quest fact entry by name or hash.");
            Theme.SetToolTip(toolTip, presetsButton, "Apply common safe quest fact presets (e.g. Skippy mode).");
            Theme.SetToolTip(toolTip, importFactsButton, "Import quest facts from a JSON file previously exported with 'Export to Text'.");
            Theme.SetToolTip(toolTip, factsSaveButton, "Export all known quest facts and their current values to a JSON text file.");
        }

        public string GameControlName => "Quest Facts";
        public string Icon => "";

        private void OnParentFormPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_parentForm.ActiveSaveFile))
            {
                if (_parentForm.ActiveSaveFile != null)
                {
                    this.InvokeIfRequired(RefreshFacts);
                }
            }
        }

        private void factsListView_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (!uint.TryParse(e.Label, out var value))
            {
                e.CancelEdit = true;
            }

            ((FactsTable.FactEntry)factsListView.SelectedVirtualItems()[0].Tag).Value = value;
            ChangeTracker.MarkChanged();
        }

        private void factsListView_MouseUp(object sender, EventArgs e)
        {
            if (factsListView.SelectedIndices.Count > 0)
            {
                factsListView.SelectedVirtualItems()[0].BeginEdit();
            }
        }

        private void factsListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (factsListView.SelectedIndices.Count > 0 && e.KeyCode == Keys.Delete)
            {
                ((FactsTable)_parentForm.ActiveSaveFile.GetFactsContainer().Children[0].Value).FactEntries.Remove((FactsTable.FactEntry)factsListView.SelectedVirtualItems()[0].Tag);
                ChangeTracker.MarkChanged();
                _parentForm.SetStatus("'" + factsListView.SelectedVirtualItems()[0].SubItems[1].Text + "' deleted.");
                factsListView.RemoveVirtualItem(factsListView.SelectedVirtualItems()[0]);
            }
        }

        private void SearchBoxGotFocus(object sender, EventArgs e)
        {
            if (((TextBox)sender).Text == "Search" && ((TextBox)sender).ForeColor == Color.Silver)
            {
                ((TextBox)sender).Text = "";
                ((TextBox)sender).ForeColor = Theme.IsDark ? Theme.DarkText : SystemColors.WindowText;
            }
        }

        private void SearchBoxLostFocus(object sender, EventArgs e)
        {
            if (((TextBox)sender).Text == "")
            {
                ((TextBox)sender).ForeColor = Color.Silver;
                ((TextBox)sender).Text = "Search";
            }
        }

        private void RefreshFacts()
        {
            RefreshFacts("");
        }

        private void RefreshFacts(string search)
        {
            var factsList = _parentForm.ActiveSaveFile.GetKnownFacts();
            var listViewRows = new List<ListViewItem>();

            foreach (FactsTable.FactEntry fact in factsList)
            {
                if (search != "")
                {
                    if (!_parentForm.ActiveSaveFile.KnownFacts[fact.FactName].ToLower().Contains(search.ToLower()))
                    {
                        continue;
                    }
                }

                var newItem = new ListViewItem(new string[] { fact.Value.ToString(), _parentForm.ActiveSaveFile.KnownFacts[fact.FactName] });
                newItem.Tag = fact;

                listViewRows.Add(newItem);
            }

            factsListView.SetVirtualItems(listViewRows);
        }

        private void factsSearchBox_TextChanged(object sender, EventArgs e)
        {
            if (factsSearchBox.ForeColor != Color.Silver)
            {
                RefreshFacts(factsSearchBox.Text);
            }
        }

        private void addFactButton_Click(object sender, EventArgs e)
        {
            var factDialog = new AddFact();
            factDialog.LoadFactDialog(RefreshFacts, _parentForm.ActiveSaveFile);
        }

        private void factsSaveButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Note: This option is not meant to overwrite your save file. If you are only trying to save changes to your quest facts, you should press 'Save Changes' instead.");

            var saveWindow = new SaveFileDialog();
            saveWindow.Filter = "Text File|*.txt";
            if (saveWindow.ShowDialog() == DialogResult.OK)
            {
                var facts = _parentForm.ActiveSaveFile.GetKnownFacts();
                var final = new List<KeyValuePair<string, uint>>(facts.Count);

                foreach (var singleFact in facts)
                {
                    final.Add(new KeyValuePair<string, uint>(_parentForm.ActiveSaveFile.KnownFacts[singleFact.FactName], singleFact.Value));
                }

                File.WriteAllText(saveWindow.FileName, JsonSerializer.Serialize(final, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        private void enableSecretEndingButton_Click(object sender, EventArgs e)
        {
            _parentForm.ActiveSaveFile.SetFactByName("sq032_johnny_friend", 1);
            ChangeTracker.MarkChanged();
            RefreshFacts();
            MessageBox.Show("Secret ending enabled.");
        }

        private void makeAllRomanceableButton_Click(object sender, EventArgs e)
        {
            _parentForm.ActiveSaveFile.SetFactByName("judy_romanceable", 1);
            _parentForm.ActiveSaveFile.SetFactByName("river_romanceable", 1);
            _parentForm.ActiveSaveFile.SetFactByName("panam_romanceable", 1);
            _parentForm.ActiveSaveFile.SetFactByName("kerry_romanceable", 1);
            ChangeTracker.MarkChanged();
            RefreshFacts();
            MessageBox.Show("Romance flags set for Judy, River, Panam and Kerry." + Environment.NewLine + Environment.NewLine +
                            "The game still checks V's body and voice in some romance scenes, so a romance outside the vanilla " +
                            "rules (for example Panam with a female V) also needs a romance mod.");
        }

        private void presetsButton_Click(object sender, EventArgs e)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Skippy: Stone Cold Killer (Headshots)", null, (_, _) =>
            {
                _parentForm.ActiveSaveFile.SetFactByName("mq007_skippy_aim_at_head", 1);
                _parentForm.ActiveSaveFile.SetFactByName("mq007_skippy_goes_to_regina", 0);
                ChangeTracker.MarkChanged();
                RefreshFacts();
                MessageBox.Show("Skippy set to Stone Cold Killer mode (headshots).", "Quest Facts Preset");
            });
            menu.Items.Add("Skippy: Puppy-Loving Pacifist (Limbs)", null, (_, _) =>
            {
                _parentForm.ActiveSaveFile.SetFactByName("mq007_skippy_aim_at_head", 0);
                _parentForm.ActiveSaveFile.SetFactByName("mq007_skippy_goes_to_regina", 0);
                ChangeTracker.MarkChanged();
                RefreshFacts();
                MessageBox.Show("Skippy set to Puppy-Loving Pacifist mode (limbs).", "Quest Facts Preset");
            });
            menu.Items.Add("Secret Ending ('Don't Fear the Reaper')", null, enableSecretEndingButton_Click);
            menu.Items.Add("Make All Characters Romanceable", null, makeAllRomanceableButton_Click);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Apartment Pet: Adopt Nibbles the Cat", null, (_, _) =>
            {
                _parentForm.ActiveSaveFile.SetFactByName("nibbles_unlocked", 1);
                ChangeTracker.MarkChanged();
                RefreshFacts();
                MessageBox.Show("Nibbles the cat unlocked in V's Megabuilding H10 apartment.", "Quest Facts Preset");
            });
            menu.Items.Add("Apartment Pet: Hatch Iguana", null, (_, _) =>
            {
                _parentForm.ActiveSaveFile.SetFactByName("mws_wat_02_started", 1);
                _parentForm.ActiveSaveFile.SetFactByName("mws_wat_02_egg_placed", 1);
                _parentForm.ActiveSaveFile.SetFactByName("mws_wat_02_iguana_hatched", 1);
                ChangeTracker.MarkChanged();
                RefreshFacts();
                MessageBox.Show("Iguana hatched in V's Megabuilding H10 apartment.", "Quest Facts Preset");
            });
            menu.Items.Add("Story: Mark Takemura Alive (q112_takemura_dead = 0)", null, (_, _) =>
            {
                _parentForm.ActiveSaveFile.SetFactByName("q112_takemura_dead", 0);
                ChangeTracker.MarkChanged();
                RefreshFacts();
                MessageBox.Show("Takemura marked as alive (q112_takemura_dead = 0).", "Quest Facts Preset");
            });
            menu.Items.Add("Story: Friendly Fingers MD (Not Beaten / Alive)", null, (_, _) =>
            {
                _parentForm.ActiveSaveFile.SetFactByName("q105_fingers_beaten", 0);
                _parentForm.ActiveSaveFile.SetFactByName("q105_fingers_dead", 0);
                ChangeTracker.MarkChanged();
                RefreshFacts();
                MessageBox.Show("Fingers MD marked as alive and friendly.", "Quest Facts Preset");
            });
            menu.Items.Add("World: Fix Pacifica Rollercoaster", null, (_, _) =>
            {
                _parentForm.ActiveSaveFile.SetFactByName("mq006_roller_coaster_fixed", 1);
                _parentForm.ActiveSaveFile.SetFactByName("mq006_enable_repeatable_rollercoaster", 1);
                ChangeTracker.MarkChanged();
                RefreshFacts();
                MessageBox.Show("Pacifica Rollercoaster repaired and enabled.", "Quest Facts Preset");
            });
            Theme.Apply(menu);
            menu.Show(presetsButton, new Point(0, presetsButton.Height));
        }

        private void importFactsButton_Click(object sender, EventArgs e)
        {
            using var openDialog = new OpenFileDialog
            {
                Title = "Import Quest Facts JSON",
                Filter = "JSON File (*.json)|*.json|All Files (*.*)|*.*"
            };

            if (openDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            try
            {
                var entries = System.Text.Json.JsonSerializer.Deserialize<List<KeyValuePair<string, uint>>>(File.ReadAllText(openDialog.FileName));
                if (entries == null || entries.Count == 0)
                {
                    MessageBox.Show("No quest facts found in the selected file.", "Import Quest Facts", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (MessageBox.Show($"Apply {entries.Count} quest fact(s) from \"{Path.GetFileName(openDialog.FileName)}\"?", "Import Quest Facts", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }

                foreach (var entry in entries)
                {
                    _parentForm.ActiveSaveFile.SetFactByName(entry.Key, entry.Value);
                }

                ChangeTracker.MarkChanged();
                RefreshFacts();
                MessageBox.Show($"Imported {entries.Count} quest fact(s).", "Import Quest Facts", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import quest facts:\n{ex.Message}", "Import Quest Facts", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
