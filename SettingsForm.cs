using DiskSpaceAnalyzer.Settings;

namespace DiskSpaceAnalyzer
{
    public partial class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private CheckBox? darkModeCheckBox;
        private CheckBox? showHiddenFilesCheckBox;
        private CheckBox? showSystemFilesCheckBox;
        private NumericUpDown? maxDisplayedItemsUpDown;
        private Button? saveButton;
        private Button? cancelButton;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            InitializeSettingsComponent();
        }

        private void InitializeSettingsComponent()
        {
            this.Text = "Settings";
            this.Size = new Size(400, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                RowCount = 5,
                ColumnCount = 1,
                RowStyles = {
                    new RowStyle(SizeType.AutoSize),
                    new RowStyle(SizeType.AutoSize),
                    new RowStyle(SizeType.AutoSize),
                    new RowStyle(SizeType.AutoSize),
                    new RowStyle(SizeType.Percent, 100)
                }
            };

            // Display Settings Group
            var displayGroup = new GroupBox
            {
                Text = "Display Settings",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            darkModeCheckBox = new CheckBox
            {
                Text = "Dark Mode",
                Checked = _settings.DarkMode,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                Location = new Point(15, 25)
            };

            // File Settings Group
            var fileGroup = new GroupBox
            {
                Text = "File Settings",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            showHiddenFilesCheckBox = new CheckBox
            {
                Text = "Show Hidden Files",
                Checked = _settings.ShowHiddenFiles,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                Location = new Point(15, 25)
            };

            showSystemFilesCheckBox = new CheckBox
            {
                Text = "Show System Files",
                Checked = _settings.ShowSystemFiles,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                Location = new Point(15, 50)
            };

            // Performance Settings Group
            var performanceGroup = new GroupBox
            {
                Text = "Performance Settings",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var maxItemsLabel = new Label
            {
                Text = "Maximum items displayed per folder:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                Location = new Point(15, 25)
            };

            maxDisplayedItemsUpDown = new NumericUpDown
            {
                Minimum = 10,
                Maximum = 1000,
                Value = _settings.MaxDisplayedItems,
                Location = new Point(15, 50),
                Width = 100
            };

            // Button Panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(0, 10, 0, 0)
            };

            saveButton = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Width = 80,
                Height = 30,
                Location = new Point(buttonPanel.Width - 175, 10)
            };
            saveButton.Click += SaveButton_Click;

            cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 80,
                Height = 30,
                Location = new Point(buttonPanel.Width - 85, 10)
            };

            // Add controls to groups
            displayGroup.Controls.Add(darkModeCheckBox);
            fileGroup.Controls.AddRange(new Control[] { showHiddenFilesCheckBox, showSystemFilesCheckBox });
            performanceGroup.Controls.AddRange(new Control[] { maxItemsLabel, maxDisplayedItemsUpDown });
            buttonPanel.Controls.AddRange(new Control[] { saveButton, cancelButton });

            // Add groups to main panel
            mainPanel.Controls.Add(displayGroup, 0, 0);
            mainPanel.Controls.Add(fileGroup, 0, 1);
            mainPanel.Controls.Add(performanceGroup, 0, 2);
            mainPanel.Controls.Add(buttonPanel, 0, 3);

            this.Controls.Add(mainPanel);
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (darkModeCheckBox != null)
                _settings.DarkMode = darkModeCheckBox.Checked;
            
            if (showHiddenFilesCheckBox != null)
                _settings.ShowHiddenFiles = showHiddenFilesCheckBox.Checked;
            
            if (showSystemFilesCheckBox != null)
                _settings.ShowSystemFiles = showSystemFilesCheckBox.Checked;
            
            if (maxDisplayedItemsUpDown != null)
                _settings.MaxDisplayedItems = (int)maxDisplayedItemsUpDown.Value;
        }
    }
}
