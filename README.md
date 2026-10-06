# 虚拟桌宠（定制版）

一只住在你桌面上的桌宠：**会陪你聊天、能投喂互动、会打工学习，还能帮你管理任务、删除文件。**

> 本项目是个人学习与定制作品，在他人开源的桌宠代码基础上修改而来，**完全开源**（详见文末[开源声明与致谢](#开源声明与致谢)）。

![主图](README.assets/%E4%B8%BB%E5%9B%BE.png)

---

## 📖 文档导航

| 文档 | 内容 |
|------|------|
| **👉 [功能与特性完全指南（GUIDE.md）](./GUIDE.md)** | **这款桌宠的全部功能、使用教程、构建部署与问题排查** |
| [二次开发支持文档](./Secondary%20Development%20Support%20Documentation.md) | MOD 与代码插件开发 |
| [上游原版文档](./README_zht.md) | [繁體中文](./README_zht.md) · [English](./README_en.md) · [日本語](./README_ja.md) |

---

## ✨ 它都能做什么

### 一只真正的桌面伙伴

- 常驻桌面、无边框透明、不打扰工作；会自己散步、趴在屏幕边缘探头、发呆、睡觉
- 完整的养成数值：饱食度、口渴度、心情、体力、健康、金钱、经验等级、好感度
- 投喂（食物/饮料/药品/礼品）、打工赚钱、学习升级、玩耍睡觉
- 数十种动作动画，会随状态（正常/开心/状态不佳/生病）呈现不同表现
- 多存档、快捷键、托盘管理、图库收藏、生日惊喜

### 本版本的特色能力

- 🤖 **AI 智能聊天**——接入任意 OpenAI 兼容大模型（智谱 / DeepSeek / OpenAI / 商汤 SenseNova…），给它性格人设，它知道自己饿不饿、心情好不好，像真正的伙伴一样和你对话
- 📝 **任务清单管家**——本日 / 本周 / 本月待办，勾选完成；每天 **8 点提醒当天任务、14 点催你完成、20 点汇报完成情况**
- 🗑 **拖拽删除文件**——把不想要的文件拖到桌宠身上，确认后彻底删除，桌面清理从未如此解压
- 💬 **右键气泡菜单**——在桌宠身边弹出气泡式功能菜单，比传统菜单更直观可爱
- 🖐 **顺滑的拖动与触碰**——全身任意点可拖、1:1 跟手不跳变；悬停头/腰/腿有不同触碰反应
- 🛡 **防走丢保护**——桌宠行走/拖动智能限制在屏幕内，再也不会"跑丢"

> 每个功能的详细用法、配置教程与常见问题，见 **[GUIDE.md](./GUIDE.md)**。

### 无限扩展

动画角色、物品、工作、台词、主题、语言、代码插件——全部可以通过 MOD 替换与添加，也可以在创意工坊分享与订阅。想让桌宠变成你自己的角色？换一套动画包即可。

---

## 🚀 快速开始

**环境要求**：Windows 10/11（x64）+ [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（`winget install Microsoft.DotNet.SDK.10`）

```bash
# 构建主程序
dotnet build "VPet-Simulator.Windows/VPet-Simulator.Windows.csproj" -c Debug -p:Platform=x64

# 构建 AI 聊天插件（自动部署到 mod 目录）
dotnet build "VPet.Plugin.AITalk/VPet.Plugin.AITalk.csproj" -c Debug

# 运行
./VPet-Simulator.Windows/bin/x64/Debug/net10.0-windows7.0/VPet-Simulator.Windows.exe
```

详细构建说明、MOD 目录链接方式（`mklink`）与插件加载规则见 [GUIDE.md 第七章](./GUIDE.md#七构建运行与部署)。

---

## 📂 数据与日志

| 文件 | 位置 | 说明 |
|------|------|------|
| `Tasks.json` | 程序目录 | 任务清单与提醒记录 |
| `Setting.lps` | 程序目录 | 设置（含 AI 配置） |
| `Saves/` | 程序目录 | 游戏存档 |
| `ai_talk.log` | `mod/1112_AITalk/` | AI 聊天日志（连接问题排查用） |

---

## 开源声明与致谢

**本桌宠完全开源**，代码采用 [Apache License 2.0](./LICENSE)，任何人都可以自由使用、修改与分发。

这只桌宠**并非从零实现，而是在他人开源的桌宠代码基础上学习、修改而来**。在此郑重感谢：

- 🙏 **特别感谢 [LorisYounger](https://github.com/LorisYounger)**——原版「虚拟桌宠模拟器 / 虚拟主播模拟器」的作者。本项目的核心玩法、养成系统、动画框架与 MOD 架构均源自他慷慨开源的 [VPet 项目](https://github.com/LorisYounger/VPet)。没有他的开源，就没有今天这只桌宠。
- 感谢 [VPet 项目](https://github.com/LorisYounger/VPet) 的全体贡献者，以及创意工坊中分享内容的创作者们。
- 感谢本项目所用开源组件的所有作者。

> **版权与许可说明**：本仓库代码遵循 Apache License 2.0；仓库内自带的桌宠动画与图片素材来自上游项目，其版权归[虚拟主播模拟器制作组](https://www.exlb.net/VUP-Simulator)所有，使用与分发请遵守[上游动画版权声明与授权](https://github.com/LorisYounger/VPet#%E5%8A%A8%E7%94%BB%E7%89%88%E6%9D%83%E5%A3%B0%E6%98%8E%E4%B8%8E%E6%8E%88%E6%9D%83)。

如果你喜欢这只桌宠，欢迎去给[原项目](https://github.com/LorisYounger/VPet)点一个 ⭐，或在 [Steam（免费）](https://store.steampowered.com/app/1920960/VPet) 支持原作者。

---

## 上游资源

- 原项目仓库：<https://github.com/LorisYounger/VPet>
- 原版 Steam 页面：<https://store.steampowered.com/app/1920960/VPet>
- 原版文档：[繁體中文](./README_zht.md) · [English](./README_en.md) · [日本語](./README_ja.md)
