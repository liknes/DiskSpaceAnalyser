namespace DiskSpaceAnalyzer
{
    using DiskSpaceAnalyzer.Helpers;
    using DiskSpaceAnalyzer.Models;
    using DiskSpaceAnalyzer.Settings;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Windows.Forms.DataVisualization.Charting;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Security.Cryptography;
    using DiskSpaceAnalyzer.Forms;

    public partial class MainForm : Form
    {
        private TreeView treeView;
        private Button scanButton;
        private ComboBox driveComboBox;
        private Label statusLabel;
        private ProgressBar progressBar;
        private Panel fileInfoPanel;
        private Label fileInfoLabel;
        private Chart fileTypeChart;
        private Panel statisticsPanel;
        private Label statisticsLabel;
        private TextBox searchBox;
        private Panel filterPanel;
        private ComboBox sizeFilterCombo;
        private TextBox extensionFilterBox;
        private Button settingsButton;
        private SplitContainer mainSplitContainer;
        private SplitContainer rightSplitContainer;
        private FlowLayoutPanel breadcrumbBar;
        private Panel previewPanel;
        private PictureBox previewPicture;
        private TextBox previewText;

        private CancellationTokenSource? _cancellationTokenSource;
        private AppSettings _settings;
        private Dictionary<string, long> _fileTypeStats;
        private long _totalScannedSize;
        private int _totalFiles;
        private int _totalDirectories;

        private ImageList _iconList;
        private ConcurrentDictionary<string, FolderStats> _folderStats = new();

        public MainForm()
        {
            InitializeComponent();
            _settings = AppSettings.Load();
            _fileTypeStats = new Dictionary<string, long>();
            InitializeMainLayout();
            InitializeControls();
            LoadDrives();
            ApplyTheme();
            this.Text = "Disk Space Analyzer";
            this.WindowState = FormWindowState.Maximized;
        }

        private void InitializeMainLayout()
        {
            // Main split container (TreeView | Right Panel)
            mainSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 60
            };

            // Right split container (FileInfo | Chart)
            rightSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 70
            };

            mainSplitContainer.Panel2.Controls.Add(rightSplitContainer);
            this.Controls.Add(mainSplitContainer);
        }

        private void InitializeControls()
        {
            InitializeTopPanel();
            InitializeTreeView();
            InitializeBreadcrumbBar();
            InitializeFileInfoPanel();
            InitializeChartPanel();
            InitializeStatisticsPanel();
            InitializeFilterPanel();
            InitializeSearchBox();
            InitializeContextMenu();
            InitializePreviewPanel();
        }

        private void InitializeTopPanel()
        {
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(5)
            };

            driveComboBox = new ComboBox
            {
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(5, 8)
            };

            scanButton = new Button
            {
                Text = "Scan Drive",
                Width = 100,
                Location = new Point(210, 8)
            };
            scanButton.Click += ScanButton_Click;

            settingsButton = new Button
            {
                Text = "Settings",
                Width = 80,
                Location = new Point(320, 8)
            };
            settingsButton.Click += SettingsButton_Click;

            // Initialize progress bar
            progressBar = new ProgressBar
            {
                Width = 150,
                Location = new Point(410, 8),
                Style = ProgressBarStyle.Blocks,
                MarqueeAnimationSpeed = 30
            };

            var findDuplicatesButton = new Button
            {
                Text = "Find Duplicates",
                Width = 100,
                Location = new Point(570, 8)
            };
            findDuplicatesButton.Click += async (s, e) => await FindDuplicateFiles();

            var timelineButton = new Button
            {
                Text = "Timeline",
                Width = 80,
                Location = new Point(680, 8)
            };
            timelineButton.Click += (s, e) =>
            {
                var items = GetAllItems();
                using var timelineForm = new TimelineForm(items);
                timelineForm.ShowDialog(this);
            };

            var trendsButton = new Button
            {
                Text = "Disk Trends",
                Width = 80,
                Location = new Point(770, 8)
            };
            trendsButton.Click += (s, e) =>
            {
                var selectedDrive = driveComboBox.Text.Split(' ')[0];
                using var trendsForm = new DiskTrendsForm(selectedDrive);
                trendsForm.ShowDialog(this);
            };

            // Add all controls to the panel
            topPanel.Controls.AddRange(new Control[] { driveComboBox, scanButton, settingsButton, progressBar, findDuplicatesButton, timelineButton, trendsButton });
            this.Controls.Add(topPanel);
        }

        private void InitializeTreeView()
        {
            _iconList = new ImageList();
            _iconList.ColorDepth = ColorDepth.Depth32Bit;
            _iconList.ImageSize = new Size(16, 16);
            
            // Add default icons
            _iconList.Images.Add("folder", System.Drawing.SystemIcons.Application.ToBitmap());
            _iconList.Images.Add("file", System.Drawing.SystemIcons.WinLogo.ToBitmap());
            
            treeView = new TreeView
            {
                Dock = DockStyle.Fill,
                ShowNodeToolTips = true,
                ImageList = _iconList
            };
            treeView.AfterSelect += TreeView_AfterSelect;
            treeView.BeforeExpand += TreeView_BeforeExpand;
            mainSplitContainer.Panel1.Controls.Add(treeView);
        }

        private void InitializeBreadcrumbBar()
        {
            breadcrumbBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 30,
                AutoScroll = true,
                Padding = new Padding(5)
            };

            treeView.AfterSelect += (s, e) => UpdateBreadcrumb(e.Node);
            this.Controls.Add(breadcrumbBar);
        }

        private void InitializeFileInfoPanel()
        {
            fileInfoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle
            };

            fileInfoLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false
            };

            fileInfoPanel.Controls.Add(fileInfoLabel);
            rightSplitContainer.Panel1.Controls.Add(fileInfoPanel);
        }

        private void InitializeChartPanel()
        {
            fileTypeChart = new Chart
            {
                Dock = DockStyle.Fill
            };

            var chartArea = new ChartArea();
            fileTypeChart.ChartAreas.Add(chartArea);

            var series = new Series
            {
                ChartType = SeriesChartType.Pie,
                Name = "FileTypes"
            };

            fileTypeChart.Series.Add(series);
            rightSplitContainer.Panel2.Controls.Add(fileTypeChart);
        }

        private void InitializeStatisticsPanel()
        {
            statisticsPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BorderStyle = BorderStyle.FixedSingle
            };

            statisticsLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false
            };

            // Initialize status label
            statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                Text = "Ready"
            };

            statisticsPanel.Controls.Add(statisticsLabel);
            statisticsPanel.Controls.Add(statusLabel);  // Add status label to statistics panel
            this.Controls.Add(statisticsPanel);
        }

        private void InitializeFilterPanel()
        {
            filterPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                Padding = new Padding(5)
            };

            var filterLabel = new Label
            {
                Text = "Filter:",
                AutoSize = true,
                Location = new Point(5, 8)
            };

            extensionFilterBox = new TextBox
            {
                Width = 100,
                PlaceholderText = "*.ext",
                Location = new Point(50, 5)
            };

            sizeFilterCombo = new ComboBox
            {
                Items = { "All Sizes", ">1MB", ">10MB", ">100MB", ">1GB" },
                SelectedIndex = 0,
                Width = 100,
                Location = new Point(160, 5)
            };

            filterPanel.Controls.AddRange(new Control[] { filterLabel, extensionFilterBox, sizeFilterCombo });
            this.Controls.Add(filterPanel);
        }

        private void InitializeSearchBox()
        {
            searchBox = new TextBox
            {
                Dock = DockStyle.Top,
                PlaceholderText = "Search files..."
            };

            var searchDebounceTimer = new System.Windows.Forms.Timer { Interval = 300 };
            searchDebounceTimer.Tick += (s, e) =>
            {
                searchDebounceTimer.Stop();
                SearchNodes(treeView.Nodes, searchBox.Text.ToLower());
            };

            searchBox.TextChanged += (s, e) =>
            {
                searchDebounceTimer.Stop();
                searchDebounceTimer.Start();
            };

            this.Controls.Add(searchBox);
        }

        private void InitializeContextMenu()
        {
            var contextMenu = new ContextMenuStrip();

            var openMenuItem = new ToolStripMenuItem("Open", null, (s, e) => OpenSelectedFile());
            var openFolderMenuItem = new ToolStripMenuItem("Open Containing Folder", null, (s, e) => OpenContainingFolder());
            var deleteMenuItem = new ToolStripMenuItem("Delete", null, (s, e) => DeleteSelectedFile());
            var copyPathMenuItem = new ToolStripMenuItem("Copy Path", null, (s, e) => CopySelectedPath());
            var propertiesMenuItem = new ToolStripMenuItem("Properties", null, (s, e) => ShowFileProperties());

            // Add sorting submenu
            var sortingMenu = new ToolStripMenuItem("Sort By");
            sortingMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Size (Largest First)", null, (s, e) => SortNodes(SortBy.Size, false)),
                new ToolStripMenuItem("Size (Smallest First)", null, (s, e) => SortNodes(SortBy.Size, true)),
                new ToolStripMenuItem("Name (A-Z)", null, (s, e) => SortNodes(SortBy.Name, false)),
                new ToolStripMenuItem("Name (Z-A)", null, (s, e) => SortNodes(SortBy.Name, true)),
                new ToolStripMenuItem("Date Modified (Newest First)", null, (s, e) => SortNodes(SortBy.Date, false)),
                new ToolStripMenuItem("Date Modified (Oldest First)", null, (s, e) => SortNodes(SortBy.Date, true))
            });

            // Add export submenu
            var exportMenu = new ToolStripMenuItem("Export");
            exportMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Export to CSV", null, (s, e) => ExportToCSV()),
                new ToolStripMenuItem("Export to JSON", null, (s, e) => ExportToJSON()),
                new ToolStripMenuItem("Export Report", null, (s, e) => ExportReport())
            });

            contextMenu.Items.AddRange(new ToolStripItem[]
            {
                openMenuItem,
                openFolderMenuItem,
                new ToolStripSeparator(),
                deleteMenuItem,
                copyPathMenuItem,
                propertiesMenuItem,
                new ToolStripSeparator(),
                sortingMenu,
                new ToolStripSeparator(),
                exportMenu
            });

            treeView.ContextMenuStrip = contextMenu;
        }

        private enum SortBy
        {
            Size,
            Name,
            Date
        }

        private void SortNodes(SortBy sortBy, bool ascending)
        {
            if (treeView.SelectedNode == null) return;

            var nodes = treeView.SelectedNode.Nodes.Cast<TreeNode>().ToList();
            
            IOrderedEnumerable<TreeNode> sortedNodes = sortBy switch
            {
                SortBy.Size => ascending 
                    ? nodes.OrderBy(n => (n.Tag as FileSystemItem)?.Size ?? 0)
                    : nodes.OrderByDescending(n => (n.Tag as FileSystemItem)?.Size ?? 0),
                
                SortBy.Name => ascending
                    ? nodes.OrderBy(n => n.Text)
                    : nodes.OrderByDescending(n => n.Text),
                
                SortBy.Date => ascending
                    ? nodes.OrderBy(n => (n.Tag as FileSystemItem)?.Modified ?? DateTime.MinValue)
                    : nodes.OrderByDescending(n => (n.Tag as FileSystemItem)?.Modified ?? DateTime.MinValue),
                
                _ => nodes.OrderBy(n => n.Text)
            };

            treeView.BeginUpdate();
            treeView.SelectedNode.Nodes.Clear();
            foreach (var node in sortedNodes)
            {
                treeView.SelectedNode.Nodes.Add(node);
            }
            treeView.EndUpdate();
        }

        private async void ScanButton_Click(object sender, EventArgs e)
        {
            if (driveComboBox.SelectedItem == null) return;

            if (_cancellationTokenSource != null)
            {
                // Cancel ongoing scan
                _cancellationTokenSource.Cancel();
                scanButton.Text = "Scan Drive";
                progressBar.Style = ProgressBarStyle.Blocks;
                return;
            }

            // Reset statistics
            _fileTypeStats.Clear();
            _totalScannedSize = 0;
            _totalFiles = 0;
            _totalDirectories = 0;

            scanButton.Text = "Cancel";
            treeView.Nodes.Clear();
            progressBar.Style = ProgressBarStyle.Marquee;
            string selectedDrive = driveComboBox.SelectedItem.ToString()!.Split(' ')[0];

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                await ScanDirectoryOptimized(selectedDrive, treeView.Nodes, _cancellationTokenSource.Token);
                UpdateStatistics();
                UpdateFileTypeChart();
                statusLabel.Text = "Scan completed";
            }
            catch (OperationCanceledException)
            {
                statusLabel.Text = "Scan cancelled";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error scanning directory: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                scanButton.Text = "Scan Drive";
                progressBar.Style = ProgressBarStyle.Blocks;
            }
        }

        private async Task ScanDirectoryOptimized(string path, TreeNodeCollection parentNode, CancellationToken cancellationToken)
        {
            var rootDirectory = new DirectoryInfo(path);
            var rootNode = parentNode.Add(rootDirectory.Name);
            rootNode.ImageKey = "folder";
            rootNode.SelectedImageKey = "folder";
            
            var directoryNodes = new ConcurrentDictionary<string, TreeNode>();
            directoryNodes[rootDirectory.FullName] = rootNode;
            _folderStats[rootDirectory.FullName] = new FolderStats();

            try
            {
                var directoryQueue = new ConcurrentQueue<DirectoryInfo>();
                directoryQueue.Enqueue(rootDirectory);
                UpdateStatusLabel($"Starting scan of {path}");

                while (directoryQueue.TryDequeue(out var currentDir))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await Task.Run(() =>
                    {
                        try
                        {
                            UpdateStatusLabel($"Scanning: {currentDir.FullName}");
                            var currentFolderStats = _folderStats[currentDir.FullName];
                            
                            // Process files
                            var files = currentDir.GetFiles()
                                .OrderByDescending(f => f.Length)
                                .Take(_settings.MaxDisplayedItems);

                            var fileItems = new List<FileSystemItem>();
                            foreach (var file in files)
                            {
                                var fileType = FileSystemHelper.GetFileType(file.Extension);
                                
                                // Update folder stats
                                currentFolderStats.TotalSize += file.Length;
                                currentFolderStats.FileCount++;
                                
                                if (!currentFolderStats.FileTypeStats.ContainsKey(fileType))
                                    currentFolderStats.FileTypeStats[fileType] = 0;
                                currentFolderStats.FileTypeStats[fileType] += file.Length;

                                // Update global stats
                                lock (_fileTypeStats)
                                {
                                    if (!_fileTypeStats.ContainsKey(fileType))
                                        _fileTypeStats[fileType] = 0;
                                    _fileTypeStats[fileType] += file.Length;
                                }

                                fileItems.Add(new FileSystemItem
                                {
                                    Name = file.Name,
                                    FullPath = file.FullName,
                                    Size = file.Length,
                                    Created = file.CreationTime,
                                    Modified = file.LastWriteTime,
                                    Accessed = file.LastAccessTime,
                                    Attributes = file.Attributes,
                                    Extension = file.Extension,
                                    IsDirectory = false,
                                    FileType = fileType
                                });

                                Interlocked.Increment(ref _totalFiles);
                                Interlocked.Add(ref _totalScannedSize, file.Length);
                            }

                            // Add files to their directory node
                            this.Invoke(() =>
                            {
                                if (directoryNodes.TryGetValue(currentDir.FullName, out var dirNode))
                                {
                                    foreach (var item in fileItems)
                                    {
                                        var node = dirNode.Nodes.Add($"{item.Name} ({FileSystemHelper.FormatSize(item.Size)})");
                                        node.Tag = item;
                                        node.ImageKey = GetIconKey(item.FullPath);
                                        node.SelectedImageKey = node.ImageKey;
                                    }
                                }
                            });

                            // Process subdirectories
                            var subDirs = currentDir.GetDirectories();
                            foreach (var subDir in subDirs)
                            {
                                try
                                {
                                    if (ShouldIncludeDirectory(subDir))
                                    {
                                        _folderStats[subDir.FullName] = new FolderStats();
                                        currentFolderStats.SubfolderCount++;

                                        // Create node for this directory
                                        this.Invoke(() =>
                                        {
                                            var parentDirNode = directoryNodes[subDir.Parent!.FullName];
                                            var dirNode = parentDirNode.Nodes.Add(subDir.Name);
                                            dirNode.ImageKey = "folder";
                                            dirNode.SelectedImageKey = "folder";
                                            directoryNodes[subDir.FullName] = dirNode;
                                        });

                                        directoryQueue.Enqueue(subDir);
                                        Interlocked.Increment(ref _totalDirectories);
                                    }
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    Debug.WriteLine($"Access denied to directory: {subDir.FullName}");
                                }
                            }

                            // Update parent folder sizes
                            if (currentDir.Parent != null && _folderStats.ContainsKey(currentDir.Parent.FullName))
                            {
                                var parentStats = _folderStats[currentDir.Parent.FullName];
                                parentStats.TotalSize += currentFolderStats.TotalSize;
                            }
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            Debug.WriteLine($"Access denied to {currentDir.FullName}: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error scanning {currentDir.FullName}: {ex.Message}");
                        }
                    });

                    // Update UI periodically
                    if (_totalFiles % 100 == 0)
                    {
                        UpdateStatisticsSafe();
                        UpdateFolderNodes(directoryNodes);
                    }
                }

                UpdateFolderNodes(directoryNodes);
                Debug.WriteLine($"Scan completed. Total files: {_totalFiles}, Total size: {FileSystemHelper.FormatSize(_totalScannedSize)}");

                this.Invoke(() =>
                {
                    rootNode.Expand();
                    UpdateStatisticsSafe();
                    UpdateStatusLabel($"Scan completed. Found {_totalFiles:N0} files in {_totalDirectories:N0} directories.");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in scan process: {ex.Message}");
                this.Invoke(() => rootNode.Text += $" (Error: {ex.Message})");
            }
        }

        private void UpdateStatusLabel(string text)
        {
            if (statusLabel.InvokeRequired)
            {
                statusLabel.Invoke(() => statusLabel.Text = text);
            }
            else
            {
                statusLabel.Text = text;
            }
        }

        private void UpdateStatisticsSafe()
        {
            if (statisticsLabel.InvokeRequired)
            {
                statisticsLabel.Invoke(UpdateStatistics);
            }
            else
            {
                UpdateStatistics();
            }
        }

        private bool ShouldIncludeFile(FileInfo file)
        {
            try
            {
                // Debug logging
                Debug.WriteLine($"Checking file: {file.Name}, Size: {FileSystemHelper.FormatSize(file.Length)}");

                if (!_settings.ShowHiddenFiles && file.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    Debug.WriteLine($"Skipping hidden file: {file.Name}");
                    return false;
                }

                if (!_settings.ShowSystemFiles && file.Attributes.HasFlag(FileAttributes.System))
                {
                    Debug.WriteLine($"Skipping system file: {file.Name}");
                    return false;
                }

                if (!string.IsNullOrEmpty(extensionFilterBox.Text))
                {
                    var filter = extensionFilterBox.Text.Trim('*', '.');
                    if (!file.Extension.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"Skipping file due to extension filter: {file.Name}");
                        return false;
                    }
                }

                var minSize = sizeFilterCombo.SelectedItem?.ToString() switch
                {
                    ">1GB" => 1024L * 1024 * 1024,
                    ">100MB" => 100L * 1024 * 1024,
                    ">10MB" => 10L * 1024 * 1024,
                    ">1MB" => 1024L * 1024,
                    _ => 0L
                };

                if (file.Length < minSize)
                {
                    Debug.WriteLine($"Skipping file due to size filter: {file.Name}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking file {file.Name}: {ex.Message}");
                return false;
            }
        }

        private bool ShouldIncludeDirectory(DirectoryInfo dir)
        {
            if (!_settings.ShowHiddenFiles && dir.Attributes.HasFlag(FileAttributes.Hidden))
                return false;

            if (!_settings.ShowSystemFiles && dir.Attributes.HasFlag(FileAttributes.System))
                return false;

            return true;
        }

        private void UpdateFileTypeChart()
        {
            fileTypeChart.Series["FileTypes"].Points.Clear();

            var sortedStats = _fileTypeStats
                .OrderByDescending(kvp => kvp.Value)
                .Take(10);

            foreach (var stat in sortedStats)
            {
                var point = fileTypeChart.Series["FileTypes"].Points.Add(stat.Value);
                point.LegendText = stat.Key;
                point.Label = $"{stat.Key}\n{FileSystemHelper.FormatSize(stat.Value)}";
            }
        }

        private void UpdateStatistics()
        {
            statisticsLabel.Text = $@"Scan Statistics:
Total Size: {FileSystemHelper.FormatSize(_totalScannedSize)}
Files Found: {_totalFiles:N0}
Directories Processed: {_totalDirectories:N0}
File Types: {_fileTypeStats.Count:N0}";
        }

        private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is FileSystemItem item)
            {
                fileInfoLabel.Text = $@"File Details:
Name: {item.Name}
Size: {FileSystemHelper.FormatSize(item.Size)}
Type: {item.FileType}
Created: {item.Created}
Modified: {item.Modified}
Last Accessed: {item.Accessed}
Attributes: {item.Attributes}
Full Path: {item.FullPath}";
            }
        }

        private void SearchNodes(TreeNodeCollection nodes, string searchText)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is FileSystemItem item)
                {
                    bool matches = item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                                  item.FileType.Contains(searchText, StringComparison.OrdinalIgnoreCase);

                    node.BackColor = matches ? Color.Yellow : Color.White;
                    node.ForeColor = _settings.DarkMode ? Color.White : Color.Black;
                }

                SearchNodes(node.Nodes, searchText);
            }
        }

        private void OpenSelectedFile()
        {
            if (treeView.SelectedNode?.Tag is FileSystemItem item)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.FullPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OpenContainingFolder()
        {
            if (treeView.SelectedNode?.Tag is FileSystemItem item)
            {
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{item.FullPath}\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteSelectedFile()
        {
            if (treeView.SelectedNode?.Tag is FileSystemItem item)
            {
                try
                {
                    var result = MessageBox.Show(
                        $"Are you sure you want to delete:\n{item.FullPath}",
                        "Confirm Delete",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.Yes)
                    {
                        File.Delete(item.FullPath);
                        treeView.SelectedNode.Remove();
                        UpdateStatistics();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void CopySelectedPath()
        {
            if (treeView.SelectedNode?.Tag is FileSystemItem item)
            {
                Clipboard.SetText(item.FullPath);
            }
        }

        private void ShowFileProperties()
        {
            if (treeView.SelectedNode?.Tag is FileSystemItem item)
            {
                Process.Start("explorer.exe", $"/properties \"{item.FullPath}\"");
            }
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            using var settingsForm = new SettingsForm(_settings);
            if (settingsForm.ShowDialog() == DialogResult.OK)
            {
                ApplyTheme();
            }
        }

        private void ApplyTheme()
        {
            if (_settings.DarkMode)
            {
                this.BackColor = Color.FromArgb(32, 32, 32);
                this.ForeColor = Color.White;
                treeView.BackColor = Color.FromArgb(48, 48, 48);
                treeView.ForeColor = Color.White;
                fileInfoPanel.BackColor = Color.FromArgb(48, 48, 48);
                statisticsPanel.BackColor = Color.FromArgb(48, 48, 48);
            }
            else
            {
                this.BackColor = SystemColors.Control;
                this.ForeColor = SystemColors.ControlText;
                treeView.BackColor = SystemColors.Window;
                treeView.ForeColor = SystemColors.ControlText;
                fileInfoPanel.BackColor = SystemColors.Window;
                statisticsPanel.BackColor = SystemColors.Control;
            }
        }

        private void LoadDrives()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo drive in drives)
            {
                if (drive.IsReady)
                {
                    string driveInfo = $"{drive.Name} ({FileSystemHelper.FormatSize(drive.TotalSize)})";
                    driveComboBox.Items.Add(driveInfo);
                }
            }
            if (driveComboBox.Items.Count > 0)
                driveComboBox.SelectedIndex = 0;
        }

        private string GetIconKey(string filePath)
        {
            try
            {
                string extension = Path.GetExtension(filePath).ToLower();
                if (!_iconList.Images.ContainsKey(extension))
                {
                    using var icon = Icon.ExtractAssociatedIcon(filePath);
                    if (icon != null)
                    {
                        _iconList.Images.Add(extension, icon.ToBitmap());
                    }
                }
                return extension;
            }
            catch
            {
                return "file";
            }
        }

        private void TreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.ImageKey == "folder")
            {
                e.Node.ImageKey = "folderopen";
                e.Node.SelectedImageKey = "folderopen";
            }
        }

        private void UpdateFolderNodes(ConcurrentDictionary<string, TreeNode> directoryNodes)
        {
            this.Invoke(() =>
            {
                foreach (var kvp in directoryNodes)
                {
                    if (_folderStats.TryGetValue(kvp.Key, out var stats))
                    {
                        var node = kvp.Value;
                        node.Text = $"{Path.GetFileName(kvp.Key)} ({FileSystemHelper.FormatSize(stats.TotalSize)})";
                    }
                }
            });
        }

        private void UpdateBreadcrumb(TreeNode node)
        {
            breadcrumbBar.Controls.Clear();
            var path = new List<TreeNode>();
            
            var current = node;
            while (current != null)
            {
                path.Insert(0, current);
                current = current.Parent;
            }

            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0)
                {
                    breadcrumbBar.Controls.Add(new Label { Text = " > ", AutoSize = true });
                }

                var pathNode = path[i];
                var link = new LinkLabel
                {
                    Text = pathNode.Text,
                    AutoSize = true,
                    Tag = pathNode
                };

                link.LinkClicked += (s, e) =>
                {
                    var targetNode = (TreeNode)((LinkLabel)s).Tag;
                    treeView.SelectedNode = targetNode;
                };

                breadcrumbBar.Controls.Add(link);
            }
        }

        private void InitializePreviewPanel()
        {
            previewPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 300,
                BorderStyle = BorderStyle.FixedSingle
            };

            previewPicture = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Visible = false
            };

            previewText = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Visible = false
            };

            previewPanel.Controls.AddRange(new Control[] { previewPicture, previewText });
            mainSplitContainer.Panel2.Controls.Add(previewPanel);

            treeView.AfterSelect += UpdatePreview;
        }

        private void UpdatePreview(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is not FileSystemItem item)
            {
                previewPicture.Visible = false;
                previewText.Visible = false;
                return;
            }

            try
            {
                string extension = Path.GetExtension(item.FullPath).ToLower();
                
                // Image preview
                if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" }.Contains(extension))
                {
                    using var stream = new FileStream(item.FullPath, FileMode.Open, FileAccess.Read);
                    previewPicture.Image = Image.FromStream(stream);
                    previewPicture.Visible = true;
                    previewText.Visible = false;
                }
                // Text preview
                else if (new[] { ".txt", ".log", ".xml", ".json", ".cs", ".html", ".css", ".js" }.Contains(extension))
                {
                    previewText.Text = File.ReadAllText(item.FullPath);
                    previewText.Visible = true;
                    previewPicture.Visible = false;
                }
                else
                {
                    previewPicture.Visible = false;
                    previewText.Visible = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading preview: {ex.Message}");
                previewPicture.Visible = false;
                previewText.Visible = false;
            }
        }

        private void ExportToCSV()
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                Title = "Export Scan Results"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using var writer = new StreamWriter(saveDialog.FileName);
                    writer.WriteLine("Name,Path,Size,Type,Created,Modified,Accessed");

                    void ExportNode(TreeNode node)
                    {
                        if (node.Tag is FileSystemItem item)
                        {
                            writer.WriteLine($"\"{item.Name}\",\"{item.FullPath}\",{item.Size},\"{item.FileType}\",\"{item.Created}\",\"{item.Modified}\",\"{item.Accessed}\"");
                        }
                        foreach (TreeNode child in node.Nodes)
                        {
                            ExportNode(child);
                        }
                    }

                    foreach (TreeNode node in treeView.Nodes)
                    {
                        ExportNode(node);
                    }

                    MessageBox.Show("Export completed successfully!", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportToJSON()
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Export Scan Results"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var data = new
                    {
                        ScanDate = DateTime.Now,
                        TotalSize = _totalScannedSize,
                        TotalFiles = _totalFiles,
                        TotalDirectories = _totalDirectories,
                        FileTypes = _fileTypeStats,
                        Items = GetAllItems()
                    };

                    string json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    File.WriteAllText(saveDialog.FileName, json);

                    MessageBox.Show("Export completed successfully!", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportReport()
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "HTML files (*.html)|*.html",
                Title = "Export Scan Report"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("<!DOCTYPE html>");
                    sb.AppendLine("<html><head><style>");
                    sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
                    sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
                    sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
                    sb.AppendLine("th { background-color: #f2f2f2; }");
                    sb.AppendLine("</style></head><body>");

                    sb.AppendLine("<h1>Disk Space Analysis Report</h1>");
                    sb.AppendLine($"<p>Generated: {DateTime.Now}</p>");
                    
                    sb.AppendLine("<h2>Summary</h2>");
                    sb.AppendLine("<ul>");
                    sb.AppendLine($"<li>Total Size: {FileSystemHelper.FormatSize(_totalScannedSize)}</li>");
                    sb.AppendLine($"<li>Total Files: {_totalFiles:N0}</li>");
                    sb.AppendLine($"<li>Total Directories: {_totalDirectories:N0}</li>");
                    sb.AppendLine("</ul>");

                    sb.AppendLine("<h2>File Types</h2>");
                    sb.AppendLine("<table>");
                    sb.AppendLine("<tr><th>Type</th><th>Size</th><th>Percentage</th></tr>");
                    foreach (var type in _fileTypeStats.OrderByDescending(x => x.Value))
                    {
                        var percentage = (double)type.Value / _totalScannedSize * 100;
                        sb.AppendLine($"<tr><td>{type.Key}</td><td>{FileSystemHelper.FormatSize(type.Value)}</td><td>{percentage:F1}%</td></tr>");
                    }
                    sb.AppendLine("</table>");

                    sb.AppendLine("</body></html>");
                    File.WriteAllText(saveDialog.FileName, sb.ToString());

                    MessageBox.Show("Report generated successfully!", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error generating report: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private List<FileSystemItem> GetAllItems()
        {
            var items = new List<FileSystemItem>();

            void CollectItems(TreeNode node)
            {
                if (node.Tag is FileSystemItem item)
                {
                    items.Add(item);
                }
                foreach (TreeNode child in node.Nodes)
                {
                    CollectItems(child);
                }
            }

            foreach (TreeNode node in treeView.Nodes)
            {
                CollectItems(node);
            }

            return items;
        }

        private async Task FindDuplicateFiles()
        {
            var duplicates = new ConcurrentDictionary<string, List<FileSystemItem>>();
            var progress = new Progress<string>(status => UpdateStatusLabel(status));
            
            try
            {
                await Task.Run(() =>
                {
                    var files = GetAllItems()
                        .Where(item => !item.IsDirectory)
                        .GroupBy(item => item.Size)
                        .Where(g => g.Count() > 1)
                        .SelectMany(g => g);

                    int processed = 0;
                    int total = files.Count();

                    foreach (var file in files)
                    {
                        processed++;
                        ((IProgress<string>)progress).Report($"Checking file {processed}/{total}: {file.Name}");

                        try
                        {
                            using var stream = File.OpenRead(file.FullPath);
                            using var md5 = MD5.Create();
                            var hash = Convert.ToBase64String(md5.ComputeHash(stream));

                            duplicates.AddOrUpdate(
                                hash,
                                new List<FileSystemItem> { file },
                                (_, list) => { list.Add(file); return list; }
                            );
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error processing file {file.FullPath}: {ex.Message}");
                        }
                    }
                });

                // Show results in a new form
                var duplicateGroups = duplicates.Where(kvp => kvp.Value.Count > 1)
                                              .Select(kvp => kvp.Value)
                                              .ToList();

                if (duplicateGroups.Any())
                {
                    using var resultForm = new Form
                    {
                        Text = "Duplicate Files",
                        Size = new Size(800, 600),
                        StartPosition = FormStartPosition.CenterParent
                    };

                    var listView = new ListView
                    {
                        Dock = DockStyle.Fill,
                        View = View.Details,
                        FullRowSelect = true,
                        GridLines = true
                    };

                    listView.Columns.AddRange(new[]
                    {
                        new ColumnHeader { Text = "File Name", Width = 200 },
                        new ColumnHeader { Text = "Path", Width = 300 },
                        new ColumnHeader { Text = "Size", Width = 100 }
                    });

                    foreach (var group in duplicateGroups)
                    {
                        var groupItem = new ListViewGroup($"Size: {FileSystemHelper.FormatSize(group[0].Size)}");
                        listView.Groups.Add(groupItem);

                        foreach (var file in group)
                        {
                            var item = new ListViewItem(new[]
                            {
                                file.Name,
                                file.FullPath,
                                FileSystemHelper.FormatSize(file.Size)
                            })
                            {
                                Group = groupItem,
                                Tag = file
                            };
                            listView.Items.Add(item);
                        }
                    }

                    resultForm.Controls.Add(listView);
                    resultForm.ShowDialog(this);
                }
                else
                {
                    MessageBox.Show("No duplicate files found.", "Duplicate Files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error finding duplicates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UpdateStatusLabel("Ready");
            }
        }
    }
}