using LinePutScript;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

        public AITalkBox(MainPlugin mainPlugin) : base(mainPlugin) { }

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
        #endregion

        /// <summary>
        /// 生成注入桌宠实时状态的 system prompt
        /// </summary>
        private string BuildSystemPrompt()
        {
            var sb = new StringBuilder(Persona);
            sb.AppendLine();
            sb.AppendLine("【要求】用中文口语回复, 保持角色扮演, 一次回复控制在60字以内, 不要使用markdown、列表或emoji。");
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
                await ChatStreamAsync(text);
            }
            catch (Exception e)
            {
                Say($"呜... AI 接口出错了: {e.Message}", true);
            }
        }

        /// <summary>
        /// 调用 Chat Completions 接口 (SSE流式), 桌宠边思考边打字机式说话
        /// </summary>
        private async Task ChatStreamAsync(string text)
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
                max_tokens = 300,
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, APIUrl);
            request.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", APIKey);
            request.Headers.TryAddWithoutValidation("Accept", "text/event-stream");

            var say = new SayInfoWithStream("say", true);
            var replyBuilder = new StringBuilder();
            say.Event_Finish += (full) =>
            {
                lock (history)
                {
                    history.Add(("user", text));
                    history.Add(("assistant", full));
                    while (history.Count > HistoryLength * 2)
                        history.RemoveAt(0);
                }
            };
            DisplayThinkToSayRnd(say);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"HTTP {(int)response.StatusCode}: {Truncate(err.Replace('\n', ' '), 120)}");
            }
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line) || !line.StartsWith("data:"))
                    continue;
                var data = line.Substring(5).Trim();
                if (data == "[DONE]")
                    break;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                    {
                        var piece = content.GetString();
                        if (!string.IsNullOrEmpty(piece))
                            say.UpdateText(replyBuilder.Append(piece).ToString());
                    }
                }
                catch { }
            }
            say.FinishGenerate();
            if (replyBuilder.Length == 0)
            {
                say.UpdateAllText("......");
                say.FinishGenerate();
            }
        }

        /// <summary>
        /// 让桌宠直接说话 (非流式, 用于错误提示等)
        /// </summary>
        private void Say(string text, bool force = false)
        {
            MainPlugin.MW.Dispatcher.Invoke(() => MainPlugin.MW.Main.SayRnd(text, force));
        }

        private static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "...";

        public override void Setting()
        {
            MainPlugin.MW.Dispatcher.Invoke(() =>
            {
                var win = new winAITalkSetting(this) { Owner = null };
                win.Topmost = true;
                win.Show();
                win.Activate();
            });
        }

        /// <summary>
        /// 清空对话历史
        /// </summary>
        public void ClearHistory()
        {
            lock (history)
                history.Clear();
        }

        public void Dispose() { }
    }
}
