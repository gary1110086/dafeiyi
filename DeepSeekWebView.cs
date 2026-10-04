using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace LightTranslate {
    // The website is a session host. Normal answers are displayed by PopupView.
    public sealed class DeepSeekWebView:Window {
        readonly WebView2 browser=new WebView2(); readonly TextBlock status;
        readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        Task initialization; bool closed,shutdown; string draft="",initializationError="";
        Task pendingCleanup=Task.FromResult(true);
        internal bool Ready { get { return browser.CoreWebView2!=null; } }
        internal string DraftText { get { return draft; } }
        internal WebView2 Browser { get { return browser; } }
        internal static string ProfilePath { get { return Path.Combine(Path.GetDirectoryName(Settings.DefaultPath),"DeepSeekWebProfile"); } }
        public DeepSeekWebView() {
            Title="大肥译 · 官网账号管理"; Width=900; Height=720; MinWidth=640; MinHeight=480; WindowStartupLocation=WindowStartupLocation.CenterScreen;
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.CanResize; Background=Ui.Brush("#101E32");
            var root=new Grid { Margin=new Thickness(12) }; root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            var title=new DockPanel { Background=Brushes.Transparent,Margin=new Thickness(2,0,2,12) };
            var back=Ui.Button("返回肥鱼浮窗",Hide,true); DockPanel.SetDock(back,Dock.Right); title.Children.Add(back);
            var brand=new StackPanel { Orientation=Orientation.Horizontal }; brand.Children.Add(WhaleTheme.Portrait(32)); var words=new StackPanel { Margin=new Thickness(10,0,0,0) }; words.Children.Add(Ui.Text("DeepSeek 官网账号",16,"#DEE8F4")); words.Children.Add(Ui.Text("在这里登录；翻译、解释和追问仍显示在大肥译浮窗",11,"#AEBFD3")); brand.Children.Add(words); title.Children.Add(brand);
            title.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) { if(!Ui.Interactive(e.OriginalSource as DependencyObject,title)) try { DragMove(); } catch(InvalidOperationException) { } }; Ui.Row(root,title,0);
            browser.DefaultBackgroundColor=System.Drawing.Color.FromArgb(255,16,30,50); Ui.Row(root,browser,1);
            status=Ui.Text("首次使用请登录官网，完成后点击「返回肥鱼浮窗」。",11,"#AEBFD3"); status.Margin=new Thickness(2,10,2,0); Ui.Row(root,status,2); Content=root;
            Loaded+=async delegate { await EnsureInitialized(); };
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e) { if(!shutdown) { e.Cancel=true; Hide(); } };
            Closed+=delegate { closed=true; browser.Dispose(); };
        }
        public void Prepare(string question) { draft=question??""; }
        internal void Shutdown() { shutdown=true; Close(); }
        Task EnsureInitialized() { if(initialization==null) initialization=Initialize(); return initialization; }
        async Task Initialize() {
            try {
                var environment=await CoreWebView2Environment.CreateAsync(null,ProfilePath);
                if(closed) return; await browser.EnsureCoreWebView2Async(environment); if(closed) return;
                browser.CoreWebView2.Settings.IsWebMessageEnabled=false;
                browser.CoreWebView2.Settings.AreHostObjectsAllowed=false;
                await browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(WebBridgeScript.Capture);
                browser.CoreWebView2.PermissionRequested+=delegate(object sender,CoreWebView2PermissionRequestedEventArgs e) { e.State=CoreWebView2PermissionState.Deny; };
                browser.CoreWebView2.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e) { e.Handled=true; Uri uri; if(Uri.TryCreate(e.Uri,UriKind.Absolute,out uri)&&uri.Scheme=="https") Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute=true }); };
                browser.CoreWebView2.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e) { Uri uri; if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out uri)||uri.Scheme!="https") e.Cancel=true; };
                browser.CoreWebView2.NavigationCompleted+=delegate(object sender,CoreWebView2NavigationCompletedEventArgs e) {
                    if(!closed) status.Text=e.IsSuccess?"登录或验证完成后，返回肥鱼浮窗，再点击翻译 / 解释。":"官网加载失败（"+e.WebErrorStatus+"），请检查网络后重试。";
                };
                browser.Source=new Uri("https://chat.deepseek.com/");
            } catch(Exception) { initializationError="官网连接未能启动，请检查 Edge WebView2 Runtime。"; if(!closed) status.Text=initializationError; }
        }
        internal async Task<string> FillDraft() {
            if(closed||!Ready||!ServiceProfile.IsChat(browser.Source==null?"":browser.Source.AbsoluteUri)) return "unavailable";
            if(string.IsNullOrWhiteSpace(draft)) return "empty";
            return await browser.ExecuteScriptAsync(ServiceProfile.FillScript(draft));
        }
        internal async Task StreamAsync(Settings settings,string input,string mode,List<Dictionary<string,string>> conversation,Action<string> chunk,CancellationToken cancellation) {
            await gate.WaitAsync(cancellation);
            bool ownsRequest=false,filled=false; string id="",prompt="";
            try {
                await pendingCleanup; cancellation.ThrowIfCancellationRequested();
                if(closed) throw new InvalidOperationException("官网连接已关闭，请重试。");
                if(!Ready&&!IsVisible) { ShowActivated=false; ShowInTaskbar=false; Opacity=0; Show(); Hide(); Opacity=1; ShowActivated=true; ShowInTaskbar=true; }
                await EnsureInitialized(); cancellation.ThrowIfCancellationRequested();
                if(initializationError.Length>0) throw new InvalidOperationException(initializationError);
                if(IsVisible) throw new InvalidOperationException("请先完成官网登录，点击「返回肥鱼浮窗」，再重试。");
                if(ServiceProfile.IsChat(browser.Source==null?"":browser.Source.AbsoluteUri)&&await browser.ExecuteScriptAsync("!!document.querySelector('textarea')?.value.trim()") == "true") {
                    ShowAccount(); throw new InvalidOperationException("官网输入框中还有未发送的文字，已为你保留。请先处理后再试。");
                }
                bool navigated=false; EventHandler<CoreWebView2NavigationCompletedEventArgs> completed=delegate(object sender,CoreWebView2NavigationCompletedEventArgs e) { if(e.IsSuccess) navigated=true; };
                browser.CoreWebView2.NavigationCompleted+=completed;
                try {
                    browser.CoreWebView2.Navigate("https://chat.deepseek.com/");
                    for(int i=0;i<160&&!navigated;i++) await Task.Delay(125,cancellation);
                } finally { browser.CoreWebView2.NavigationCompleted-=completed; }
                if(!navigated) throw new InvalidOperationException("官网加载超时，请检查网络后重试。");
                bool editor=false;
                for(int i=0;i<160;i++) {
                    cancellation.ThrowIfCancellationRequested(); if(closed) throw new OperationCanceledException();
                    await Task.Delay(125,cancellation);
                    if(browser.Source!=null&&ServiceProfile.IsChat(browser.Source.AbsoluteUri)) {
                        if(browser.Source.AbsolutePath.Contains("sign_in")) { ShowAccount(); throw new InvalidOperationException("需要登录 DeepSeek 官网。登录后返回浮窗并重试。"); }
                        if(await browser.ExecuteScriptAsync(WebBridgeScript.Editor)=="true"&&await browser.ExecuteScriptAsync("!!window.__dafeiyiCapture")=="true") { editor=true; break; }
                    }
                }
                if(!editor) { ShowAccount(); throw new InvalidOperationException("官网未就绪，请在账号窗口完成登录或验证后重试。"); }
                if(await browser.ExecuteScriptAsync(WebBridgeScript.Configure(settings.Thinking))!="true") throw new InvalidOperationException("官网模式控件发生变化，请打开连接设置检查页面。");
                prompt=ServiceProfile.WebPrompt(settings,input,mode,conversation); Prepare(prompt);
                if(await FillDraft()!="\"filled\"") throw new InvalidOperationException("官网输入框暂时不可用，原有文字已保留，请稍后重试。");
                filled=true;
                await Task.Delay(150,cancellation);
                id=Guid.NewGuid().ToString("N");
                await browser.ExecuteScriptAsync("window.__dafeiyiCapture={id:'"+id+"',armed:true,started:false,body:'',done:false,status:0};"); ownsRequest=true;
                if(await browser.ExecuteScriptAsync(WebBridgeScript.Send)!="true") throw new InvalidOperationException("官网发送按钮暂时不可用，请稍后重试。");
                var decoder=new WebAnswerStream(); int read=0; string emitted=""; var watch=Stopwatch.StartNew();
                var json=new JavaScriptSerializer { MaxJsonLength=2*1024*1024 };
                while(watch.Elapsed.TotalSeconds<(settings.Thinking?180:90)) {
                    await Task.Delay(120,cancellation); if(closed) throw new OperationCanceledException();
                    if(!ServiceProfile.IsChat(browser.Source==null?"":browser.Source.AbsoluteUri)) throw new InvalidOperationException("官网连接已跳转，请重新登录后重试。");
                    var state=json.DeserializeObject(await browser.ExecuteScriptAsync(WebBridgeScript.State)) as Dictionary<string,object>;
                    if(state==null||!state.ContainsKey("id")||Convert.ToString(state["id"])!=id) throw new InvalidOperationException("官网页面已切换，请重试；未使用旧对话回答。");
                    string body=Convert.ToString(state["body"]); if(body.Length<read) throw new InvalidOperationException("官网响应已重置，请重试。");
                    if(body.Length>read) { decoder.Feed(body.Substring(read)); read=body.Length; }
                    string answer=decoder.Answer;
                    if(answer.Length>200000) throw new InvalidOperationException("回答太长，请分段处理。");
                    if(!answer.StartsWith(emitted,StringComparison.Ordinal)) throw new InvalidOperationException("官网修改了已返回的回答，请重试以取得完整内容。");
                    if(answer.Length>emitted.Length) { chunk(answer.Substring(emitted.Length)); emitted=answer; }
                    if(decoder.Error.Length>0) throw new InvalidOperationException(decoder.Error);
                    if(Convert.ToBoolean(state["done"])) {
                        if(Convert.ToString(state["error"]).Length>0||Convert.ToInt32(state["status"])!=200) { ShowAccount(); throw new InvalidOperationException("官网连接中断或请求被拒绝，请在账号窗口查看验证、网络或额度提示。"); }
                        if(!decoder.Finished||string.IsNullOrWhiteSpace(emitted)) throw new InvalidOperationException("官网未返回完整回答，请重试；未保存为完成结果。");
                        ownsRequest=false; return;
                    }
                    if(!Convert.ToBoolean(state["started"])&&watch.Elapsed.TotalSeconds>12) { ShowAccount(); throw new InvalidOperationException("官网未开始生成，请在账号窗口完成验证后重试。"); }
                }
                throw new TimeoutException("官网回答等待超时，请缩短内容或关闭深度思考后重试。");
            } finally {
                if(ownsRequest||filled) pendingCleanup=CleanupAsync(id,prompt,ownsRequest);
                gate.Release();
            }
        }
        async Task CleanupAsync(string id,string prompt,bool stop) {
            if(closed||!Ready||!ServiceProfile.IsChat(browser.Source==null?"":browser.Source.AbsoluteUri)) return;
            try {
                if(stop&&id.Length>0) await browser.ExecuteScriptAsync("if(window.__dafeiyiCapture?.id==='"+id+"'){"+WebBridgeScript.Stop+"}");
                await browser.ExecuteScriptAsync("(()=>{const e=document.querySelector('textarea');if(e&&e.value==="+new JavaScriptSerializer().Serialize(prompt)+"){Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set.call(e,'');e.dispatchEvent(new Event('input',{bubbles:true}));}})()");
            } catch(Exception) { }
        }
        void ShowAccount() { if(!IsVisible) Show(); WindowState=WindowState.Normal; Activate(); }
    }
}
