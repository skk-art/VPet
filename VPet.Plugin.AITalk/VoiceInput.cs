using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;

namespace VPet.Plugin.AITalk
{
    /// <summary>
    /// 语音输入: 使用 Windows 内置语音识别 (离线, 免费)
    /// 点击麦克风后聆听一次, 识别出文字; 若为指令则执行对应动作, 否则作为聊天内容发送
    /// </summary>
    public class VoiceInput : IDisposable
    {
        private SpeechRecognitionEngine? engine;
        private bool engineReady;
        private bool initFailed;

        /// <summary>识别到最终文本 (已去除标点空白)</summary>
        public event Action<string>? Recognized;
        /// <summary>状态文本变化 (如"聆听中...")</summary>
        public event Action<string>? StatusChanged;
        /// <summary>出错/提示信息</summary>
        public event Action<string>? Failed;

        public bool IsListening { get; private set; }

        /// <summary>
        /// 系统是否支持语音识别 (存在任意识别器)
        /// </summary>
        public static bool IsSupported
        {
            get
            {
                try { return SpeechRecognitionEngine.InstalledRecognizers().Count > 0; }
                catch { return false; }
            }
        }

        /// <summary>
        /// 初始化识别引擎 (懒加载)
        /// </summary>
        private bool EnsureEngine()
        {
            if (engineReady)
                return true;
            if (initFailed)
                return false;
            try
            {
                var recs = SpeechRecognitionEngine.InstalledRecognizers();
                if (recs.Count == 0)
                {
                    initFailed = true;
                    Failed?.Invoke("系统未安装语音识别, 请在 Windows 设置→时间和语言→语音 中添加语音包");
                    return false;
                }
                //优先中文识别器, 没有则用第一个
                var info = recs.FirstOrDefault(r => r.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) ?? recs[0];
                engine = new SpeechRecognitionEngine(info);
                //录音设备: 默认麦克风
                engine.SetInputToDefaultAudioDevice();
                //听写语法 (自由说话)
                engine.LoadGrammar(new DictationGrammar());
                //常用指令语法 (提高指令识别率)
                engine.LoadGrammar(BuildCommandGrammar());
                engine.InitialSilenceTimeout = TimeSpan.FromSeconds(6);
                engine.BabbleTimeout = TimeSpan.FromSeconds(6);
                engine.EndSilenceTimeout = TimeSpan.FromSeconds(1.2);
                engine.SpeechRecognized += (s, e) =>
                {
                    if (!IsListening)
                        return;//已被取消(超时/手动停止)后迟到的识别结果, 丢弃
                    var text = e.Result?.Text;
                    if (string.IsNullOrWhiteSpace(text))
                        return;
                    Stop();
                    Recognized?.Invoke(Normalize(text));
                };
                engine.RecognizeCompleted += (s, e) =>
                {
                    if (e.Error != null)
                    {
                        IsListening = false;
                        StatusChanged?.Invoke("");
                        Failed?.Invoke("语音识别出错: " + e.Error.Message);
                        return;
                    }
                    //完成但没有识别到内容
                    if (e.Result == null && IsListening)
                    {
                        IsListening = false;
                        StatusChanged?.Invoke("");
                        Failed?.Invoke(e.Cancelled ? "已取消聆听" : "没有听清, 再点一次试试~");
                    }
                };
                engineReady = true;
                return true;
            }
            catch (Exception ex)
            {
                initFailed = true;
                EngineLog("初始化失败: " + ex.Message);
                Failed?.Invoke(ex.Message.Contains("No audio") || ex.Message.Contains("音频")
                    ? "没有找到麦克风, 请检查录音设备"
                    : "语音识别初始化失败: " + ex.Message);
                return false;
            }
        }

        private static void EngineLog(string msg) => AITalkBox.Log("[语音] " + msg);

        /// <summary>
        /// 指令关键词语法 (与文本匹配规则互补, 提升识别率)
        /// </summary>
        private static Grammar BuildCommandGrammar()
        {
            var words = new List<string>();
            foreach (var cmd in VoiceCommands)
                words.AddRange(cmd.Keywords);
            var choices = new Choices(words.ToArray());
            var gb = new GrammarBuilder(choices);
            gb.Culture = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
            return new Grammar(gb);
        }

        /// <summary>
        /// 单击麦克风: 聆听一次 (带10秒守护超时, 不依赖系统识别器的超时行为)
        /// </summary>
        private int listenSeq;
        public void ListenOnce()
        {
            if (IsListening)
            {
                Stop();
                return;
            }
            if (!EnsureEngine())
                return;
            try
            {
                engine!.RecognizeAsyncCancel();
                IsListening = true;
                StatusChanged?.Invoke("聆听中…请说话");
                EngineLog("开始聆听");
                var seq = ++listenSeq;
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(10000);
                    if (IsListening && seq == listenSeq)
                    {//守护超时: 一直没有识别结果或一直没有说话
                        Stop();
                        Failed?.Invoke("没有听清, 再点一次试试~");
                    }
                });
                engine.RecognizeAsync(RecognizeMode.Single);
            }
            catch (Exception ex)
            {
                IsListening = false;
                StatusChanged?.Invoke("");
                Failed?.Invoke("开始录音失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 停止聆听
        /// </summary>
        public void Stop()
        {
            try { engine?.RecognizeAsyncCancel(); } catch { }
            IsListening = false;
            StatusChanged?.Invoke("");
        }

        /// <summary>
        /// 文本归一化: 去掉标点/空白, 便于指令匹配
        /// </summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            return new string(text.Where(c => !char.IsPunctuation(c) && !char.IsSymbol(c) && !char.IsWhiteSpace(c)).ToArray()).Trim();
        }

        #region 语音指令
        public class VoiceCommand
        {
            /// <summary>关键词 (出现任意一个即命中)</summary>
            public string[] Keywords = Array.Empty<string>();
            /// <summary>动作名 (日志与提示用)</summary>
            public string Name = "";
            /// <summary>执行动作, 返回是否成功</summary>
            public Func<bool> Action = () => false;
        }

        /// <summary>
        /// 指令表 (由 AITalkBox 在初始化时填充动作实现)
        /// </summary>
        public static List<VoiceCommand> VoiceCommands { get; } = new();

        /// <summary>
        /// 尝试把识别文本作为指令执行
        /// 仅短句可能是指令 (长句是聊天内容/环境噪音转写, 避免误触发)
        /// </summary>
        /// <param name="text">归一化后的文本</param>
        /// <returns>命中的指令名; 未命中返回 null</returns>
        public static string? TryExecuteCommand(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 12)
                return null;
            foreach (var cmd in VoiceCommands)
            {
                if (cmd.Keywords.Any(k => text.Contains(k)))
                {
                    try
                    {
                        if (cmd.Action())
                        {
                            EngineLog($"执行指令「{cmd.Name}」(语音: {text})");
                            return cmd.Name;
                        }
                    }
                    catch (Exception ex)
                    {
                        EngineLog($"指令「{cmd.Name}」执行失败: {ex.Message}");
                    }
                }
            }
            return null;
        }
        #endregion

        public void Dispose()
        {
            try
            {
                engine?.RecognizeAsyncCancel();
                engine?.Dispose();
            }
            catch { }
            engine = null;
            engineReady = false;
        }
    }
}
