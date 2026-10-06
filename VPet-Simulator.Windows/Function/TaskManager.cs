using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using VPet_Simulator.Windows.Interface;

namespace VPet_Simulator.Windows
{
    /// <summary>
    /// 任务范围
    /// </summary>
    public enum TaskScope
    {
        /// <summary>本日</summary>
        Day,
        /// <summary>本周</summary>
        Week,
        /// <summary>本月</summary>
        Month,
    }

    /// <summary>
    /// 单条任务
    /// </summary>
    public class TaskItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Content { get; set; } = "";
        public TaskScope Scope { get; set; } = TaskScope.Day;
        /// <summary>所属周期(创建时确定): 日=yyyy-MM-dd, 周=yyyy-Www, 月=yyyy-MM</summary>
        public string PeriodKey { get; set; } = "";
        public bool Done { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? DoneAt { get; set; }
    }

    /// <summary>
    /// 持久化数据
    /// </summary>
    public class TaskData
    {
        public List<TaskItem> Tasks { get; set; } = new();
        /// <summary>提醒触发记录: "8"/"14"/"20" -> 日期(yyyy-MM-dd), 保证每天每档只弹一次</summary>
        public Dictionary<string, string> ReminderFired { get; set; } = new();
    }

    /// <summary>
    /// 任务清单管理器: 数据与持久化 (Tasks.json, 位于游戏目录)
    /// </summary>
    public static class TaskManager
    {
        private static readonly object lockObj = new();
        private static readonly JsonSerializerOptions jsonOpts = new() { WriteIndented = true };

        private static string FilePath => Path.Combine(ExtensionValue.BaseDirectory, "Tasks.json");

        private static TaskData? data;
        public static TaskData Data
        {
            get
            {
                if (data == null)
                    Load();
                return data!;
            }
        }

        /// <summary>
        /// 从磁盘加载
        /// </summary>
        public static void Load()
        {
            lock (lockObj)
            {
                try
                {
                    if (File.Exists(FilePath))
                        data = JsonSerializer.Deserialize<TaskData>(File.ReadAllText(FilePath)) ?? new TaskData();
                    else
                        data = new TaskData();
                }
                catch
                {
                    data = new TaskData();
                }
            }
        }

        /// <summary>
        /// 保存到磁盘
        /// </summary>
        public static void Save()
        {
            lock (lockObj)
            {
                try
                {
                    File.WriteAllText(FilePath, JsonSerializer.Serialize(Data, jsonOpts));
                }
                catch { }
            }
        }

        /// <summary>
        /// 计算某天在指定范围下的周期键
        /// </summary>
        public static string PeriodKeyOf(TaskScope scope, DateTime day) => scope switch
        {
            TaskScope.Day => day.ToString("yyyy-MM-dd"),
            TaskScope.Week => $"{ISOWeek.GetYear(day):D4}-W{ISOWeek.GetWeekOfYear(day):D2}",
            TaskScope.Month => day.ToString("yyyy-MM"),
            _ => day.ToString("yyyy-MM-dd"),
        };

        /// <summary>
        /// 当前周期的任务 (未完成在前)
        /// </summary>
        public static List<TaskItem> Current(TaskScope scope)
        {
            var key = PeriodKeyOf(scope, DateTime.Now);
            lock (lockObj)
            {
                return Data.Tasks.Where(t => t.Scope == scope && t.PeriodKey == key)
                    .OrderBy(t => t.Done).ThenBy(t => t.CreatedAt).ToList();
            }
        }

        /// <summary>
        /// 添加任务
        /// </summary>
        public static TaskItem Add(string content, TaskScope scope)
        {
            var t = new TaskItem
            {
                Content = content.Trim(),
                Scope = scope,
                PeriodKey = PeriodKeyOf(scope, DateTime.Now),
                CreatedAt = DateTime.Now,
            };
            lock (lockObj)
            {
                Data.Tasks.Add(t);
                //顺带清理: 已完成且超过60天的旧任务不再保留
                Data.Tasks.RemoveAll(x => x.Done && x.DoneAt.HasValue && (DateTime.Now - x.DoneAt.Value).TotalDays > 60);
            }
            Save();
            return t;
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        public static void Remove(string id)
        {
            lock (lockObj)
            {
                Data.Tasks.RemoveAll(t => t.Id == id);
            }
            Save();
        }

        /// <summary>
        /// 勾选/取消完成
        /// </summary>
        public static void SetDone(string id, bool done)
        {
            lock (lockObj)
            {
                var t = Data.Tasks.Find(t => t.Id == id);
                if (t == null)
                    return;
                t.Done = done;
                t.DoneAt = done ? DateTime.Now : null;
            }
            Save();
        }

        /// <summary>
        /// 完成进度 (已完成数, 总数)
        /// </summary>
        public static (int Done, int Total) Progress(TaskScope scope)
        {
            var list = Current(scope);
            return (list.Count(t => t.Done), list.Count);
        }

        /// <summary>
        /// 判断某档提醒今天是否已触发
        /// </summary>
        public static bool IsReminderFiredToday(int checkpoint)
        {
            lock (lockObj)
            {
                return Data.ReminderFired.TryGetValue(checkpoint.ToString(), out var d)
                    && d == DateTime.Now.ToString("yyyy-MM-dd");
            }
        }

        /// <summary>
        /// 标记某档提醒今天已触发
        /// </summary>
        public static void MarkReminderFired(int checkpoint)
        {
            lock (lockObj)
            {
                Data.ReminderFired[checkpoint.ToString()] = DateTime.Now.ToString("yyyy-MM-dd");
            }
            Save();
        }
    }
}
