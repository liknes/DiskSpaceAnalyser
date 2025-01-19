using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

public class DiskTrendsForm : Form
{
    private Chart trendsChart;
    private readonly string _drivePath;
    private System.Windows.Forms.Timer _updateTimer;
    private readonly Queue<DiskSnapshot> _snapshots = new(30); // Keep last 30 snapshots

    private class DiskSnapshot
    {
        public DateTime Time { get; set; }
        public long TotalSpace { get; set; }
        public long UsedSpace { get; set; }
        public long FreeSpace { get; set; }
    }

    public DiskTrendsForm(string drivePath)
    {
        _drivePath = drivePath;
        InitializeComponents();
        StartMonitoring();
    }

    private void InitializeComponents()
    {
        this.Size = new Size(800, 500);
        this.Text = $"Disk Space Trends - {_drivePath}";
        this.StartPosition = FormStartPosition.CenterParent;

        trendsChart = new Chart
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        var chartArea = new ChartArea();
        chartArea.AxisX.LabelStyle.Format = "HH:mm:ss";
        chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
        chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;
        chartArea.AxisY.LabelStyle.Format = "N0";
        trendsChart.ChartAreas.Add(chartArea);

        var legend = new Legend();
        trendsChart.Legends.Add(legend);

        var usedSpaceSeries = new Series("Used Space")
        {
            ChartType = SeriesChartType.Line,
            XValueType = ChartValueType.DateTime
        };

        var freeSpaceSeries = new Series("Free Space")
        {
            ChartType = SeriesChartType.Line,
            XValueType = ChartValueType.DateTime
        };

        trendsChart.Series.Add(usedSpaceSeries);
        trendsChart.Series.Add(freeSpaceSeries);

        this.Controls.Add(trendsChart);
    }

    private void StartMonitoring()
    {
        _updateTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000 // Update every second
        };

        _updateTimer.Tick += (s, e) => UpdateDiskStats();
        _updateTimer.Start();
        UpdateDiskStats(); // Initial update
    }

    private void UpdateDiskStats()
    {
        try
        {
            var drive = new DriveInfo(_drivePath);
            if (!drive.IsReady) return;

            var snapshot = new DiskSnapshot
            {
                Time = DateTime.Now,
                TotalSpace = drive.TotalSize,
                FreeSpace = drive.AvailableFreeSpace,
                UsedSpace = drive.TotalSize - drive.AvailableFreeSpace
            };

            _snapshots.Enqueue(snapshot);
            if (_snapshots.Count > 30) // Keep only last 30 snapshots
                _snapshots.Dequeue();

            UpdateChart();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating disk stats: {ex.Message}");
        }
    }

    private void UpdateChart()
    {
        trendsChart.Series["Used Space"].Points.Clear();
        trendsChart.Series["Free Space"].Points.Clear();

        foreach (var snapshot in _snapshots)
        {
            trendsChart.Series["Used Space"].Points.AddXY(
                snapshot.Time, 
                snapshot.UsedSpace / (1024.0 * 1024 * 1024) // Convert to GB
            );

            trendsChart.Series["Free Space"].Points.AddXY(
                snapshot.Time, 
                snapshot.FreeSpace / (1024.0 * 1024 * 1024) // Convert to GB
            );
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _updateTimer?.Stop();
        base.OnFormClosing(e);
    }
} 