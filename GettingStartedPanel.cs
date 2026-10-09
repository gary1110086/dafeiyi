using System;
using System.Windows;
using System.Windows.Controls;
namespace LightTranslate {
 internal sealed class GettingStartedPanel:StackPanel {
  static string L(string chinese,string english) { return ProductLanguage.Interface=="en"?english:chinese; }
  internal GettingStartedPanel(Action demo,Action<string> navigate) {
   Card("01",L("先试一次，不用登录","Try it first, without signing in"),L("内置示例只在本机展示译文。选中一句英文，体验选区与翻译按钮。","The built-in example runs locally. Select an English sentence to try the workflow."),L("体验划词示例","Try selection demo"),demo);
   Card("02",L("连接你的 DeepSeek","Connect your DeepSeek account"),L("官网账号：在应用打开的窗口登录，检查连接后返回。API：填写自己的 Key。两种方式都在同一个肥鱼浮窗显示回答。","Website: sign in inside the app, check the connection, then return. API: enter your own key. Both show answers in the same whale popup."),L("设置连接方式","Set up connection"),delegate { navigate("连接与模型"); });
   Card("03",L("选一种舒服的使用方式","Choose when translation happens"),L("点击：选中后点小按钮。自动：选中并停顿。剪贴板：复制新文字。仅陪伴：关闭自动取词。单击肥鱼随时切换；按住可以拖动。","Button: select and click. Automatic: select and pause. Clipboard: copy new text. Companion: turn off automatic capture. Click the whale to switch; hold and drag to move."),L("调整划词与快捷键","Adjust selection & shortcuts"),delegate { navigate("划词与快捷键"); });
   var questions=new StackPanel();
   Faq(questions,L("划词没反应？","No selection button?"),L("先确认不是「仅陪伴」或暂停状态。部分 PDF、图片和软件不提供文字选区：复制后按 Ctrl+Alt+V，或按 Ctrl+Alt+S 框选屏幕。公式与图表选「框图 / 公式」，确认裁剪后再发送。","Check that capture is not paused and Companion mode is off. Some PDFs, images and apps do not expose selections. Copy and press Ctrl+Alt+V, or capture with Ctrl+Alt+S. For equations and charts, choose Image / formula and confirm the crop before sending."));
   Faq(questions,L("浏览器登录了，为什么不能翻译？","Signed in in Chrome, but cannot translate?"),L("Chrome 与应用内官网窗口的登录是独立的。请在「连接与模型」打开应用内登录，完成验证，检查页面就绪后返回。关闭账号窗口会保留登录。失败时保留原文，点「重试这次请求」；不会自动切换收费 API。","Chrome and the app have separate website sessions. Sign in inside Connection & models, complete verification, check readiness and return. Closing the account window preserves login. Failed requests keep your source; use Retry this request. There is no automatic fallback to paid API."));
   Faq(questions,L("怎么让浮窗消失、留下或变小？","Hide, pin or resize the popup?"),L("未固定的浮窗点击其他软件会收起，Esc 也可关闭；关闭生成中的浮窗会取消请求。点图钉可保持显示，拖动窗口边缘可缩放，Aa 调整字号和行距。","An unpinned popup hides when you click another app; Esc hides it too. Closing while generating cancels the request. Pin it to keep it visible, drag its edges to resize, or use Aa for text size and spacing."));
   Faq(questions,L("复制什么就会翻译什么吗？","Does every copy get translated?"),L("剪贴板模式会把新复制的文字交给当前连接。复制密码或私人资料前，切换「点击」或「仅陪伴」。普通点击模式只有点击翻译按钮才发送文字；框图只在确认后发送裁剪区域。","Clipboard mode sends newly copied text to your selected service. Switch to Button or Companion before copying passwords or private material. Button mode sends only after a click; image capture sends the crop only after confirmation."));
   var faq=new Expander { Header=L("遇到问题？这里先看一眼","Quick answers"),Content=questions,IsExpanded=false,Margin=new Thickness(0,8,0,16) }; Children.Add(faq);
   var update=Ui.Button(L("前往检查更新","Check updates"),delegate { navigate("更新与关于"); },false); update.HorizontalAlignment=HorizontalAlignment.Left; Children.Add(update);
   var note=Ui.Text(L("更新保留本机登录、配置、最近 30 条历史、术语和背景。需要反馈时提供版本与错误提示；不要提交 API Key、Cookie 或私人内容。","Updates preserve login, settings, 30 recent results, terms and backgrounds. For feedback, share the version and error message; omit API keys, cookies and private content."),11,"#AEBFD3"); note.Margin=new Thickness(0,10,0,0); Children.Add(note);
  }
  void Card(string number,string title,string detail,string label,Action action) {
   var row=new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(38) }); row.ColumnDefinitions.Add(new ColumnDefinition());
   var badge=Ui.Text(number,14,"#8CD3DF"); badge.Margin=new Thickness(0,2,0,0); row.Children.Add(badge); var text=new StackPanel(); Grid.SetColumn(text,1); row.Children.Add(text);
   var heading=Ui.Text(title,15,"#DEE8F4"); heading.FontWeight=FontWeights.SemiBold; text.Children.Add(heading); var description=Ui.Text(detail,12,"#AEBFD3"); description.Margin=new Thickness(0,7,0,10); text.Children.Add(description);
   var button=Ui.Button(label,action,false); button.HorizontalAlignment=HorizontalAlignment.Left; text.Children.Add(button);
   Children.Add(new Border { Background=Ui.Brush("#101E32"),BorderBrush=Ui.Brush("#2A4058"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(14),Margin=new Thickness(0,0,0,10),Child=row });
  }
  static void Faq(StackPanel parent,string title,string answer) {
   var text=Ui.Text(answer,12,"#AEBFD3"); text.Margin=new Thickness(8,8,8,12); parent.Children.Add(new Expander { Header=title,Content=text,Margin=new Thickness(0,7,0,0) });
  }
 }
}
