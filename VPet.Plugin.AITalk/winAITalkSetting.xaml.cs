using Panuon.WPF.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace VPet.Plugin.AITalk
{
    /// <summary>
    /// AI 聊天设置窗口
    /// </summary>
    public partial class winAITalkSetting : WindowX
    {
        private readonly AITalkBox talkBox;
        private bool loading;

        /// <summary>
        /// 常用服务预设: 名称 / API地址 / 推荐模型
        /// </summary>
        private static readonly (string Name, string Url, string Model)[] Presets = new[]
        {
            ("智谱 GLM (有免费额度)", "https://open.bigmodel.cn/api/paas/v4/chat/completions", "glm-4-flash"),
            ("DeepSeek", "https://api.deepseek.com/chat/completions", "deepseek-chat"),
            ("OpenAI", "https://api.openai.com/v1/chat/completions", "gpt-4o-mini"),
            ("商汤 SenseNova Token Plan", "https://token.sensenova.cn/v1/chat/completions", "sensenova-6.8-flash-lite"),
            ("商汤 SenseNova (兼容模式)", "https://api.sensenova.cn/compatible-mode/v1/chat/completions", "sensenova-6.7-flash-lite"),
            ("自定义 (仅切换不修改已填内容)", "", ""),
        };

        public winAITalkSetting(AITalkBox talkBox)
        {
            InitializeComponent();
            this.talkBox = talkBox;
            loading = true;
            foreach (var p in Presets)
                CbPreset.Items.Add(p.Name);
            LoadSettings();
            CbPreset.SelectedIndex = Presets.Length - 1;
            loading = false;
        }

        private void LoadSettings()
        {
            TbUrl.Text = talkBox.APIUrl;
            PbKey.Password = talkBox.APIKey;
            TbModel.Text = talkBox.Model;
            TbPersona.Text = talkBox.Persona;
            SldHistory.Value = talkBox.HistoryLength;
            CkbState.IsChecked = talkBox.InjectState;
            CkbMemory.IsChecked = talkBox.AutoMemory;
            TxtHistory.Text = $"{(int)SldHistory.Value} 轮";
            SldHistory.ValueChanged += (s, e) =>
                TxtHistory.Text = $"{(int)SldHistory.Value} 轮";
        }

        private void CbPreset_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (loading || CbPreset.SelectedIndex < 0)
                return;
            var p = Presets[CbPreset.SelectedIndex];
            if (string.IsNullOrEmpty(p.Url))
                return;//自定义: 不覆盖
            TbUrl.Text = p.Url;
            TbModel.Text = p.Model;
        }

        private void SaveSettings()
        {
            talkBox.APIUrl = TbUrl.Text.Trim();
            talkBox.APIKey = PbKey.Password.Trim();
            talkBox.Model = TbModel.Text.Trim();
            talkBox.Persona = TbPersona.Text;
            talkBox.HistoryLength = (int)SldHistory.Value;
            talkBox.InjectState = CkbState.IsChecked == true;
            talkBox.AutoMemory = CkbMemory.IsChecked == true;
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

        private void BtnMemory_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            new winAITalkMemory { Topmost = true }.Show();
        }

        private void BtnLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(AITalkBox.LogPath))
                    File.AppendAllText(AITalkBox.LogPath, $"# AI 聊天日志 ({DateTime.Now:yyyy-MM-dd HH:mm:ss})\n");
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{AITalkBox.LogPath}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBoxX.Show($"无法打开日志文件:\n{AITalkBox.LogPath}\n{Truncate(ex.Message, 200)}", "AI 聊天");
            }
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
                AITalkBox.Log("测试连接失败: " + ex.ToString());
                MessageBoxX.Show($"连接失败:\n{Truncate(ex.Message, 400)}{DiagnoseHint(ex)}\n\n详情已记录到日志(可点击\"打开日志\"查看):\n{AITalkBox.LogPath}", "AI 聊天");
            }
            finally
            {
                BtnTest.IsEnabled = true;
                BtnTest.Content = "测试连接";
            }
        }

        /// <summary>
        /// 根据异常内容给出常见原因提示
        /// </summary>
        private static string DiagnoseHint(Exception ex)
        {
            var full = ex.ToString();
            if (full.Contains("No such host") || full.Contains("remote name could not be resolved"))
                return "\n\n可能原因: 域名无法解析 — 请检查 API 地址是否写错, 或本机 DNS/代理设置";
            if (full.Contains("Connection refused") || full.Contains("actively refused"))
                return "\n\n可能原因: 目标拒绝连接 — 请检查地址和端口(或用浏览器确认该服务可用)";
            if (full.Contains("SSL") || full.Contains("TLS") || full.Contains("certificate"))
                return "\n\n可能原因: TLS/证书握手失败 — 如使用代理软件请确认其正在正常工作";
            if (ex is TaskCanceledException || ex is OperationCanceledException)
                return "\n\n可能原因: 请求超时 — 请检查网络能否访问该服务";
            if (full.Contains("401"))
                return "\n\n可能原因: API Key 无效或未填写";
            if (full.Contains("404"))
                return "\n\n可能原因: API 地址路径错误 — 确认是否缺少 /v1 或 /chat/completions 等后缀";
            if (full.Contains("429"))
                return "\n\n可能原因: 请求频率超限或账户余额不足";
            return "";
        }

        private static string Truncate(string s, int n)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "...");

        /// <summary>
        /// 发送一条最简非流式请求验证配置 (地址自动补全 + 代理失败自动直连重试)
        /// </summary>
        private async Task<string> TestRequestAsync()
        {
            var keyCheck = AITalkBox.CheckKeyLooksValid(talkBox.APIKey);
            if (keyCheck != null)
            {
                AITalkBox.Log("测试连接 密钥格式异常: " + keyCheck);
                throw new Exception(keyCheck);
            }
            var url = AITalkBox.NormalizeUrl(talkBox.APIUrl);
            if (!string.Equals(url, talkBox.APIUrl.Trim(), StringComparison.OrdinalIgnoreCase))
                AITalkBox.Log($"测试连接 地址自动补全: {talkBox.APIUrl} → {url}");
            AITalkBox.Log($"测试连接 → {url} model={talkBox.Model}");
            var req = new
            {
                model = talkBox.Model,
                messages = new[]
                {
                    new { role = "user", content = "请回复: 连接成功" }
                },
                stream = false,
                max_tokens = 50,
            };
            Func<HttpRequestMessage> makeRequest = () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", talkBox.APIKey);
                return request;
            };
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(makeRequest());
            }
            catch (Exception ex) when (IsNetworkError(ex))
            {
                AITalkBox.Log($"测试连接 系统网络路径失败({ex.GetType().Name}: {ex.Message}), 改用直连重试");
                using var clientNoProxy = new HttpClient(new SocketsHttpHandler
                {
                    UseProxy = false,
                    ConnectTimeout = TimeSpan.FromSeconds(20),
                })
                { Timeout = TimeSpan.FromSeconds(30) };
                response = await clientNoProxy.SendAsync(makeRequest());
            }
            using (response)
            {
                var body = await response.Content.ReadAsStringAsync();
                AITalkBox.Log($"测试连接 ← HTTP {(int)response.StatusCode} {response.ReasonPhrase}, 响应: {Truncate(body, 500)}");
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body.Replace('\n', ' ').Replace('\r', ' '), 300)}");
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                    throw new Exception("响应中没有 choices 字段: " + Truncate(body, 300));
                var c0 = choices[0];
                if (c0.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var mc))
                    return mc.GetString() ?? "(空)";
                if (c0.TryGetProperty("text", out var t))
                    return t.GetString() ?? "(空)";
                return "(无法识别的响应格式, 详情见日志)";
            }
        }

        /// <summary>
        /// 判断是否为网络层错误(可尝试直连重试)
        /// </summary>
        private static bool IsNetworkError(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is System.Net.Http.HttpRequestException || e is System.IO.IOException
                    || e is System.Net.Sockets.SocketException || e is TaskCanceledException)
                    return true;
            }
            return false;
        }
    }
}
