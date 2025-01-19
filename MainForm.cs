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
    using System.Drawing;
    using ImageMagick;

    public partial class MainForm : Form
    {
        private Panel? topPanel;
        private TreeView? treeView;
        private Button? scanButton;
        private ComboBox? driveComboBox;
        private Label? statusLabel;
        private ProgressBar? progressBar;
        private Panel? fileInfoPanel;
        private Label? fileInfoLabel;
        private Chart? fileTypeChart;
        private Panel? statisticsPanel;
        private Label? statisticsLabel;
        private TextBox? searchBox;
        private Panel? filterPanel;
        private ComboBox? sizeFilterCombo;
        private TextBox? extensionFilterBox;
        private Button? settingsButton;
        private SplitContainer? mainSplitContainer;
        private SplitContainer? rightSplitContainer;
        private FlowLayoutPanel? breadcrumbBar;
        private Panel? previewPanel;
        private PictureBox? previewPicture;
        private TextBox? previewText;
        private Label? loadingLabel;
        private Button? timelineButton;
        private Button? diskTrendsButton;
        private Button? duplicatesButton;

        private CancellationTokenSource? _cancellationTokenSource;
        private AppSettings _settings;
        private Dictionary<string, long> _fileTypeStats;
        private long _totalScannedSize;
        private int _totalFiles;
        private int _totalDirectories;

        private ImageList? _iconList;
        private ConcurrentDictionary<string, FolderStats> _folderStats = new();

        private ListView? fileTypeListView;

        private Button? browseButton;

        private DateTime _scanStartTime;
        private long _totalBytesToScan;

        private Panel? statusPanel;

        private Label? versionLabel;

        public MainForm()
        {
            InitializeComponent();
            _settings = AppSettings.Load();
            _fileTypeStats = new Dictionary<string, long>();
            InitializeMainLayout();
            InitializeControls();
            LoadDrives();
            ApplyTheme();
            this.Text = $"Disk Space Analyzer {AppVersion.VersionString}";
            this.WindowState = FormWindowState.Maximized;
        }

        private void InitializeMainLayout()
        {
            mainSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Panel1MinSize = 100,
                Panel2MinSize = 100
            };

            rightSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Panel1MinSize = 100,
                Panel2MinSize = 100
            };

            mainSplitContainer.Panel2.Controls.Add(rightSplitContainer);
            this.Controls.Add(mainSplitContainer);

            this.Load += (s, e) => {
                if (mainSplitContainer.Width > 400)
                    mainSplitContainer.SplitterDistance = mainSplitContainer.Width / 4;  // 25% for tree view
                if (rightSplitContainer.Height > 400)
                    rightSplitContainer.SplitterDistance = rightSplitContainer.Height / 3;  // 33% for top panel
            };
        }

        private void InitializeControls()
        {
            InitializeTopPanel();
            InitializeTreeView();
            InitializeBreadcrumbBar();
            InitializeFileInfoPanel();
            InitializeStatisticsPanel();
            InitializeFileTypePanel();
            InitializeFilterPanel();
            InitializeSearchBox();
            InitializeContextMenu();
            
            // Initial text
            if (statisticsLabel != null)
                statisticsLabel.Text = "Click 'Browse' to select a folder or choose a drive to scan";

            if (fileInfoLabel != null)
                fileInfoLabel.Text = "Select a file or folder to view its details";

            if (previewText != null)
            {
                previewText.Visible = true;
                previewText.Text = "File preview will appear here when you select a supported file";
            }

            if (treeView != null)
            {
                var welcomeNode = new TreeNode("Welcome to Disk Space Analyzer");
                welcomeNode.Nodes.Add("Select a drive or browse for a folder to begin scanning");
                treeView.Nodes.Add(welcomeNode);
            }

            versionLabel = new Label
            {
                Text = AppVersion.VersionString,
                AutoSize = true,
                Location = new Point(1100, 12),
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8f)
            };
        }

        private void InitializeTopPanel()
        {
            topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(5)
            };

            driveComboBox = new ComboBox
            {
                Width = 200,
                Location = new Point(5, 8)
            };

            scanButton = new Button
            {
                Text = "Scan",
                Width = 80,
                Height = 30,
                Location = new Point(215, 5)
            };
            scanButton.Click += ScanButton_Click;

            browseButton = new Button
            {
                Text = "Browse",
                Width = 80,
                Height = 30,
                Location = new Point(300, 5)
            };
            browseButton.Click += BrowseButton_Click;

            duplicatesButton = new Button
            {
                Text = "Find Duplicates",
                Width = 100,
                Height = 30,
                Location = new Point(385, 5)
            };
            duplicatesButton.Click += async (s, e) => await FindDuplicateFiles();

            timelineButton = new Button
            {
                Text = "Timeline",
                Width = 80,
                Height = 30,
                Location = new Point(490, 5),
                Enabled = false
            };

            diskTrendsButton = new Button
            {
                Text = "Disk Trends",
                Width = 80,
                Height = 30,
                Location = new Point(575, 5),
                Enabled = false
            };

            settingsButton = new Button
            {
                Text = "⚙️ Settings",
                Width = 80,
                Height = 30,
                Location = new Point(660, 5)
            };
            settingsButton.Click += SettingsButton_Click;

            progressBar = new ProgressBar
            {
                Width = 150,
                Height = 20,
                Location = new Point(745, 10),
                Style = ProgressBarStyle.Blocks
            };

            statusLabel = new Label
            {
                Text = "Ready",
                AutoSize = true,
                Location = new Point(900, 12)
            };

            topPanel.Controls.AddRange(new Control[] 
            { 
                driveComboBox,
                scanButton,
                browseButton,
                duplicatesButton,
                timelineButton,
                diskTrendsButton,
                settingsButton,
                progressBar,
                statusLabel
            });

            this.Controls.Add(topPanel);
            LoadDrives();
        }

        private void InitializeTreeView()
        {
            _iconList = new ImageList();
            _iconList.ColorDepth = ColorDepth.Depth32Bit;
            _iconList.ImageSize = new Size(16, 16);
            
            _iconList.Images.Add("folder", SystemIcons.Shield.ToBitmap());
            _iconList.Images.Add("folderopen", SystemIcons.Application.ToBitmap());
            _iconList.Images.Add("file", SystemIcons.WinLogo.ToBitmap());
            
            treeView = new TreeView
            {
                Dock = DockStyle.Fill,
                ShowNodeToolTips = true,
                ImageList = _iconList,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.None,
                ShowLines = true,
                Indent = 20,
                ItemHeight = 22 // Increased for better readability
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
            Debug.WriteLine("Initializing File Info Panel");
            
            // Create a split container for file info and preview
            var fileInfoSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Panel1MinSize = 100,
                Panel2MinSize = 100
            };

            // Clear any existing controls
            if (rightSplitContainer?.Panel1 != null)
                rightSplitContainer.Panel1.Controls.Clear();

            // Add labels to identify panels
            var panel1Label = new Label
            {
                Text = "File Details",
                Dock = DockStyle.Top,
                BackColor = Color.LightBlue,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 25
            };
            fileInfoSplitContainer.Panel1.Controls.Add(panel1Label);

            var panel2Label = new Label
            {
                Text = "File Preview",
                Dock = DockStyle.Top,
                BackColor = Color.LightGreen,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 25
            };
            fileInfoSplitContainer.Panel2.Controls.Add(panel2Label);

            // Panel 1 - File Info
            fileInfoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(10)
            };

            fileInfoLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.White
            };

            fileInfoPanel.Controls.Add(fileInfoLabel);
            fileInfoSplitContainer.Panel1.Controls.Add(fileInfoPanel);

            // Panel 2 - Preview
            var previewContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None
            };

            previewPicture = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                Visible = false,
                BorderStyle = BorderStyle.Fixed3D
            };

            previewText = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Visible = false,
                Font = new Font("Segoe UI", 9F)
            };

            loadingLabel = new Label
            {
                Text = "⌛ Loading preview...",
                AutoSize = false,
                Font = new Font("Segoe UI", 12f),
                Visible = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            // Add preview controls to previewContainer
            previewContainer.Controls.AddRange(new Control[] { previewPicture, previewText, loadingLabel });
            fileInfoSplitContainer.Panel2.Controls.Add(previewContainer);

            Debug.WriteLine("Adding fileInfoSplitContainer to rightSplitContainer.Panel1");
            rightSplitContainer.Panel1.Controls.Add(fileInfoSplitContainer);

            // Initial text
            fileInfoLabel.Text = "Select a file or folder to view its details";
            previewText.Text = "File preview will appear here when you select a supported file";
            previewText.Visible = true;

            // Make sure preview updates work
            if (treeView != null)
            {
                Debug.WriteLine("Setting up TreeView preview handler");
                treeView.AfterSelect -= UpdatePreview;
                treeView.AfterSelect += UpdatePreview;
            }
        }

        private void InitializeFileTypePanel()
        {
            Debug.WriteLine("Initializing File Type Panel");
            
            // Clear any existing controls
            if (rightSplitContainer?.Panel2 != null)
                rightSplitContainer.Panel2.Controls.Clear();

            var panel3Label = new Label
            {
                Text = "File Type Statistics",
                Dock = DockStyle.Top,
                BackColor = Color.LightPink,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 25
            };

            fileTypeListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Sorting = SortOrder.Descending,
                Font = new Font("Segoe UI", 9F)
            };

            fileTypeListView.Columns.AddRange(new[]
            {
                new ColumnHeader { Text = "File Type", Width = 100 },
                new ColumnHeader { Text = "Size", Width = 100 },
                new ColumnHeader { Text = "Count", Width = 80 },
                new ColumnHeader { Text = "Percentage", Width = 100 },
                new ColumnHeader { Text = "Avg. File Size", Width = 100 },
                new ColumnHeader { Text = "Last Modified", Width = 120 }
            });

            // Add context menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Copy to Clipboard", null, (s, e) => CopyFileTypeStats()),
                new ToolStripMenuItem("Export to CSV...", null, (s, e) => ExportFileTypeStats()),
                new ToolStripSeparator(),
                new ToolStripMenuItem("Sort by Size", null, (s, e) => fileTypeListView.Sort()),
                new ToolStripMenuItem("Filter Large Files", null, (s, e) => FilterLargeFiles())
            });
            fileTypeListView.ContextMenuStrip = contextMenu;

            // Color coding based on file size
            fileTypeListView.DrawItem += (s, e) => 
            {
                if (e.Item == null) return;
                var percentage = double.Parse(e.Item.SubItems[3].Text.TrimEnd('%'));
                
                if (percentage > 10)
                    e.Item.BackColor = Color.LightPink;
                else if (percentage > 5)
                    e.Item.BackColor = Color.LightYellow;
            };

            // Add initial "no data" message
            var item = new ListViewItem(new[] { "No scan data", "-", "-", "-", "-", "-" });
            fileTypeListView.Items.Add(item);

            Debug.WriteLine("Adding controls to rightSplitContainer.Panel2");
            rightSplitContainer.Panel2.Controls.Add(panel3Label);
            rightSplitContainer.Panel2.Controls.Add(fileTypeListView);
            
            if (statisticsPanel != null)
            {
                statisticsPanel.Dock = DockStyle.Bottom;
                rightSplitContainer.Panel2.Controls.Add(statisticsPanel);
            }
        }

        private void InitializeStatisticsPanel()
        {
            statisticsPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 100,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10)
            };

            statisticsLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.White
            };

            if (statisticsPanel != null && statisticsLabel != null)
                statisticsPanel.Controls.Add(statisticsLabel);
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
            if (string.IsNullOrEmpty(driveComboBox?.Text)) return;

            ResetPanels();

            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = null;
                if (scanButton != null) scanButton.Text = "Scan";
                if (progressBar != null) progressBar.Style = ProgressBarStyle.Blocks;
                return;
            }

            // Reset statistics
            _fileTypeStats.Clear();
            _totalScannedSize = 0;
            _totalFiles = 0;
            _totalDirectories = 0;
            _scanStartTime = DateTime.Now;

            if (scanButton != null) scanButton.Text = "Cancel";
            if (treeView != null) treeView.Nodes.Clear();
            if (progressBar != null) 
            {
                progressBar.Value = 0;
                progressBar.Style = ProgressBarStyle.Marquee;
            }
            
            string selectedPath = driveComboBox.Text.Contains("(") 
                ? driveComboBox.Text.Split(' ')[0]  // Takes "D:" from "D: (800 MB)"
                : driveComboBox.Text;  // Use full path for folders

            // Get total size to scan
            try
            {
                var driveInfo = new DriveInfo(selectedPath);
                if (driveInfo.IsReady)
                {
                    _totalBytesToScan = driveInfo.TotalSize - driveInfo.TotalFreeSpace;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting drive info: {ex.Message}");
                _totalBytesToScan = 0;
            }

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                if (treeView != null) await ScanDirectoryOptimized(selectedPath, treeView.Nodes, _cancellationTokenSource.Token);
                UpdateStatistics();
                UpdateFileTypeStats();
                if (statusLabel != null) statusLabel.Text = "Scan completed";
                if (progressBar != null) 
                {
                    progressBar.Style = ProgressBarStyle.Blocks;
                    progressBar.Value = 100;
                }
            }
            catch (OperationCanceledException)
            {
                if (statusLabel != null) statusLabel.Text = "Scan cancelled";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error scanning directory: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                if (scanButton != null) scanButton.Text = "Scan";
                if (progressBar != null) progressBar.Style = ProgressBarStyle.Blocks;
            }
        }

        private void UpdateProgress()
        {
            if (progressBar == null || _totalBytesToScan == 0) return;

            var progress = (int)((_totalScannedSize * 100.0) / _totalBytesToScan);
            progress = Math.Min(99, progress); // Never show 100% until completely done

            var elapsedTime = DateTime.Now - _scanStartTime;
            var estimatedTotalTime = TimeSpan.FromTicks((long)(elapsedTime.Ticks / (_totalScannedSize / (double)_totalBytesToScan)));
            var remainingTime = estimatedTotalTime - elapsedTime;

            this.Invoke(() =>
            {
                progressBar.Value = progress;
                statusLabel.Text = $"Scanned {_totalFiles:N0} files ({FileSystemHelper.FormatSize(_totalScannedSize)}) - " +
                                  $"Est. {remainingTime.Minutes:D2}:{remainingTime.Seconds:D2} remaining";
            });
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

                    // Update status periodically
                    if (_totalFiles % 100 == 0)
                    {
                        UpdateProgress();
                        UpdateStatisticsSafe();
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
            if (statusLabel == null) return;
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

        private void UpdateFileTypeStats()
        {
            if (fileTypeListView == null) return;
            
            fileTypeListView.BeginUpdate();
            fileTypeListView.Items.Clear();
            var items = new List<ListViewItem>();

            var fileTypeInfo = new Dictionary<string, (long TotalSize, int Count, DateTime LastModified)>();

            // Gather detailed information
            foreach (var item in GetAllItems().Where(i => !i.IsDirectory))
            {
                var ext = Path.GetExtension(item.Name).ToLowerInvariant();
                if (!fileTypeInfo.ContainsKey(ext))
                    fileTypeInfo[ext] = (item.Size, 1, item.Modified);
                else
                {
                    var current = fileTypeInfo[ext];
                    fileTypeInfo[ext] = (current.TotalSize + item.Size, 
                                       current.Count + 1, 
                                       item.Modified > current.LastModified ? item.Modified : current.LastModified);
                }
            }

            foreach (var type in fileTypeInfo.OrderByDescending(x => x.Value.TotalSize))
            {
                var percentage = (double)type.Value.TotalSize / _totalScannedSize * 100;
                var avgSize = type.Value.Count > 0 ? type.Value.TotalSize / type.Value.Count : 0;
                
                var item = new ListViewItem(new[]
                {
                    string.IsNullOrEmpty(type.Key) ? "(no extension)" : type.Key,
                    FileSystemHelper.FormatSize(type.Value.TotalSize),
                    type.Value.Count.ToString("N0"),
                    $"{percentage:F1}%",
                    FileSystemHelper.FormatSize(avgSize),
                    type.Value.LastModified.ToString("g")
                });
                
                items.Add(item);
            }

            fileTypeListView.Items.AddRange(items.ToArray());
            fileTypeListView.EndUpdate();
        }

        private void UpdateStatistics()
        {
            if (statisticsLabel == null) return;

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
            if (treeView?.SelectedNode?.Tag is FileSystemItem item)
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
                // Dark theme colors
                var darkBackground = Color.FromArgb(32, 32, 32);
                var darkSecondary = Color.FromArgb(45, 45, 48);
                var darkBorder = Color.FromArgb(60, 60, 60);
                var darkText = Color.FromArgb(240, 240, 240);
                var accentColor = Color.FromArgb(0, 122, 204);  // Blue accent

                // Main form
                this.BackColor = darkBackground;
                this.ForeColor = darkText;

                // TreeView
                if (treeView != null)
                {
                    treeView.BackColor = darkSecondary;
                    treeView.ForeColor = darkText;
                    treeView.LineColor = darkBorder;
                }

                // File info panel
                if (fileInfoPanel != null)
                {
                    fileInfoPanel.BackColor = darkSecondary;
                    fileInfoPanel.ForeColor = darkText;
                }

                if (fileInfoLabel != null)
                {
                    fileInfoLabel.BackColor = darkSecondary;
                    fileInfoLabel.ForeColor = darkText;
                }

                // Preview controls
                if (previewText != null)
                {
                    previewText.BackColor = darkSecondary;
                    previewText.ForeColor = darkText;
                }

                // Statistics panel
                if (statisticsPanel != null)
                {
                    statisticsPanel.BackColor = darkSecondary;
                    statisticsPanel.ForeColor = darkText;
                }

                if (statisticsLabel != null)
                {
                    statisticsLabel.BackColor = darkSecondary;
                    statisticsLabel.ForeColor = darkText;
                }

                // File type list
                if (fileTypeListView != null)
                {
                    fileTypeListView.BackColor = darkSecondary;
                    fileTypeListView.ForeColor = darkText;
                }

                // Top panel controls
                if (topPanel != null)
                {
                    topPanel.BackColor = darkBackground;
                }

                // ComboBox
                if (driveComboBox != null)
                {
                    driveComboBox.BackColor = darkSecondary;
                    driveComboBox.ForeColor = darkText;
                }

                // Buttons
                foreach (Control control in topPanel?.Controls.Cast<Control>() ?? Enumerable.Empty<Control>())
                {
                    if (control is Button button)
                    {
                        button.BackColor = darkSecondary;
                        button.ForeColor = darkText;
                        button.FlatStyle = FlatStyle.Flat;
                        button.FlatAppearance.BorderColor = darkBorder;
                    }
                }
            }
            else
            {
                // Light theme colors
                var lightBackground = Color.White;
                var lightSecondary = Color.FromArgb(250, 250, 250);
                var lightText = Color.FromArgb(30, 30, 30);
                var lightBorder = Color.FromArgb(200, 200, 200);

                // Main form
                this.BackColor = lightBackground;
                this.ForeColor = lightText;

                // TreeView
                if (treeView != null)
                {
                    treeView.BackColor = lightBackground;
                    treeView.ForeColor = lightText;
                    treeView.LineColor = lightBorder;
                }

                // File info panel
                if (fileInfoPanel != null)
                {
                    fileInfoPanel.BackColor = lightSecondary;
                    fileInfoPanel.ForeColor = lightText;
                }

                if (fileInfoLabel != null)
                {
                    fileInfoLabel.BackColor = lightSecondary;
                    fileInfoLabel.ForeColor = lightText;
                }

                // Preview controls
                if (previewText != null)
                {
                    previewText.BackColor = lightBackground;
                    previewText.ForeColor = lightText;
                }

                // Statistics panel
                if (statisticsPanel != null)
                {
                    statisticsPanel.BackColor = lightSecondary;
                    statisticsPanel.ForeColor = lightText;
                }

                if (statisticsLabel != null)
                {
                    statisticsLabel.BackColor = lightSecondary;
                    statisticsLabel.ForeColor = lightText;
                }

                // File type list
                if (fileTypeListView != null)
                {
                    fileTypeListView.BackColor = lightBackground;
                    fileTypeListView.ForeColor = lightText;
                }

                // Top panel controls
                if (topPanel != null)
                {
                    topPanel.BackColor = lightBackground;
                }

                // ComboBox
                if (driveComboBox != null)
                {
                    driveComboBox.BackColor = lightBackground;
                    driveComboBox.ForeColor = lightText;
                }

                // Buttons
                foreach (Control control in topPanel?.Controls.Cast<Control>() ?? Enumerable.Empty<Control>())
                {
                    if (control is Button button)
                    {
                        button.UseVisualStyleBackColor = true;
                        button.FlatStyle = FlatStyle.Standard;
                    }
                }
            }
        }

        private void LoadDrives()
        {
            if (driveComboBox == null) return;
            
            driveComboBox.Items.Clear();  // Clear existing items first
            
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo drive in drives)
            {
                if (drive.IsReady)
                {
                    string driveInfo = $"{drive.Name} ({FileSystemHelper.FormatSize(drive.TotalSize)})";
                    driveComboBox.Items.Add(driveInfo);
                }
            }
            
            if (driveComboBox?.Items.Count > 0)
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
            if (e.Node?.ImageKey == "folder")
            {
                e.Node.ImageKey = "folderopen";
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
            if (breadcrumbBar == null) return;
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
                    if (s is LinkLabel link && link.Tag is TreeNode targetNode && treeView != null)
                        treeView.SelectedNode = targetNode;
                };

                breadcrumbBar.Controls.Add(link);
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

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using var folderDialog = new FolderBrowserDialog
            {
                ShowNewFolderButton = false,
                Description = "Select folder to scan"
            };

            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                if (driveComboBox != null)
                {
                    driveComboBox.Text = string.Empty;
                    driveComboBox.Items.Add(folderDialog.SelectedPath);
                    driveComboBox.SelectedItem = folderDialog.SelectedPath;
                    
                    // Automatically trigger scan
                    ScanButton_Click(sender, e);
                }
            }
        }

        private void CopyFileTypeStats()
        {
            if (fileTypeListView == null) return;
            
            var sb = new StringBuilder();
            foreach (ListViewItem item in fileTypeListView.Items)
            {
                sb.AppendLine(string.Join("\t", item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text)));
            }
            
            if (sb.Length > 0)
                Clipboard.SetText(sb.ToString());
        }

        private void ExportFileTypeStats()
        {
            if (fileTypeListView == null) return;

            using var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = "FileTypeStats.csv"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                var sb = new StringBuilder();
                // Add header
                sb.AppendLine(string.Join(",", fileTypeListView.Columns.Cast<ColumnHeader>().Select(ch => $"\"{ch.Text}\"")));
                
                // Add data
                foreach (ListViewItem item in fileTypeListView.Items)
                {
                    sb.AppendLine(string.Join(",", item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => $"\"{s.Text}\"")));
                }
                
                File.WriteAllText(saveDialog.FileName, sb.ToString());
            }
        }

        private void FilterLargeFiles()
        {
            if (fileTypeListView == null) return;

            foreach (ListViewItem item in fileTypeListView.Items)
            {
                var percentage = double.Parse(item.SubItems[3].Text.TrimEnd('%'));
                item.Selected = percentage > 5;
            }
        }

        private async void UpdatePreview(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is not FileSystemItem item || previewPicture == null || previewText == null || loadingLabel == null)
            {
                Debug.WriteLine("Preview update skipped: null check failed");
                return;
            }

            // Ensure preview controls are in the correct panel
            if (previewPicture.Parent?.Parent != rightSplitContainer?.Panel1)
            {
                Debug.WriteLine("Fixing preview control location");
                if (rightSplitContainer?.Panel1 != null)
                {
                    var splitContainer = rightSplitContainer.Panel1.Controls.OfType<SplitContainer>().FirstOrDefault();
                    if (splitContainer != null)
                    {
                        splitContainer.Panel2.Controls.Add(previewPicture);
                        splitContainer.Panel2.Controls.Add(previewText);
                        splitContainer.Panel2.Controls.Add(loadingLabel);
                    }
                }
            }

            try
            {
                string extension = Path.GetExtension(item.FullPath).ToLower();
                Debug.WriteLine($"Attempting to preview file: {item.FullPath} with extension: {extension}");
                
                // Image preview
                if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" }.Contains(extension))
                {
                    Debug.WriteLine("Loading standard image preview");
                    
                    previewText.Visible = false;
                    previewPicture.Visible = false;
                    loadingLabel.Visible = true;
                    loadingLabel.BringToFront();
                    
                    try
                    {
                        using (var stream = new MemoryStream(File.ReadAllBytes(item.FullPath)))
                        {
                            var image = Image.FromStream(stream);
                            if (previewPicture.Image != null)
                            {
                                var oldImage = previewPicture.Image;
                                previewPicture.Image = null;
                                oldImage.Dispose();
                            }
                            previewPicture.Image = image;
                            previewPicture.BringToFront();
                            previewPicture.Visible = true;
                            Debug.WriteLine("Image loaded successfully");
                        }
                    }
                    catch (Exception imgEx)
                    {
                        Debug.WriteLine($"Image loading error: {imgEx.Message}");
                        previewText.Text = $"Error loading image: {imgEx.Message}";
                        previewText.Visible = true;
                        previewText.BringToFront();
                    }
                    finally
                    {
                        loadingLabel.Visible = false;
                    }
                }
                else
                {
                    previewPicture.Visible = false;
                    previewText.Visible = true;
                    previewText.BringToFront();
                    previewText.Text = "Preview not available for this file type";
                    loadingLabel.Visible = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Preview error: {ex.Message}");
                previewPicture.Visible = false;
                previewText.Text = $"Error loading preview: {ex.Message}";
                previewText.Visible = true;
                previewText.BringToFront();
                loadingLabel.Visible = false;
            }
        }

        private void ResetPanels()
        {
            if (statisticsLabel != null)
                statisticsLabel.Text = "Click 'Browse' to select a folder or choose a drive to scan";

            if (fileInfoLabel != null)
                fileInfoLabel.Text = "Select a file or folder to view its details";

            if (previewText != null)
            {
                previewText.Visible = true;
                previewText.Text = "File preview will appear here when you select a supported file";
            }

            if (previewPicture != null)
            {
                previewPicture.Image?.Dispose();
                previewPicture.Image = null;
                previewPicture.Visible = false;
            }

            if (treeView != null)
            {
                treeView.Nodes.Clear();
                var welcomeNode = new TreeNode("Welcome to Disk Space Analyzer");
                welcomeNode.Nodes.Add("Select a drive or browse for a folder to begin scanning");
                treeView.Nodes.Add(welcomeNode);
            }

            if (fileTypeListView != null)
            {
                fileTypeListView.Items.Clear();
                fileTypeListView.Items.Add(new ListViewItem(new[] { "No scan data", "-", "-", "-", "-", "-" }));
            }

            if (statusLabel != null)
                statusLabel.Text = "Ready";

            if (progressBar != null)
                progressBar.Value = 0;
        }
    }
}