using Panuon.WPF.UI;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace VPet.Plugin.AITalk
{
    /// <summary>
    /// AI 聊天设置窗口
    /// </summary>
    public partial class winAITalkSetting : WindowX
    {
        private readonly AITalkBox talkBox;

        public winAITalkSetting(AITalkBox talkBox)
        {
            InitializeComponent();
            this.talkBox = talkBox;
            LoadSettings();
        }

        private void LoadSettings()
        {
            TbUrl.Text = talkBox.APIUrl;
            PbKey.Password = talkBox.APIKey;
            TbModel.Text = talkBox.Model;
            TbPersona.Text = talkBox.Persona;
            SldHistory.Value = talkBox.HistoryLength;
            CkbState.IsChecked = talkBox.InjectState;
            SldHistory.ValueChanged += (s, e) =>
                TxtHistory.Text = $"{(int)SldHistory.Value} 轮";
        }

        private void SaveSettings()
        {
            talkBox.APIUrl = TbUrl.Text.Trim();
            talkBox.APIKey = PbKey.Password.Trim();
            talkBox.Model = TbModel.Text.Trim();
            talkBox.Persona = TbPersona.Text;
            talkBox.HistoryLength = (int)SldHistory.Value;
            talkBox.InjectState = CkbState.IsChecked == true;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            MessageBoxX.Show("设置已保存", "AI 聊天");
            Close();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            talkBox.ClearHistory();
            MessageBoxX.Show("对话记忆已清空", "AI 聊天");
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            BtnTest.IsEnabled = false;
            BtnTest.Content = "测试中...";
            try
            {
                var reply = await TestRequestAsync();
                MessageBoxX.Show($"连接成功! AI 回复:\n{reply}", "AI 聊天");
            }
            catch (Exception ex)
            {
                MessageBoxX.Show($"连接失败:\n{ex.Message}", "AI 聊天");
            }
            finally
            {
                BtnTest.IsEnabled = true;
                BtnTest.Content = "测试连接";
            }
        }

        /// <summary>
        /// 发送一条最简非流式请求验证配置
        /// </summary>
        private async Task<string> TestRequestAsync()
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var req = new
            {
                model = talkBox.Model,
                messages = new[]
                {
                    new { role = "user", content = "请回复: 连接成功" }
                },
                stream = false,
                max_tokens = 20,
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, talkBox.APIUrl);
            request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", talkBox.APIKey);
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception($"HTTP {(int)response.StatusCode}: {body.Replace('\n', ' ')}");
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }
    }
}
