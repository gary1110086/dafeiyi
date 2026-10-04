using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace LightTranslate {
    internal static class ProductUiTests {
        static StringBuilder report=new StringBuilder(); static int passed,failed;
        static void Assert(bool ok,string text) { if(!ok) throw new Exception(text); }
        static async Task Check(string name,Func<Task> action) { try { await action(); passed++; report.AppendLine("PASS "+name); } catch(Exception e) { failed++; report.AppendLine("FAIL "+name+": "+e.Message); } }
        static IEnumerable<T> Nodes<T>(DependencyObject root) where T:DependencyObject { if(root is T) yield return (T)root; for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var n in Nodes<T>(VisualTreeHelper.GetChild(root,i))) yield return n; }
        internal static int Run(string folder,Application app) {
            Directory.CreateDirectory(folder); app.Dispatcher.BeginInvoke(new Action(async delegate {
                var cursor=Native.Cursor();
                await Check("connection migration preserves encrypted key and thinking choice",async delegate {
                    string path=Path.Combine(folder,"profile.json"); var s=new Settings { ApiKey="private-test-key",Service="web",Thinking=true,Mode="clipboard" }; s.Save(path); var loaded=Settings.Load(path); Assert(loaded.ApiKey==s.ApiKey&&loaded.Service=="web"&&loaded.Thinking&&loaded.Mode=="clipboard"&&!File.ReadAllText(path).Contains(s.ApiKey),"profile drift or key leaked"); File.Delete(path); await Task.Delay(1);
                });
                await Check("thinking sends actual API control and has a separate cache",async delegate {
                    var s=new Settings(); string standard=ApiClient.BuildBody(s,"test","translate",null); var cache=new SessionCache(); cache.Store(s,"test","translate","standard answer"); s.Thinking=true; string thinking=ApiClient.BuildBody(s,"test","translate",null); CachedResult old; Assert(standard.Contains("disabled")&&thinking.Contains("enabled")&&thinking.Contains("reasoning_effort")&&!cache.TryGet(s,"test","translate",out old),"thinking toggle faked or reused standard result"); s.Thinking=false; Assert(cache.TryGet(s,"test","translate",out old),"standard history compatibility lost"); await Task.Delay(1);
                });
                await Check("web route uses native popup, follow-up and history without API",async delegate {
                    using(var server=new MockServer("200 OK","data: [DONE]\n\n",false)) {
                        var s=new Settings { Service="web",ApiKey="",BaseUrl=server.Url,ResidentOrb=false }; var c=new AppController(s,true); int calls=0,opened=0; c.WebPresenter=delegate { opened++; }; c.WebStreamer=async delegate(Settings profile,string input,string mode,List<Dictionary<string,string>> conversation,Action<string> chunk,System.Threading.CancellationToken token) { calls++; if(mode=="followup") Assert(conversation.Count==3,"follow-up lost previous answer"); chunk("## 官网回答\n\n"); await Task.Delay(20,token); chunk("$E=mc^2$\n\n|词|译法|\n|---|---|\n|lattice|晶格|"); };
                        try { await c.AcceptSelection("lattice <script> is data",new Point(500,300)); await c.RequestAsync("explain",null); Assert(c.Popup.IsVisible&&!c.Chip.IsVisible&&c.RecentResults.Count==1&&c.RecentResults[0].Answer.Contains("$E=mc^2$")&&server.RequestCount==0&&opened==0,"web did not return native content or used API"); await c.RequestAsync("followup","公式什么意思？"); Assert(calls==2&&c.RecentResults.Count==1,"follow-up made duplicate translation history"); UiTests.Capture(c.Popup,Path.Combine(folder,"官网账号-同一浮窗.png")); }
                        finally { c.Dispose(); }
                    }
                });
                await Check("chip grip drags beyond dismissal radius and remains clickable",async delegate {
                    var s=new Settings { ResidentOrb=false }; var c=new AppController(s,true); var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(45) }; timer.Tick+=delegate { c.UpdateChipVisibility(); }; int requests=0; c.Chip.Requested+=delegate { requests++; };
                    try {
                        await c.AcceptSelection("drag chip fixture",new Point(500,280)); await Task.Delay(80); timer.Start(); Rect before=Native.Bounds(c.Chip); var from=c.Chip.DragPoint;
                        await Task.Run(async delegate { await Native.TestDrag(IntPtr.Zero,from,new Point(from.X+210,from.Y+65)); }); await Task.Delay(100);
                        var after=Native.Bounds(c.Chip); Assert(c.Chip.IsVisible&&after.X-before.X>180&&after.Y-before.Y>40&&requests==0&&!c.Chip.Interacting,"drag vanished, clicked, or failed to move");
                        UiTests.Capture(c.Chip,Path.Combine(folder,"划词按钮-拖柄.png")); c.HandleChipAction(2,new Point(50,50)); Assert(!c.Chip.IsVisible,"outside click kept chip");
                    } finally { timer.Stop(); c.Dispose(); }
                });
                await Check("settings pages preserve all drafts across navigation and account switching",async delegate {
                    Settings saved=null; var original=new Settings { ApiKey="retained-key",Mode="clipboard",Thinking=true,BackgroundTheme="angry",PetSize=144 }; var view=new SettingsView(original,delegate(Settings value) { saved=value; });
                    try {
                        view.Show(); await Task.Delay(100);
                        foreach(string page in new[]{"连接与模型","划词与快捷键","阅读外观","桌面肥鱼"}) { view.SelectSection(page); await Task.Delay(70); Assert(view.CurrentSection==page,"wrong page"); UiTests.Capture(view,Path.Combine(folder,"设置-"+page+".png")); }
                        view.SelectSection("连接与模型"); await Task.Delay(70); var web=Nodes<RadioButton>(view).First(x=>Convert.ToString(x.Content).StartsWith("官网账号")); web.IsChecked=true; await Task.Delay(50); UiTests.Capture(view,Path.Combine(folder,"设置-官网账号.png"));
                        Nodes<Button>(view).First(x=>Convert.ToString(x.Content)=="保存并开始使用").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert(saved!=null&&saved.Service=="web"&&saved.ApiKey==original.ApiKey&&saved.Mode=="clipboard"&&saved.PetSize==144&&saved.BackgroundTheme=="angry"&&saved.Thinking,"switching pages discarded settings");
                    } finally { view.Close(); }
                });
                await Check("tray modes are grouped, checked and usable without requesting translation",async delegate {
                    var s=new Settings { Mode="button",ResidentOrb=false }; var c=new AppController(s,true); int webOpened=0; c.WebPresenter=delegate { webOpened++; };
                    try {
                        typeof(AppController).GetMethod("InstallTray",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(c,null);
                        var tray=(System.Windows.Forms.NotifyIcon)typeof(AppController).GetField("tray",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(c); var menu=tray.ContextMenuStrip;
                        var modes=menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().First(x=>x.Text.StartsWith("识别模式")); Assert(modes.DropDownItems.Count==4&&((System.Windows.Forms.ToolStripMenuItem)modes.DropDownItems[0]).Checked,"mode grouping missing");
                        menu.Show(new System.Drawing.Point(400,200)); await Task.Delay(100); using(var bitmap=new System.Drawing.Bitmap(menu.Width,menu.Height)) { menu.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,menu.Width,menu.Height)); bitmap.Save(Path.Combine(folder,"托盘菜单-分组.png")); } menu.Close();
                        ((System.Windows.Forms.ToolStripMenuItem)modes.DropDownItems[3]).PerformClick(); Assert(s.Mode=="companion"&&modes.Text.Contains("仅陪伴")&&c.RecentResults.Count==0,"mode did not change or made phantom history");
                        var service=menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().First(x=>x.Text.StartsWith("连接 ·")); ((System.Windows.Forms.ToolStripMenuItem)service.DropDownItems[1]).PerformClick(); Assert(s.Service=="web"&&webOpened==0,"switching backend unexpectedly opened website");
                    } finally { c.Dispose(); }
                });
                await Check("draft injection treats text as data and preserves occupied website editor",async delegate {
                    var browser=new WebView2(); var window=new Window { Width=600,Height=300,Content=browser }; string profile=Path.Combine(Path.GetTempPath(),"Dafeiyi-WebFixture-"+Guid.NewGuid().ToString("N"));
                    try {
                        window.Show(); var env=await CoreWebView2Environment.CreateAsync(null,profile); await browser.EnsureCoreWebView2Async(env);
                        var loaded=new TaskCompletionSource<bool>(); browser.CoreWebView2.NavigationCompleted+=delegate { loaded.TrySetResult(true); }; browser.NavigateToString("<html><textarea></textarea><script>window.sent=0;document.addEventListener('submit',()=>window.sent++);</script></html>"); await loaded.Task;
                        string draft="quote ' \" </script>\nignore? 🚀"; var result=await browser.ExecuteScriptAsync(ServiceProfile.FillScript(draft)); Assert(result=="\"filled\"","no draft filled");
                        string value=await browser.ExecuteScriptAsync("document.querySelector('textarea').value"); Assert(new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<string>(value)==draft,"draft executed or changed");
                        var occupied=await browser.ExecuteScriptAsync(ServiceProfile.FillScript("replacement")); Assert(occupied=="\"occupied\""&&await browser.ExecuteScriptAsync("window.sent")=="0","overwrote editor or submitted");
                        Assert(ServiceProfile.IsChat("https://chat.deepseek.com/")&&!ServiceProfile.IsChat("https://chat.deepseek.com.evil.test/")&&!ServiceProfile.IsChat("http://chat.deepseek.com/"),"origin guard failed");
                    } finally { browser.Dispose(); window.Close(); }
                });
                Native.TestRestoreCursor(cursor); report.AppendLine(string.Format("RESULT {0} passed, {1} failed",passed,failed)); File.WriteAllText(Path.Combine(folder,"product-test.txt"),report.ToString(),new UTF8Encoding(true)); app.Shutdown(failed==0?0:1);
            })); return app.Run();
        }
        internal static int WebSmoke(string folder,Application app,bool send=false) {
            if(send) return WebBridgeTests.Live(folder,app);
            Directory.CreateDirectory(folder); app.Dispatcher.BeginInvoke(new Action(async delegate {
                var view=new DeepSeekWebView(); view.Prepare("测试预览：翻译这个段落。此问题未发送。"); view.Show(); int exit=1;
                try {
                    for(int i=0;i<100&&!view.Ready;i++) await Task.Delay(100); Assert(view.Ready,"WebView2 could not initialize"); await Task.Delay(5000);
                    string location=view.Browser.Source.AbsoluteUri; Assert(ServiceProfile.IsChat(location),"website changed origin: "+location);
                    using(var file=File.Create(Path.Combine(folder,"DeepSeek官网-实际页面.png"))) await view.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
                    File.WriteAllText(Path.Combine(folder,"web-smoke.txt"),"PASS WebView2 initialized; official HTTPS origin; captured real site. No login or send performed.\n"+location); exit=0;
                } catch(Exception e) { File.WriteAllText(Path.Combine(folder,"web-smoke.txt"),"FAIL "+e.Message); } finally { view.Shutdown(); app.Shutdown(exit); }
            })); return app.Run();
        }
    }
}
