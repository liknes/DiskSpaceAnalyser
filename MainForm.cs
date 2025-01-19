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

        private CancellationTokenSource? _cancellationTokenSource;
        private AppSettings _settings;
        private Dictionary<string, long> _fileTypeStats;
        private long _totalScannedSize;
        private int _totalFiles;
        private int _totalDirectories;

        private ImageList? _iconList;
        private ConcurrentDictionary<string, FolderStats> _folderStats = new();

        private ListView? fileTypeListView;

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
                    rightSplitContainer.SplitterDistance = rightSplitContainer.Height / 3;  // 33% for top panel, giving more space to bottom
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
            InitializePreviewPanel();
            
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
        }

        private void InitializeTopPanel()
        {
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(240, 240, 240)
            };

            // Modern button style
            var buttonStyle = new Action<Button>(btn =>
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
                btn.BackColor = Color.White;
                btn.Font = new Font("Segoe UI", 9F);
                btn.Cursor = Cursors.Hand;
                btn.Height = 30;
            });

            driveComboBox = new ComboBox
            {
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(10, 10),
                Font = new Font("Segoe UI", 9F),
                Height = 30
            };

            var browseButton = new Button
            {
                Text = "Browse",
                Width = 70,
                Location = new Point(170, 10)
            };
            buttonStyle(browseButton);
            browseButton.Click += BrowseButton_Click;

            scanButton = new Button
            {
                Text = "Scan",
                Width = 70,
                Location = new Point(250, 10)
            };
            buttonStyle(scanButton);
            scanButton.Click += ScanButton_Click;

            settingsButton = new Button
            {
                Text = "Settings",
                Width = 80,
                Location = new Point(330, 10)
            };
            buttonStyle(settingsButton);
            settingsButton.Click += SettingsButton_Click;

            progressBar = new ProgressBar
            {
                Width = 150,
                Location = new Point(420, 10),
                Height = 30,
                Style = ProgressBarStyle.Blocks,
                MarqueeAnimationSpeed = 30
            };

            var findDuplicatesButton = new Button
            {
                Text = "Find Duplicates",
                Width = 110,
                Location = new Point(580, 10)
            };
            buttonStyle(findDuplicatesButton);
            findDuplicatesButton.Click += async (s, e) => await FindDuplicateFiles();

            var timelineButton = new Button
            {
                Text = "Timeline",
                Width = 80,
                Location = new Point(700, 10)
            };
            buttonStyle(timelineButton);
            timelineButton.Click += (s, e) =>
            {
                var items = GetAllItems();
                using var timelineForm = new TimelineForm(items);
                timelineForm.ShowDialog(this);
            };

            var trendsButton = new Button
            {
                Text = "Disk Trends",
                Width = 90,
                Location = new Point(790, 10)
            };
            buttonStyle(trendsButton);
            trendsButton.Click += (s, e) =>
            {
                var selectedDrive = driveComboBox.Text.Split(' ')[0];
                using var trendsForm = new DiskTrendsForm(selectedDrive);
                trendsForm.ShowDialog(this);
            };

            topPanel.Controls.AddRange(new Control[] { 
                driveComboBox, browseButton, scanButton, settingsButton, progressBar, 
                findDuplicatesButton, timelineButton, trendsButton 
            });
            this.Controls.Add(topPanel);
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

            fileInfoPanel?.Controls.Add(fileInfoLabel);
            rightSplitContainer?.Panel1.Controls.Add(fileInfoPanel);
        }

        private void InitializeFileTypePanel()
        {
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

            if (rightSplitContainer != null)
            {
                // Add the ListView
                rightSplitContainer.Panel2.Controls.Add(fileTypeListView);
                
                // Add the statistics panel at the bottom
                if (statisticsPanel != null)
                {
                    statisticsPanel.Dock = DockStyle.Bottom;
                    rightSplitContainer.Panel2.Controls.Add(statisticsPanel);
                    statisticsPanel.BringToFront();
                }
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

            if (scanButton != null) scanButton.Text = "Cancel";
            if (treeView != null) treeView.Nodes.Clear();
            if (progressBar != null) progressBar.Style = ProgressBarStyle.Marquee;
            
            // If it contains parentheses, it's a drive entry, otherwise it's a folder path
            string selectedPath = driveComboBox.Text.Contains("(") 
                ? driveComboBox.Text.Split(' ')[0]  // Takes "D:" from "D: (800 MB)"
                : driveComboBox.Text;  // Use full path for folders

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                if (treeView != null) await ScanDirectoryOptimized(selectedPath, treeView.Nodes, _cancellationTokenSource.Token);
                UpdateStatistics();
                UpdateFileTypeStats();
                if (statusLabel != null) statusLabel.Text = "Scan completed";
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
                var darkBackground = Color.FromArgb(32, 32, 32);
                var darkSecondary = Color.FromArgb(48, 48, 48);
                var darkText = Color.FromArgb(240, 240, 240);

                this.BackColor = darkBackground;
                this.ForeColor = darkText;
                
                treeView.BackColor = darkSecondary;
                treeView.ForeColor = darkText;
                treeView.LineColor = Color.FromArgb(80, 80, 80);
                
                fileInfoPanel.BackColor = darkSecondary;
                fileInfoLabel.BackColor = darkSecondary;
                fileInfoLabel.ForeColor = darkText;
                
                statisticsPanel.BackColor = darkSecondary;
                statisticsLabel.ForeColor = darkText;
            }
            else
            {
                var lightBackground = Color.White;
                var lightSecondary = Color.FromArgb(250, 250, 250);
                var lightText = Color.FromArgb(30, 30, 30);

                this.BackColor = lightBackground;
                this.ForeColor = lightText;
                
                treeView.BackColor = lightBackground;
                treeView.ForeColor = lightText;
                treeView.LineColor = Color.FromArgb(200, 200, 200);
                
                fileInfoPanel.BackColor = lightSecondary;
                fileInfoLabel.BackColor = lightSecondary;
                fileInfoLabel.ForeColor = lightText;
                
                statisticsPanel.BackColor = lightSecondary;
                statisticsLabel.ForeColor = lightText;
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

        private void InitializePreviewPanel()
        {
            previewPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 400,
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

            loadingLabel = new Label
            {
                Text = "⌛ Loading preview...",
                AutoSize = true,
                Font = new Font("Segoe UI", 12f),
                Visible = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };

            if (rightSplitContainer?.Panel2 != null && previewPicture != null && previewText != null && loadingLabel != null)
                rightSplitContainer.Panel2.Controls.AddRange(new Control[] { previewPicture, previewText, loadingLabel });
            if (mainSplitContainer?.Panel2 != null && previewPanel != null)
                mainSplitContainer.Panel2.Controls.Add(previewPanel);

            treeView.AfterSelect += UpdatePreview;
        }

        private async void UpdatePreview(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is not FileSystemItem item || previewPicture == null || previewText == null || loadingLabel == null)
            {
                if (previewPicture != null) previewPicture.Visible = false;
                if (previewText != null) previewText.Visible = false;
                if (loadingLabel != null) loadingLabel.Visible = false;
                return;
            }

            try
            {
                string extension = Path.GetExtension(item.FullPath).ToLower();
                
                // RAW formats - handle asynchronously
                if (new[] { ".dng", ".cr2", ".nef" }.Contains(extension))
                {
                    previewPicture.Image = null;
                    previewPicture.Visible = false;
                    previewText.Visible = false;
                    loadingLabel.Visible = true;

                    await Task.Run(() =>
                    {
                        using var image = new MagickImage(item.FullPath);
                        image.Quality = 50;
                        image.Resize(800, 800);
                        
                        using var memStream = new MemoryStream();
                        image.Write(memStream, MagickFormat.Jpg);
                        memStream.Position = 0;

                        this.Invoke(() =>
                        {
                            previewPicture.Image = Image.FromStream(memStream);
                            previewPicture.Visible = true;
                            loadingLabel.Visible = false;
                        });
                    });
                }
                // Image preview
                else if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" }.Contains(extension))
                {
                    using var stream = File.OpenRead(item.FullPath);
                    previewPicture.Image = Image.FromStream(stream);
                    previewPicture.Visible = true;
                    previewText.Visible = false;
                    loadingLabel.Visible = false;
                }
                // Text preview
                else if (new[] { ".txt", ".log", ".xml", ".json", ".cs", ".html", ".css", ".js" }.Contains(extension))
                {
                    previewText.Text = File.ReadAllText(item.FullPath);
                    previewText.Visible = true;
                    previewPicture.Visible = false;
                    loadingLabel.Visible = false;
                }
                else
                {
                    loadingLabel.Visible = false;
                    previewPicture.Visible = false;
                    previewText.Visible = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading preview: {ex.Message}");
                if (previewPicture != null) previewPicture.Visible = false;
                if (previewText != null) 
                {
                    previewText.Text = $"Error loading preview: {ex.Message}";
                    previewText.Visible = true;
                }
                if (loadingLabel != null) loadingLabel.Visible = false;
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
    }
}