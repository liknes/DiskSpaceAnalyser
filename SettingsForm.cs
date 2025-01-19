using DiskSpaceAnalyzer.Settings;

namespace DiskSpaceAnalyzer
{
    public partial class SettingsForm : Form
    {
        private readonly AppSettings _settings;

        public SettingsForm(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            InitializeControls();
        }

        private void InitializeControls()
        {
            this.Size = new Size(400, 500);
            this.Text = "Settings";
            this.StartPosition = FormStartPosition.CenterParent;

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 8,
                ColumnCount = 2
            };

            // Minimum file size
            mainPanel.Controls.Add(new Label { Text = "Minimum File Size (MB):" }, 0, 0);
            var minSizeInput = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 1000,
                Value = _settings.MinimumFileSize / (1024 * 1024),
                Dock = DockStyle.Fill
            };
            mainPanel.Controls.Add(minSizeInput, 1, 0);

            // Show hidden files
            var hiddenFilesCheck = new CheckBox
            {
                Text = "Show Hidden Files",
                Checked = _settings.ShowHiddenFiles,
                Dock = DockStyle.Fill
            };
            mainPanel.Controls.Add(hiddenFilesCheck, 0, 1);

            // Show system files
            var systemFilesCheck = new CheckBox
            {
                Text = "Show System Files",
                Checked = _settings.ShowSystemFiles,
                Dock = DockStyle.Fill
            };
            mainPanel.Controls.Add(systemFilesCheck, 0, 2);

            // Dark mode
            var darkModeCheck = new CheckBox
            {
                Text = "Dark Mode",
                Checked = _settings.DarkMode,
                Dock = DockStyle.Fill
            };
            mainPanel.Controls.Add(darkModeCheck, 0, 3);

            // Auto expand nodes
            var autoExpandCheck = new CheckBox
            {
                Text = "Auto Expand Nodes",
                Checked = _settings.AutoExpandNodes,
                Dock = DockStyle.Fill
            };
            mainPanel.Controls.Add(autoExpandCheck, 0, 4);

            // Max displayed items
            mainPanel.Controls.Add(new Label { Text = "Max Displayed Items:" }, 0, 5);
            var maxItemsInput = new NumericUpDown
            {
                Minimum = 100,
                Maximum = 10000,
                Value = _settings.MaxDisplayedItems,
                Dock = DockStyle.Fill
            };
            mainPanel.Controls.Add(maxItemsInput, 1, 5);

            // Save button
            var saveButton = new Button
            {
                Text = "Save",
                Dock = DockStyle.Bottom
            };
            saveButton.Click += (s, e) =>
            {
                _settings.MinimumFileSize = (long)minSizeInput.Value * 1024 * 1024;
                _settings.ShowHiddenFiles = hiddenFilesCheck.Checked;
                _settings.ShowSystemFiles = systemFilesCheck.Checked;
                _settings.DarkMode = darkModeCheck.Checked;
                _settings.AutoExpandNodes = autoExpandCheck.Checked;
                _settings.MaxDisplayedItems = (int)maxItemsInput.Value;
                _settings.Save();
                DialogResult = DialogResult.OK;
            };

            mainPanel.Controls.Add(saveButton, 0, 7);
            this.Controls.Add(mainPanel);
        }
    }
}
