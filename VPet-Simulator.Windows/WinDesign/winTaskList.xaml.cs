using Panuon.WPF.UI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VPet_Simulator.Windows
{
    /// <summary>
    /// 任务清单窗口: 本日/本周/本月 任务管理, 勾选完成
    /// </summary>
    public partial class winTaskList : WindowX
    {
        public winTaskList()
        {
            InitializeComponent();
            Refresh();
        }

        /// <summary>
        /// 打开并定位到指定分类
        /// </summary>
        public void OpenTab(TaskScope scope)
        {
            Tabs.SelectedIndex = (int)scope;
            Refresh();
        }

        /// <summary>
        /// 刷新全部列表与进度
        /// </summary>
        public void Refresh()
        {
            var (d1, t1) = TaskManager.Progress(TaskScope.Day);
            var (d2, t2) = TaskManager.Progress(TaskScope.Week);
            var (d3, t3) = TaskManager.Progress(TaskScope.Month);
            TxtSummary.Text = $"今日 {d1}/{t1} 已完成    ·    本周 {d2}/{t2} 已完成    ·    本月 {d3}/{t3} 已完成";
            Fill(SpDay, TaskScope.Day);
            Fill(SpWeek, TaskScope.Week);
            Fill(SpMonth, TaskScope.Month);
        }

        private void Fill(StackPanel panel, TaskScope scope)
        {
            panel.Children.Clear();
            var list = TaskManager.Current(scope);
            if (list.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "暂无任务, 在下方输入框添加吧~",
                    Opacity = 0.55,
                    Margin = new Thickness(4, 10, 4, 0),
                    Foreground = (Brush)FindResource("DARKPrimary"),
                });
                return;
            }
            foreach (var t in list)
                panel.Children.Add(BuildRow(t));
        }

        private UIElement BuildRow(TaskItem t)
        {
            var grid = new Grid { Margin = new Thickness(2, 3, 2, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var cb = new CheckBox
            {
                IsChecked = t.Done,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
            };
            cb.Checked += (s, e) => { TaskManager.SetDone(t.Id, true); Refresh(); };
            cb.Unchecked += (s, e) => { TaskManager.SetDone(t.Id, false); Refresh(); };
            Grid.SetColumn(cb, 0);

            var tb = new TextBlock
            {
                Text = t.Content,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("DARKPrimary"),
            };
            if (t.Done)
            {
                tb.TextDecorations = TextDecorations.Strikethrough;
                tb.Opacity = 0.5;
            }
            Grid.SetColumn(tb, 1);

            var del = new Button
            {
                Content = "✕",
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = Cursors.Hand,
                ToolTip = "删除该任务",
                Opacity = 0.65,
            };
            del.SetResourceReference(StyleProperty, "ThemedButtonStyle");
            Panuon.WPF.UI.ButtonHelper.SetCornerRadius(del, new CornerRadius(4));
            del.Click += (s, e) => { TaskManager.Remove(t.Id); Refresh(); };
            Grid.SetColumn(del, 2);

            grid.Children.Add(cb);
            grid.Children.Add(tb);
            grid.Children.Add(del);
            return grid;
        }

        private void AddBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            if (sender == TbDay)
                AddDay_Click(sender, e);
            else if (sender == TbWeek)
                AddWeek_Click(sender, e);
            else if (sender == TbMonth)
                AddMonth_Click(sender, e);
        }

        private void AddDay_Click(object sender, RoutedEventArgs e) => AddFrom(TbDay, TaskScope.Day);
        private void AddWeek_Click(object sender, RoutedEventArgs e) => AddFrom(TbWeek, TaskScope.Week);
        private void AddMonth_Click(object sender, RoutedEventArgs e) => AddFrom(TbMonth, TaskScope.Month);

        private void AddFrom(TextBox box, TaskScope scope)
        {
            var text = box.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;
            TaskManager.Add(text, scope);
            box.Text = "";
            Refresh();
        }
    }
}
