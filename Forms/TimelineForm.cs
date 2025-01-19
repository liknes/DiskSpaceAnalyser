using DiskSpaceAnalyzer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace DiskSpaceAnalyzer.Forms
{
    public class TimelineForm : Form
    {
        private Chart timelineChart;
        private ComboBox timeRangeCombo;
        private readonly List<FileSystemItem> _items;

        public TimelineForm(List<FileSystemItem> items)
        {
            _items = items;
            InitializeComponents();
            PopulateChart("Last 30 Days");
        }

        private void InitializeComponents()
        {
            this.Size = new Size(1000, 600);
            this.Text = "File Timeline";
            this.StartPosition = FormStartPosition.CenterParent;

            timeRangeCombo = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            timeRangeCombo.Items.AddRange(new[] { "Last 30 Days", "Last 12 Months", "Last 5 Years" });
            timeRangeCombo.SelectedIndex = 0;
            timeRangeCombo.SelectedIndexChanged += (s, e) => PopulateChart(timeRangeCombo.SelectedItem.ToString());

            timelineChart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            var chartArea = new ChartArea();
            chartArea.AxisX.LabelStyle.Format = "dd/MM/yyyy";
            chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisY.LabelStyle.Format = "N0";
            timelineChart.ChartAreas.Add(chartArea);

            var legend = new Legend();
            timelineChart.Legends.Add(legend);

            var createdSeries = new Series("Created")
            {
                ChartType = SeriesChartType.StackedColumn,
                XValueType = ChartValueType.DateTime
            };

            var modifiedSeries = new Series("Modified")
            {
                ChartType = SeriesChartType.StackedColumn,
                XValueType = ChartValueType.DateTime
            };

            timelineChart.Series.Add(createdSeries);
            timelineChart.Series.Add(modifiedSeries);

            this.Controls.Add(timelineChart);
            this.Controls.Add(timeRangeCombo);
        }

        private void PopulateChart(string timeRange)
        {
            DateTime startDate = timeRange switch
            {
                "Last 30 Days" => DateTime.Now.AddDays(-30),
                "Last 12 Months" => DateTime.Now.AddMonths(-12),
                "Last 5 Years" => DateTime.Now.AddYears(-5),
                _ => DateTime.Now.AddDays(-30)
            };

            var createdFiles = _items
                .Where(i => i.Created >= startDate)
                .GroupBy(i => i.Created.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date);

            var modifiedFiles = _items
                .Where(i => i.Modified >= startDate)
                .GroupBy(i => i.Modified.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date);

            timelineChart.Series["Created"].Points.Clear();
            timelineChart.Series["Modified"].Points.Clear();

            foreach (var point in createdFiles)
            {
                timelineChart.Series["Created"].Points.AddXY(point.Date, point.Count);
            }

            foreach (var point in modifiedFiles)
            {
                timelineChart.Series["Modified"].Points.AddXY(point.Date, point.Count);
            }
        }
    }
} 