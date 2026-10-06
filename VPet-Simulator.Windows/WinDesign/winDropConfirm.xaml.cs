using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VPet_Simulator.Windows
{
    /// <summary>
    /// 拖拽删除确认窗口: 列出待删除项目, 用户确认后由调用方执行彻底删除
    /// </summary>
    public partial class winDropConfirm : WindowX
    {
        /// <summary>
        /// 用户是否确认删除
        /// </summary>
        public bool Confirmed { get; private set; } = false;

        /// <summary>
        /// 待删除路径
        /// </summary>
        public string[] Paths { get; }

        public winDropConfirm(string[] paths)
        {
            InitializeComponent();
            Paths = paths;
            BuildList();
        }

        private void BuildList()
        {
            long totalBytes = 0;
            int dirCount = 0;
            foreach (var p in Paths)
            {
                try
                {
                    bool isDir = Directory.Exists(p);
                    string sizeText = "";
                    if (isDir)
                    {
                        dirCount++;
                        sizeText = "文件夹(递归删除其中所有内容)";
                    }
                    else if (File.Exists(p))
                    {
                        var fi = new FileInfo(p);
                        totalBytes += fi.Length;
                        sizeText = FormatSize(fi.Length);
                    }
                    else
                    {
                        sizeText = "不存在";
                    }
                    var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
                    sp.Children.Add(new TextBlock
                    {
                        Text = isDir ? "📁" : "📄",
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                    });
                    sp.Children.Add(new TextBlock
                    {
                        Text = p,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = (Brush)FindResource("DARKPrimary"),
                    });
                    sp.Children.Add(new TextBlock
                    {
                        Text = $"　({sizeText})",
                        Opacity = 0.6,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = (Brush)FindResource("DARKPrimary"),
                    });
                    SpItems.Children.Add(sp);
                }
                catch { }
            }
            TxtWarn.Text = $"共 {Paths.Length} 项" + (dirCount > 0 ? $" (含 {dirCount} 个文件夹)" : "")
                + (totalBytes > 0 ? $", 合计 {FormatSize(totalBytes)}" : "")
                + "\n以下内容将被永久删除 (不经过回收站, 无法恢复):";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024)
                return $"{bytes / 1024.0 / 1024 / 1024:f2} GB";
            if (bytes >= 1024 * 1024)
                return $"{bytes / 1024.0 / 1024:f1} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:f1} KB";
            return $"{bytes} B";
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
