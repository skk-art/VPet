using LinePutScript;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VPet_Simulator.Core;
using VPet_Simulator.Windows.Interface;
using static VPet_Simulator.Core.GraphInfo;

namespace VPet.Plugin.AITalk
{
    /// <summary>
    /// AI 聊天框: 用户自由输入, 通过 OpenAI 兼容 Chat Completions 接口生成个性化回复
    /// 结合角色人设(MOD/用户自定义)与桌宠实时状态(心情/饱食/金钱/好感度等)注入 system prompt
    /// </summary>
    public class AITalkBox : TalkBox
    {
        public override string APIName => "AITalk";

        /// <summary>
        /// 对话历史 (role, content)
        /// </summary>
        private readonly List<(string role, string content)> history = new();

        private static readonly HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(90)
        };
        /// <summary>
        /// 直连客户端 (不走系统代理), 用于代理路径失败时自动重试
        /// </summary>
        private static readonly HttpClient clientNoProxy = new(new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(30),
        })
        {
            Timeout = TimeSpan.FromSeconds(90)
        };

        /// <summary>
        /// 规范化API地址: 用户只填了域名或 /v1 等版本段时自动补全 /chat/completions
        /// </summary>
        public static string NormalizeUrl(string url)
        {
            var u = (url ?? "").Trim().TrimEnd('/');
            if (u.Length == 0)
                return u;
            if (u.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                return u;
            if (System.Text.RegularExpressions.Regex.IsMatch(u, @"/v\d+[a-z]*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return u + "/chat/completions";//形如 /v1 /v4 /v1beta 等版本段
            try
            {
                var uri = new Uri(u);
                if (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/")
                    return u + "/v1/chat/completions";//只填了域名
            }
            catch { }
            return u;
        }
        /// <summary>
        /// 密钥格式防呆校验: 常见误填(把API地址/整段配置粘进密钥栏)
        /// </summary>
        /// <returns>有问题时返回中文提示, 否则返回null</returns>
        public static string? CheckKeyLooksValid(string key)
        {
            var k = (key ?? "").Trim();
            if (k.Length == 0)
                return null;
            if (k.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || k.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return "主人, 「API 密钥」栏里填的好像是一个网址…\n请把网址填到「API 地址」栏; 「API 密钥」栏要填服务商控制台创建的 API Key";
            if (k.Contains("sensenova.cn") || k.Contains("bigmodel.cn") || k.Contains("deepseek.com") || k.Contains("openai.com"))
                return "主人, 「API 密钥」栏里填的好像是网址的一部分…\n「API 密钥」栏请填服务商控制台创建的 API Key (一串字母数字), 网址请填到「API 地址」栏";
            if (k.Contains(' '))
                return "主人, 「API 密钥」里有空格, 请检查是否复制了多余字符~";
            return null;
        }
        /// <summary>
        /// 判断是否为网络层错误(可尝试换网络路径重试)
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
        /// <summary>
        /// 发送请求: 默认走系统网络设置; 若失败(TLS/连接等网络问题)自动改用直连重试一次
        /// (对开了代理但节点抖动/规则错误的环境特别有效)
        /// </summary>
        private static async Task<HttpResponseMessage> SendWithFallbackAsync(Func<HttpRequestMessage> makeRequest, HttpCompletionOption option, string tag)
        {
            try
            {
                return await client.SendAsync(makeRequest(), option);
            }
            catch (Exception ex) when (IsNetworkError(ex))
            {
                Log($"⚠ {tag} 系统网络路径失败({ex.GetType().Name}: {Truncate(ex.Message, 120)}), 改用直连重试");
                return await clientNoProxy.SendAsync(makeRequest(), option);
            }
        }

        public AITalkBox(MainPlugin mainPlugin) : base(mainPlugin)
        {
            LoadHistory();
            InitVoiceInput();
        }

        #region 语音输入
        private VoiceInput? voice;
        private System.Windows.Controls.Button? btnVoice;
        private System.Windows.Controls.TextBlock? txtVoiceStatus;

        /// <summary>
        /// 初始化语音输入: 聊天框内添加麦克风按钮, 注册语音指令
        /// </summary>
        private void InitVoiceInput()
        {
            try
            {
                if (!VoiceInput.IsSupported)
                {
                    Log("[语音] 系统未安装语音识别器, 语音输入不可用");
                    return;
                }
                //语音指令表 (仅匹配短句; 未命中指令的语音会作为聊天内容发给AI)
                VoiceInput.VoiceCommands.Clear();
                VoiceInput.VoiceCommands.AddRange(new[]
                {
                    new VoiceInput.VoiceCommand { Name = "睡觉", Keywords = new[] { "睡觉", "休息一下", "困了", "去睡" }, Action = DoSleep },
                    new VoiceInput.VoiceCommand { Name = "起床", Keywords = new[] { "起床", "醒醒", "别睡了" }, Action = DoWakeUp },
                    new VoiceInput.VoiceCommand { Name = "停止工作", Keywords = new[] { "停止工作", "停止学习", "别干了" }, Action = DoStopWork },
                    new VoiceInput.VoiceCommand { Name = "打开投喂", Keywords = new[] { "投喂", "喂我", "吃点东西" }, Action = DoOpenFeed },
                    new VoiceInput.VoiceCommand { Name = "任务清单", Keywords = new[] { "任务清单", "待办事项", "待办", "查看任务" }, Action = DoOpenTasks },
                });
                voice = new VoiceInput();
                voice.StatusChanged += s => MainPlugin.MW.Dispatcher.Invoke(() =>
                {
                    if (btnVoice != null)
                        btnVoice.Content = string.IsNullOrEmpty(s) ? "🎤 点击说话" : "🔴 " + s;
                });
                voice.Failed += msg => MainPlugin.MW.Dispatcher.Invoke(() =>
                {
                    if (txtVoiceStatus != null)
                        txtVoiceStatus.Text = msg;
                    Log("[语音] " + msg);
                });
                voice.Recognized += OnVoiceRecognized;

                //聊天框底部添加麦克风按钮与状态文字
                var panel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(2, 6, 2, 0),
                };
                btnVoice = new System.Windows.Controls.Button
                {
                    Content = "🎤 点击说话",
                    FontSize = 22,
                    Padding = new Thickness(14, 4, 14, 4),
                    BorderThickness = new Thickness(2),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "语音输入: 直接说话即可与桌宠聊天;\n也支持语音指令: 睡觉 / 起床 / 停止工作 / 投喂 / 任务清单",
                };
                btnVoice.SetResourceReference(FrameworkElement.StyleProperty, "ThemedButtonStyle");
                Panuon.WPF.UI.ButtonHelper.SetCornerRadius(btnVoice, new CornerRadius(4));
                btnVoice.Click += (s, e) => voice?.ListenOnce();
                txtVoiceStatus = new System.Windows.Controls.TextBlock
                {
                    FontSize = 20,
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.85,
                };
                txtVoiceStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "DARKPrimary");
                panel.Children.Add(btnVoice);
                panel.Children.Add(txtVoiceStatus);
                PublicGrid.Children.Add(panel);
                Log("[语音] 语音输入已就绪 (识别器: " + System.Speech.Recognition.SpeechRecognitionEngine.InstalledRecognizers()[0].Culture.DisplayName + ")");
            }
            catch (Exception e)
            {
                Log("[语音] 初始化异常: " + e.Message);
            }
        }

        /// <summary>
        /// 识别到语音文本: 优先按指令执行, 否则作为聊天内容发送
        /// </summary>
        private void OnVoiceRecognized(string text)
        {
            MainPlugin.MW.Dispatcher.Invoke(() =>
            {
                try
                {
                    var cmd = VoiceInput.TryExecuteCommand(text);
                    if (cmd != null)
                    {
                        if (txtVoiceStatus != null)
                            txtVoiceStatus.Text = $"已执行语音指令「{cmd}」";
                        MainPlugin.MW.Main.SayRnd($"收到指令「{cmd}」~", true);
                        return;
                    }
                    if (txtVoiceStatus != null)
                        txtVoiceStatus.Text = $"识别: {text}";
                    tbTalk.Text = text;
                    btnSend.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                }
                catch (Exception e)
                {
                    Log("[语音] 处理失败: " + e.Message);
                }
            });
        }

        private bool DoSleep()
        {
            var m = MainPlugin.MW.Main;
            if (m.State == Main.WorkingState.Sleep)
                return false;
            if (m.State is Main.WorkingState.Nomal or Main.WorkingState.Travel)
            {
                m.DisplaySleep(true);
                return true;
            }
            m.WorkTimer?.Stop(() => m.DisplaySleep(true), WorkTimer.FinishWorkInfo.StopReason.MenualStop);
            return true;
        }

        private bool DoWakeUp()
        {
            var m = MainPlugin.MW.Main;
            if (m.State != Main.WorkingState.Sleep || m.Core.Save!.Mode == IGameSave.ModeType.Ill)
                return false;
            m.State = Main.WorkingState.Nomal;
            m.Display(GraphType.Sleep, AnimatType.C_End, m.DisplayNomal);
            return true;
        }

        private bool DoStopWork()
        {
            var m = MainPlugin.MW.Main;
            if (m.State != Main.WorkingState.Work)
                return false;
            m.WorkTimer?.Stop(() => m.DisplayNomal(), WorkTimer.FinishWorkInfo.StopReason.MenualStop);
            return true;
        }

        private bool DoOpenFeed()
        {
            var m = MainPlugin.MW.Main;
            m.ToolBar.Show();
            m.ToolBar.MenuFeed.IsSubmenuOpen = true;
            return true;
        }

        private bool DoOpenTasks()
        {
            var mi = MainPlugin.MW.GetType().GetMethod("ShowTaskList");
            if (mi == null)
                return false;
            mi.Invoke(MainPlugin.MW, new object?[] { null });
            return true;
        }
        #endregion

        #region 对话历史持久化
        private static string HistoryPath => Path.Combine(AITalkMemory.Dir, "AI_ChatHistory.json");
        private void LoadHistory()
        {
            try
            {
                if (File.Exists(HistoryPath))
                {
                    var list = JsonSerializer.Deserialize<List<string[]>>(File.ReadAllText(HistoryPath));
                    if (list != null)
                    {
                        lock (history)
                        {
                            foreach (var it in list.TakeLast(60))
                                if (it.Length == 2)
                                    history.Add((it[0], it[1]));
                        }
                    }
                }
            }
            catch { }
        }
        private void SaveHistory()
        {
            try
            {
                List<string[]> list;
                lock (history)
                {
                    list = history.TakeLast(60).Select(h => new[] { h.role, h.content }).ToList();
                }
                File.WriteAllText(HistoryPath, JsonSerializer.Serialize(list));
            }
            catch { }
        }
        #endregion

        #region 日志
        /// <summary>
        /// 日志文件路径 (MOD目录下 ai_talk.log, 用于排查接入问题)
        /// </summary>
        public static string LogPath
        {
            get
            {
                var dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
                return Path.GetFullPath(Path.Combine(dllDir, "..", "ai_talk.log"));
            }
        }
        /// <summary>
        /// 写入排查日志
        /// </summary>
        public static void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\n");
            }
            catch { }
        }
        private static string Mask(string key)
            => string.IsNullOrEmpty(key) ? "(未填写)" : key.Length <= 8 ? "****" : key.Substring(0, 4) + "****" + key.Substring(key.Length - 4);
        private static string Truncate(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "...");
        #endregion

        #region 设置读写
        /// <summary>API 完整地址 (OpenAI 兼容 chat/completions)</summary>
        public string APIUrl
        {
            get => MainPlugin.MW.Set["AITalk"].GetString("url", "https://open.bigmodel.cn/api/paas/v4/chat/completions");
            set => MainPlugin.MW.Set["AITalk"].SetString("url", value);
        }
        /// <summary>API 密钥</summary>
        public string APIKey
        {
            get => MainPlugin.MW.Set["AITalk"].GetString("key", "");
            set => MainPlugin.MW.Set["AITalk"].SetString("key", value);
        }
        /// <summary>模型名称</summary>
        public string Model
        {
            get => MainPlugin.MW.Set["AITalk"].GetString("model", "glm-4-flash");
            set => MainPlugin.MW.Set["AITalk"].SetString("model", value);
        }
        /// <summary>角色人设 (system prompt 主体)</summary>
        public string Persona
        {
            get => MainPlugin.MW.Set["AITalk"].GetString("persona",
                "你是桌面宠物「萝莉斯」, 一个活泼可爱、有点黏人的虚拟桌宠。称呼用户为主人。说话简短自然、语气俏皮。");
            set => MainPlugin.MW.Set["AITalk"].SetString("persona", value);
        }
        /// <summary>携带的历史对话轮数</summary>
        public int HistoryLength
        {
            get => MainPlugin.MW.Set["AITalk"].GetInt("historylength", 10);
            set => MainPlugin.MW.Set["AITalk"].SetInt("historylength", value);
        }
        /// <summary>是否在提示词中注入桌宠实时状态</summary>
        public bool InjectState
        {
            get => MainPlugin.MW.Set["AITalk"].GetBool("injectstate");
            set => MainPlugin.MW.Set["AITalk"].SetBool("injectstate", value);
        }
        /// <summary>是否自动从每次对话中提炼记忆 (默认开启; 存储的是"关闭"标记)</summary>
        public bool AutoMemory
        {
            get => !MainPlugin.MW.Set["AITalk"].GetBool("automemoryoff");
            set => MainPlugin.MW.Set["AITalk"].SetBool("automemoryoff", !value);
        }
        #endregion

        /// <summary>
        /// 生成注入桌宠实时状态的 system prompt
        /// </summary>
        private string BuildSystemPrompt()
        {
            var sb = new StringBuilder(Persona);
            sb.AppendLine();
            sb.AppendLine("【要求】用中文口语回复, 保持角色扮演, 一次回复控制在60字以内, 不要使用markdown、列表或emoji。");
            //知识库与长期记忆注入 (让AI了解主人)
            try
            {
                var mem = AITalkMemory.BuildPromptSection();
                if (!string.IsNullOrWhiteSpace(mem))
                    sb.AppendLine(mem);
            }
            catch { }
            if (!InjectState)
                return sb.ToString();
            try
            {
                var save = MainPlugin.MW.GameSavesData.GameSave;
                var main = MainPlugin.MW.Main;
                string mode = save.Mode switch
                {
                    IGameSave.ModeType.Happy => "开心",
                    IGameSave.ModeType.Nomal => "平静",
                    IGameSave.ModeType.PoorCondition => "状态不好",
                    IGameSave.ModeType.Ill => "生病了",
                    _ => "平静",
                };
                string doing;
                if (main.State == Main.WorkingState.Sleep)
                    doing = "正在睡觉";
                else if (main.State == Main.WorkingState.Work && main.NowWork != null)
                    doing = $"正在{main.NowWork.Name}";
                else if (main.State == Main.WorkingState.Travel)
                    doing = "正在旅行";
                else
                    doing = "正闲着";
                sb.AppendLine("【你当前的状态】(回复时自然代入, 不要罗列数据)");
                sb.AppendLine($"主人: {(string.IsNullOrWhiteSpace(save.HostName) ? "主人" : save.HostName)}; " +
                    $"等级: Lv.{save.Level}; 状态: {mode}; 此刻: {doing}");
                sb.AppendLine($"体力: {save.Strength:f0}/{save.StrengthMax:f0}; 心情: {save.Feeling:f0}/{save.FeelingMax:f0}; " +
                    $"饱食度: {save.StrengthFood:f0}/100; 口渴度: {save.StrengthDrink:f0}/100");
                sb.AppendLine($"金钱: ${save.Money:f0}; 好感度: {save.Likability:f0}/{save.LikabilityMax:f0}");
            }
            catch { }
            return sb.ToString();
        }

        /// <summary>
        /// 用户发送消息后回调 (后台线程)
        /// </summary>
        public override async void Responded(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            try
            {
                if (string.IsNullOrWhiteSpace(APIKey))
                {
                    Say("主人, 还没有配置 AI 接口哦, 马上为你打开设置窗口~");
                    Setting();
                    return;
                }
                var keyErr = CheckKeyLooksValid(APIKey);
                if (keyErr != null)
                {
                    Log("密钥格式异常: " + keyErr);
                    Say(keyErr, true);
                    Setting();
                    return;
                }
                var reply = await ChatStreamAsync(text);
                if (string.IsNullOrWhiteSpace(reply))
                {//推理模型的思考可能耗尽了token预算导致正文为空, 自动以更大预算重试一次
                    Log("⚠ 正文为空(思考可能耗尽token预算), 自动以更大预算重试一次");
                    var reply2 = await ChatStreamAsync(text, 8000);
                    if (string.IsNullOrWhiteSpace(reply2))
                        Say("……(模型这次没有说出话来, 再问一次试试?)", true);
                }
            }
            catch (Exception e)
            {
                Log("通讯异常: " + e.ToString());
                Say($"呜... AI 接口出错了: {FormatError(e)}", true);
            }
        }

        /// <summary>
        /// 把异常整理成用户能看懂的中文提示 (含常见原因判断)
        /// </summary>
        private static string FormatError(Exception e)
        {
            if (e is TaskCanceledException || e is OperationCanceledException)
                return "请求超时(90秒), 请检查网络是否可访问接口地址";
            var all = new List<string>();
            for (var x = e; x != null; x = x.InnerException)
                all.Add(x.Message);
            var s = string.Join(" <- ", all.Distinct());
            if (s.Contains("No such host is known") || s.Contains("nodename nor servname"))
                s = "域名无法解析(请检查 API 地址是否写错, 或本机 DNS/代理问题)";
            else if (s.Contains("Connection refused") || s.Contains("actively refused"))
                s = "目标拒绝连接(请检查 API 地址和端口是否正确)";
            else if (s.Contains("SSL") || s.Contains("TLS") || s.Contains("certificate"))
                s = "TLS/证书握手失败(如有代理软件请确认其正常工作)";
            return Truncate(s, 220) + " — 详情见日志 ai_talk.log";
        }

        /// <summary>
        /// 调用 Chat Completions 接口 (SSE流式), 桌宠边思考边打字机式说话
        /// 兼容: 非流式响应回退解析 / 推理模型的 reasoning 增量 / choices为空的usage事件
        /// </summary>
        /// <param name="maxTokens">本次请求的最大token预算 (推理模型思考也会消耗, 需给足)</param>
        private async Task<string> ChatStreamAsync(string text, int maxTokens = 4000)
        {
            DisplayThink();
            var messages = new List<(string role, string content)> { ("system", BuildSystemPrompt()) };
            lock (history)
            {
                messages.AddRange(history.TakeLast(HistoryLength * 2));
                messages.Add(("user", text));
            }

            var req = new
            {
                model = Model,
                messages = messages.Select(m => new { role = m.role, content = m.content }),
                stream = true,
                temperature = 0.8,
                max_tokens = maxTokens,
            };
            var url = NormalizeUrl(APIUrl);
            if (!string.Equals(url, APIUrl.Trim(), StringComparison.OrdinalIgnoreCase))
                Log($"地址自动补全: {APIUrl} → {url}");
            Log($"→ 请求 {url} model={Model} key={Mask(APIKey)} max_tokens={maxTokens} 输入=\"{Truncate(text, 80)}\"");
            HttpResponseMessage response = await SendWithFallbackAsync(() =>
            {
                var r = new HttpRequestMessage(HttpMethod.Post, url);
                r.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
                r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", APIKey);
                r.Headers.TryAddWithoutValidation("Accept", "text/event-stream");
                return r;
            }, HttpCompletionOption.ResponseHeadersRead, "聊天请求");
            using (response)
            {
            var ctype = response.Content.Headers.ContentType?.MediaType ?? "(无)";
            Log($"← 响应 HTTP {(int)response.StatusCode} {response.StatusCode} Content-Type={ctype}");
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                Log("← 错误响应体: " + Truncate(err, 800));
                throw new Exception($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(err.Replace('\n', ' ').Replace('\r', ' '), 240)}");
            }

            var say = new SayInfoWithStream("say", true);
            var replyBuilder = new StringBuilder();
            say.Event_Finish += (full) =>
            {
                lock (history)
                {
                    history.Add(("user", text));
                    history.Add(("assistant", full));
                    while (history.Count > 60)
                        history.RemoveAt(0);
                }
                SaveHistory();
                //持久化聊天记录 + 自动提炼长期记忆
                AITalkMemory.AppendChatLog(text, full);
                if (AutoMemory && !string.IsNullOrWhiteSpace(full))
                    _ = ExtractMemoriesAsync(text, full);
            };
            DisplayThinkToSayRnd(say);

            //服务端忽略 stream=true 时返回普通 JSON, 走非流式回退
            if (!ctype.Contains("event-stream") && !ctype.Contains("stream"))
            {
                var body = await response.Content.ReadAsStringAsync();
                Log($"非流式响应体({ctype}): " + Truncate(body, 1000));
                var content = TryParseFullContent(body);
                if (string.IsNullOrWhiteSpace(content))
                {
                    say.FinishGenerate();
                    Log("✗ 非流式响应无法解析出回复内容");
                    return "";
                }
                say.UpdateAllText(content);
                say.FinishGenerate();
                Log($"✓ 非流式解析成功, 回复{content.Length}字");
                return content;
            }

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                int parsed = 0, skipped = 0;
                string? firstBad = null;
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null)
                        break;
                    if (line.Length == 0 || !line.StartsWith("data:"))
                        continue;
                    var data = line.Substring(5).Trim();
                    if (data == "[DONE]")
                        break;
                    try
                    {
                        using var doc = JsonDocument.Parse(data);
                        var root = doc.RootElement;
                        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                        {//usage事件(choices为空)等, 正常跳过
                            skipped++;
                            continue;
                        }
                        var choice = choices[0];
                        if (!choice.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object)
                        {
                            skipped++;
                            continue;
                        }
                        if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                        {
                            var piece = content.GetString();
                            if (!string.IsNullOrEmpty(piece))
                            {
                                replyBuilder.Append(piece);
                                say.UpdateText(replyBuilder.ToString());
                            }
                        }
                        //推理模型的 reasoning/reasoning_content 增量: 忽略不计入正文
                        parsed++;
                    }
                    catch (JsonException)
                    {
                        skipped++;
                        firstBad ??= data;
                    }
                }
                say.FinishGenerate();
                Log($"✓ 流式完成: 正文{replyBuilder.Length}字, 解析{parsed}条, 跳过{skipped}条"
                    + (firstBad != null ? $", 首个无法解析数据: {Truncate(firstBad, 200)}" : ""));
            }
            return replyBuilder.ToString();
            }
        }

        /// <summary>
        /// 尝试从非流式响应体中解析回复内容 (兼容 choices[0].message.content / choices[0].text)
        /// </summary>
        private static string? TryParseFullContent(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() == 0)
                    return null;
                var c0 = choices[0];
                if (c0.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var mc) && mc.ValueKind == JsonValueKind.String)
                    return mc.GetString();
                if (c0.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                    return t.GetString();
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 让桌宠直接说话 (非流式, 用于错误提示等)
        /// </summary>
        private void Say(string text, bool force = false)
        {
            MainPlugin.MW.Dispatcher.Invoke(() => MainPlugin.MW.Main.SayRnd(text, force));
        }

        /// <summary>
        /// 从一轮对话中提炼关于主人的新信息, 存入长期记忆 (每轮对话后自动执行)
        /// </summary>
        private static readonly SemaphoreSlim extractLock = new(1, 1);
        private async Task ExtractMemoriesAsync(string userText, string replyText)
        {
            try
            {
                await extractLock.WaitAsync();
                try
                {
                    var existing = AITalkMemory.Memories.TakeLast(50).Select(m => "- " + m.Content).ToList();
                    var prompt = new StringBuilder();
                    prompt.AppendLine("你是记忆整理助手。下面是主人与桌宠「萝莉斯」的一段对话。");
                    prompt.AppendLine("请从中提取关于「主人」的、值得长期记住的新信息: 个人信息、喜好、习惯、计划、重要事件等。");
                    prompt.AppendLine("要求:");
                    prompt.AppendLine("1. 只提取对话中明确表达的信息, 不要推测");
                    prompt.AppendLine("2. 不要重复「已有记忆」中的内容");
                    prompt.AppendLine("3. 每条一行, 以\"- \"开头, 简洁陈述(不超过30字), 不要编号和其他说明文字");
                    prompt.AppendLine("4. 如果没有新信息, 只输出: 无");
                    if (existing.Count > 0)
                    {
                        prompt.AppendLine("已有记忆:");
                        foreach (var e in existing)
                            prompt.AppendLine(e);
                    }
                    prompt.AppendLine("对话:");
                    prompt.AppendLine("主人: " + userText);
                    prompt.AppendLine("萝莉斯: " + Truncate(replyText, 300));

                    var req = new
                    {
                        model = Model,
                        messages = new[] { new { role = "user", content = prompt.ToString() } },
                        stream = false,
                        temperature = 0.2,
                        max_tokens = 1200,
                    };
                    var url = NormalizeUrl(APIUrl);
                    using var response = await SendWithFallbackAsync(() =>
                    {
                        var r = new HttpRequestMessage(HttpMethod.Post, url);
                        r.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
                        r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", APIKey);
                        return r;
                    }, HttpCompletionOption.ResponseContentRead, "记忆提炼");
                    if (!response.IsSuccessStatusCode)
                    {
                        Log($"[记忆] 提炼请求失败 HTTP {(int)response.StatusCode}");
                        return;
                    }
                    var body = await response.Content.ReadAsStringAsync();
                    var content = TryParseFullContent(body);
                    if (string.IsNullOrWhiteSpace(content) || content.Trim().TrimEnd('。', '.') == "无")
                    {
                        Log("[记忆] 本轮无新增信息");
                        return;
                    }
                    int added = 0;
                    foreach (var line in content.Split('\n'))
                    {
                        var t = line.Trim();
                        if (!(t.StartsWith("-") || t.StartsWith("•") || t.StartsWith("*")))
                            continue;
                        t = t.TrimStart('-', '•', '*', ' ').Trim();
                        if (t.Length < 2 || t == "无")
                            continue;
                        if (AITalkMemory.Add(t))
                            added++;
                    }
                    Log($"[记忆] 提炼完成: 新增 {added} 条 (记忆库共 {AITalkMemory.Memories.Count} 条)");
                }
                finally
                {
                    extractLock.Release();
                }
            }
            catch (Exception e)
            {
                Log("[记忆] 提炼异常: " + e.Message);
            }
        }

        public override void Setting()
        {
            try
            {
                MainPlugin.MW.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var win = new winAITalkSetting(this) { Owner = null };
                        win.Topmost = true;
                        win.Show();
                        win.Activate();
                        Log("设置窗口已打开");
                    }
                    catch (Exception ex)
                    {
                        Log("打开设置窗口失败(UI线程): " + ex.ToString());
                        System.Windows.MessageBox.Show("打开设置窗口失败:\n" + ex.Message + "\n\n日志: " + LogPath, "AI 聊天");
                    }
                });
            }
            catch (Exception ex)
            {
                Log("打开设置窗口失败(调度): " + ex.ToString());
            }
        }

        /// <summary>
        /// 清空对话历史
        /// </summary>
        public void ClearHistory()
        {
            lock (history)
                history.Clear();
        }

        public void Dispose() { voice?.Dispose(); }
    }
}
