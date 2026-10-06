using Panuon.WPF.UI;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VPet.Plugin.AITalk
{
    /// <summary>
    /// 记忆与知识库管理窗口: 编辑"关于我"知识库 + 管理长期记忆条目
    /// </summary>
    public partial class winAITalkMemory : WindowX
    {
        public winAITalkMemory()
        {
            InitializeComponent();
            TbProfile.Text = AITalkMemory.Profile;
            Refresh();
        }

        public void Refresh()
        {
            TxtSummary.Text = $"长期记忆 {AITalkMemory.Memories.Count} 条" +
                (string.IsNullOrWhiteSpace(AITalkMemory.Profile) ? " · 知识库为空 (建议在\"关于我\"页填写)" : " · 知识库已填写");
            SpMemories.Children.Clear();
            var list = AITalkMemory.Memories.OrderByDescending(m => m.Time).ToList();
            if (list.Count == 0)
            {
                SpMemories.Children.Add(new TextBlock
                {
                    Text = "还没有记忆~ 和桌宠聊聊天, 它会自动记住关于你的事情",
                    Opacity = 0.55,
                    Margin = new Thickness(4, 10, 4, 0),
                    Foreground = (Brush)FindResource("DARKPrimary"),
                });
                return;
            }
            foreach (var m in list)
                SpMemories.Children.Add(BuildRow(m));
        }

        private UIElement BuildRow(MemoryEntry m)
        {
            var grid = new Grid { Margin = new Thickness(2, 3, 2, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tag = new TextBlock
            {
                Text = m.Source == "manual" ? "✍" : "💡",
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = m.Source == "manual" ? "手动添加" : "对话自动学习",
            };
            Grid.SetColumn(tag, 0);

            var tb = new TextBlock
            {
                Text = $"{m.Content}",
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("DARKPrimary"),
                ToolTip = m.Time.ToString("yyyy-MM-dd HH:mm"),
            };
            Grid.SetColumn(tb, 1);

            var del = new Button
            {
                Content = "✕",
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = Cursors.Hand,
                ToolTip = "删除该记忆",
                Opacity = 0.65,
            };
            del.SetResourceReference(StyleProperty, "ThemedButtonStyle");
            ButtonHelper.SetCornerRadius(del, new CornerRadius(4));
            del.Click += (s, e) => { AITalkMemory.Remove(m.Id); Refresh(); };
            Grid.SetColumn(del, 2);

            grid.Children.Add(tag);
            grid.Children.Add(tb);
            grid.Children.Add(del);
            return grid;
        }

        private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            AITalkMemory.Profile = TbProfile.Text;
            MessageBoxX.Show("知识库已保存, 桌宠现在知道这些啦~", "记忆与知识库");
            Refresh();
        }

        private void TbNewMemory_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnAdd_Click(sender, e);
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var text = TbNewMemory.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;
            if (!AITalkMemory.Add(text, "manual"))
            {
                MessageBoxX.Show("这条记忆和已有的重复啦", "记忆与知识库");
                return;
            }
            TbNewMemory.Text = "";
            Refresh();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBoxX.Show("确定要清空全部长期记忆吗?\n(知识库不受影响, 清空后无法恢复)", "记忆与知识库",
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;
            AITalkMemory.ClearAll();
            Refresh();
        }
    }
}
