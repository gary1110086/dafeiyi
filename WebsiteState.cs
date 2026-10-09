using System;
using System.Collections.Generic;
namespace LightTranslate {
 internal sealed class WebsiteState {
  internal string Code,Message;
  internal bool Ready { get { return Code=="ready"; } }
  internal WebsiteState(string code,string message) { Code=code; Message=message; }
  internal static WebsiteState From(Dictionary<string,object> data) {
   if(data==null) return new WebsiteState("loading","官网正在加载，请稍后检查连接。");
   Func<string,bool> yes=key=>data.ContainsKey(key)&&Convert.ToBoolean(data[key]);
   if(yes("login")) return new WebsiteState("login","需要登录或重新验证。完成后点击「检查连接」。");
   if(!yes("editor")) return new WebsiteState("loading","页面尚未就绪，请完成验证或刷新官网。");
   if(yes("occupied")) return new WebsiteState("draft","官网有未发送的文字，已为你保留；请先发送或清空。");
   if(!yes("send")) return new WebsiteState("layout","未找到发送控件，请刷新后重试；可导出不含个人内容的诊断。");
   return new WebsiteState("ready","官网页面已就绪。返回浮窗即可翻译、解释和追问。");
  }
 }
}
