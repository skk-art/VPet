using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace VPet.Plugin.AITalk
{
    /// <summary>
    /// 一条长期记忆
    /// </summary>
    public class MemoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime Time { get; set; } = DateTime.Now;
        /// <summary>记忆内容</summary>
        public string Content { get; set; } = "";
        /// <summary>来源: auto=对话自动提炼, manual=手动添加</summary>
        public string Source { get; set; } = "auto";
    }

    /// <summary>
    /// AI 长期记忆与知识库管理
    /// - 知识库(AI_Knowledge.txt): 用户手动维护的"关于我"信息, 每次聊天都会注入给 AI
    /// - 长期记忆(AI_Memories.json): 从每次对话中自动提炼的关于主人的信息, 也可手动增删
    /// </summary>
    public static class AITalkMemory
    {
        private static readonly object lockObj = new();
        private static readonly JsonSerializerOptions jsonOpts = new() { WriteIndented = true };

        /// <summary>记忆/知识库文件所在目录 (与日志同目录, 即 MOD 文件夹)</summary>
        public static string Dir => Path.GetDirectoryName(AITalkBox.LogPath) ?? ".";
        /// <summary>知识库文件 (关于我的信息, 纯文本)</summary>
        public static string ProfilePath => Path.Combine(Dir, "AI_Knowledge.txt");
        /// <summary>长期记忆文件</summary>
        public static string MemoryPath => Path.Combine(Dir, "AI_Memories.json");
        /// <summary>聊天记录文件 (逐轮追加, 可翻阅)</summary>
        public static string ChatLogPath => Path.Combine(Dir, "AI_ChatLog.txt");

        #region 知识库 (用户档案)
        private static string? profile;
        /// <summary>关于主人的知识库文本 (每轮对话都会注入给AI)</summary>
        public static string Profile
        {
            get
            {
                if (profile == null)
                {
                    try
                    {
                        profile = File.Exists(ProfilePath) ? File.ReadAllText(ProfilePath) : "";
                    }
                    catch { profile = ""; }
                }
                return profile;
            }
            set
            {
                lock (lockObj)
                {
                    profile = value ?? "";
                    try { File.WriteAllText(ProfilePath, profile); } catch { }
                }
            }
        }
        #endregion

        #region 长期记忆
        private static List<MemoryEntry>? memories;
        public static List<MemoryEntry> Memories
        {
            get
            {
                if (memories == null)
                {
                    lock (lockObj)
                    {
                        if (memories == null)
                        {
                            try
                            {
                                memories = File.Exists(MemoryPath)
                                    ? JsonSerializer.Deserialize<List<MemoryEntry>>(File.ReadAllText(MemoryPath)) ?? new()
                                    : new();
                            }
                            catch { memories = new(); }
                        }
                    }
                }
                return memories!;
            }
        }

        private static void SaveMemories()
        {
            lock (lockObj)
            {
                try { File.WriteAllText(MemoryPath, JsonSerializer.Serialize(Memories, jsonOpts)); } catch { }
            }
        }

        /// <summary>
        /// 归一化比较用文本 (去空白与标点)
        /// </summary>
        private static string Normalize(string s)
            => new string((s ?? "").Where(c => !char.IsWhiteSpace(c) && !char.IsPunctuation(c) && !char.IsSymbol(c)).ToArray()).ToLowerInvariant();

        /// <summary>
        /// 是否与已有记忆重复 (包含关系也算重复, 防止"喜欢冰美式"与"最喜欢的饮料是冰美式"重复入库)
        /// </summary>
        public static bool IsDuplicate(string content)
        {
            var n = Normalize(content);
            if (n.Length < 2)
                return true;
            lock (lockObj)
            {
                foreach (var m in Memories)
                {
                    var e = Normalize(m.Content);
                    if (e.Length == 0)
                        continue;
                    if (e == n || e.Contains(n) || n.Contains(e))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 新增一条记忆 (去重), 返回是否成功新增
        /// </summary>
        public static bool Add(string content, string source = "auto")
        {
            content = (content ?? "").Trim().TrimStart('-', '•', '*').Trim();
            if (content.Length < 2 || IsDuplicate(content))
                return false;
            lock (lockObj)
            {
                Memories.Add(new MemoryEntry { Content = content, Source = source });
                //上限保护: 最多保留1000条, 超出时移除最旧的手动记忆除外
                if (Memories.Count > 1000)
                {
                    var oldest = Memories.Where(m => m.Source == "auto").OrderBy(m => m.Time).FirstOrDefault();
                    if (oldest != null)
                        Memories.Remove(oldest);
                }
            }
            SaveMemories();
            return true;
        }

        /// <summary>
        /// 删除一条记忆
        /// </summary>
        public static void Remove(string id)
        {
            lock (lockObj)
            {
                Memories.RemoveAll(m => m.Id == id);
            }
            SaveMemories();
        }

        /// <summary>
        /// 清空全部记忆 (保留知识库)
        /// </summary>
        public static void ClearAll()
        {
            lock (lockObj)
            {
                Memories.Clear();
            }
            SaveMemories();
        }

        /// <summary>
        /// 追加一条聊天记录 (持久化, 任何时候的聊天都会保存)
        /// </summary>
        public static void AppendChatLog(string userText, string replyText)
        {
            try
            {
                File.AppendAllText(ChatLogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 主人: {userText}\r\n" +
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 萝莉斯: {replyText}\r\n\r\n");
            }
            catch { }
        }
        #endregion

        /// <summary>
        /// 构造注入 system prompt 的知识库+记忆段落 (控制长度防超token)
        /// </summary>
        /// <param name="maxMemories">最多注入的记忆条数</param>
        public static string BuildPromptSection(int maxMemories = 120)
        {
            var sb = new StringBuilder();
            var p = Profile.Trim();
            if (p.Length > 0)
            {
                if (p.Length > 2000)
                    p = p.Substring(0, 2000) + "…";
                sb.AppendLine("【主人档案 · 你已知晓的信息】");
                sb.AppendLine(p);
            }
            List<MemoryEntry> mems;
            lock (lockObj) { mems = Memories.ToList(); }
            if (mems.Count > 0)
            {
                sb.AppendLine("【你对主人的长期记忆】(平时对话中自然运用这些了解, 但不要生硬地逐条复述)");
                foreach (var m in mems.TakeLast(maxMemories))
                    sb.AppendLine("- " + m.Content);
            }
            return sb.ToString();
        }
    }
}
