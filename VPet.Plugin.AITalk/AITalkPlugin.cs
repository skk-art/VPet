using LinePutScript;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.AITalk
{
    /// <summary>
    /// AI 聊天插件主体: 通过 OpenAI 兼容接口接入大语言模型, 让桌宠结合角色设定与实时状态进行个性化聊天
    /// </summary>
    public class AITalkPlugin : MainPlugin
    {
        /// <summary>
        /// 插件名称
        /// </summary>
        public override string PluginName => "AITalk";

        /// <summary>
        /// AI 聊天框
        /// </summary>
        private AITalkBox? talkBox;

        public AITalkPlugin(IMainWindow mainwin) : base(mainwin) { }

        public override void LoadPlugin()
        {
            //首次安装自动把聊天模式切换为自定义聊天接口 (仅一次, 之后尊重用户选择)
            if (!MW.Set["AITalk"][(gbol)"firstinit"])
            {
                MW.Set["AITalk"][(gbol)"firstinit"] = true;
                MW.Set["AITalk"].SetBool("injectstate", true);
                //无论当前是 LB/OFF/DIY, 首次安装都指向本插件的聊天接口
                //(旧档可能是 DIY 但接口名为空/已卸载的插件, 属于不可用状态)
                MW.Set["CGPT"][(gstr)"type"] = "DIY";
                MW.Set["CGPT"][(gstr)"DIY"] = "AITalk";
            }
            talkBox = new AITalkBox(this);
            MW.TalkAPI.Add(talkBox);
        }

        public override void Setting()
        {
            talkBox?.Setting();
        }

        public override void EndGame()
        {
            talkBox?.Dispose();
        }
    }
}
