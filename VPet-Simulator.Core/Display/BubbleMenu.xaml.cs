using Panuon.WPF.UI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using static VPet_Simulator.Core.GraphInfo;
using Timer = System.Timers.Timer;

namespace VPet_Simulator.Core
{
    /// <summary>
    /// 右键气泡式交互菜单: 右键桌宠时在周围弹出聊天气泡(投喂/面板/互动/系统), 代替工具栏作为交互入口
    /// </summary>
    public partial class BubbleMenu : UserControl
    {
        private readonly Main m;
        /// <summary>
        /// 自动关闭计时器
        /// </summary>
        private readonly Timer CloseTimer;
        /// <summary>
        /// 正在播放关闭动画
        /// </summary>
        private bool closing;
        /// <summary>
        /// 气泡菜单是否正在显示
        /// </summary>
        public bool IsOpen => IsBubblesVisible();

        public BubbleMenu(Main m)
        {
            InitializeComponent();
            this.m = m;
            CloseTimer = new Timer(8000)
            {
                AutoReset = false,
                Enabled = false
            };
            CloseTimer.Elapsed += (s, e) => Dispatcher.Invoke(Hide);
        }

        /// <summary>
        /// 弹出气泡菜单 (自动隐藏工具栏)
        /// </summary>
        public void Show()
        {
            if (m.ToolBar?.Visibility == Visibility.Visible)
            {
                m.ToolBar.CloseTimer.Enabled = false;
                m.ToolBar.Visibility = Visibility.Collapsed;
            }
            closing = false;
            CloseTimer.Stop();
            CloseTimer.Start();
            Panel.SetZIndex(this, 1000);
            m.MsgBar?.ForceClose();
            //自定气泡: 仅当工具栏的自定菜单已注册菜单项时显示
            if (m.ToolBar != null && m.ToolBar.MenuDIY.Visibility == Visibility.Visible && m.ToolBar.MenuDIY.HasItems)
                BubDIY.Visibility = Visibility.Visible;
            else
                BubDIY.Visibility = Visibility.Collapsed;

            var bubbles = new Grid[] { BubFeed, BubPanel, BubInteract, BubSetting, BubDIY };
            for (int i = 0; i < bubbles.Length; i++)
            {
                var bub = bubbles[i];
                bub.Visibility = Visibility.Visible;
                bub.Opacity = 1;
                var st = new ScaleTransform(0.1, 0.1);
                bub.RenderTransform = st;
                var ease = new BackEase { Amplitude = 0.6, EasingMode = EasingMode.EaseOut };
                var animX = new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease };
                var animY = new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease };
                var animO = new DoubleAnimation(1, TimeSpan.FromMilliseconds(100));
                st.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
                bub.BeginAnimation(OpacityProperty, animO);
                //错开弹出时间, 依次弹出更活泼
                if (i > 0)
                {
                    var delayed = bub;
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(i * 60) };
                    timer.Tick += (s, e) =>
                    {
                        ((DispatcherTimer)s!).Stop();
                        var st2 = new ScaleTransform(0.1, 0.1);
                        delayed.RenderTransform = st2;
                        st2.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
                        st2.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
                    };
                    timer.Start();
                }
            }
        }

        /// <summary>
        /// 收起气泡菜单
        /// </summary>
        public void Hide()
        {
            if (Visibility != Visibility.Visible && !IsBubblesVisible())
                return;
            if (closing)
                return;
            closing = true;
            CloseTimer.Stop();
            var bubbles = new Grid[] { BubFeed, BubPanel, BubInteract, BubSetting, BubDIY };
            foreach (var bub in bubbles)
            {
                var animO = new DoubleAnimation(0, TimeSpan.FromMilliseconds(120));
                animO.Completed += (s, e) =>
                {
                    if (closing)
                    {
                        bub.Visibility = Visibility.Collapsed;
                    }
                };
                bub.BeginAnimation(OpacityProperty, animO);
            }
            var done = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
            done.Tick += (s, e) =>
            {
                ((DispatcherTimer)s!).Stop();
                if (closing)
                {
                    foreach (var bub in bubbles)
                        bub.Visibility = Visibility.Collapsed;
                    closing = false;
                }
            };
            done.Start();
        }

        private bool IsBubblesVisible()
        {
            return BubFeed.Visibility == Visibility.Visible || BubPanel.Visibility == Visibility.Visible
                || BubInteract.Visibility == Visibility.Visible || BubSetting.Visibility == Visibility.Visible
                || BubDIY.Visibility == Visibility.Visible;
        }

        /// <summary>
        /// 打开工具栏对应子菜单 (保持插件注册的菜单项全部可用)
        /// </summary>
        private void OpenToolBarSub(MenuItem mi)
        {
            Hide();
            m.ToolBar?.Show();
            mi.IsSubmenuOpen = true;
        }

        private void BubFeed_Click(object sender, MouseButtonEventArgs e)
        {
            if (m.ToolBar != null)
                OpenToolBarSub(m.ToolBar.MenuFeed);
        }

        private void BubPanel_Click(object sender, MouseButtonEventArgs e)
        {//面板: 显示桌宠状态面板 (与工具栏悬停面板一致)
            Hide();
            if (m.ToolBar != null)
            {
                m.ToolBar.Show();
                m.ToolBar.ShowPanel();
            }
        }

        private void BubInteract_Click(object sender, MouseButtonEventArgs e)
        {
            if (m.ToolBar != null)
                OpenToolBarSub(m.ToolBar.MenuInteract);
        }

        private void BubSetting_Click(object sender, MouseButtonEventArgs e)
        {
            if (m.ToolBar != null)
                OpenToolBarSub(m.ToolBar.MenuSetting);
        }

        private void BubDIY_Click(object sender, MouseButtonEventArgs e)
        {
            if (m.ToolBar != null)
                OpenToolBarSub(m.ToolBar.MenuDIY);
        }

        private void UserControl_MouseEnter(object sender, MouseEventArgs e)
        {
            CloseTimer.Stop();
        }

        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            if (IsBubblesVisible())
                CloseTimer.Start();
        }
    }
}
