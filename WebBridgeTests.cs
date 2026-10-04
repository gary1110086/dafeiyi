using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Web.Script.Serialization;
namespace LightTranslate {
    internal static class WebBridgeTests {
        static void Assert(bool value,string text) { if(!value) throw new Exception(text); }
        static string Frame(object value) { return "data: "+new JavaScriptSerializer().Serialize(value)+"\n\n"; }
        internal static int Run(string folder,Application app) {
            Directory.CreateDirectory(folder); app.Dispatcher.BeginInvoke(new Action(async delegate {
                var log=new StringBuilder(); int passed=0,failed=0;
                Func<string,Func<Task>,Task> check=async delegate(string name,Func<Task> action) { try { await action(); passed++; log.AppendLine("PASS "+name); } catch(Exception e) { failed++; log.AppendLine("FAIL "+name+": "+e.Message); } };
                await check("website SSE reconstructs fragmented Markdown and math and excludes reasoning",async delegate {
                    var stream=new WebAnswerStream(); string initial=Frame(new { v=new { response=new { status="WIP",fragments=new[]{new { type="THINK",content="private reasoning" },new { type="RESPONSE",content="## 译文\n\n" }} } } });
                    string body=initial+Frame(new { p="response/fragments/-1/content",o="APPEND",v="$E=" })+Frame(new { v="mc^2$\n\n|词|译法|\n|---|---|\n|lattice|晶格|" })+Frame(new { p="response",o="BATCH",v=new[]{new { p="status",v="FINISHED" }} });
                    foreach(char c in body) stream.Feed(c.ToString()); Assert(stream.Finished&&stream.Answer=="## 译文\n\n$E=mc^2$\n\n|词|译法|\n|---|---|\n|lattice|晶格|","raw content changed"); await Task.Delay(1);
                    var windows=new WebAnswerStream(); foreach(char c in body.Replace("\n","\r\n")) windows.Feed(c.ToString()); Assert(windows.Finished&&windows.Answer==stream.Answer,"CRLF boundaries lost data");
                });
                await check("website fragment transitions and idle never fabricate completion",async delegate {
                    var stream=new WebAnswerStream(); stream.Feed(Frame(new { v=new { response=new { status="WIP",fragments=new[]{new { type="THINK",content="thinking" }} } } }));
                    stream.Feed(Frame(new { p="response/fragments",o="APPEND",v=new[]{new { type="RESPONSE",content="答案" }} })); stream.Feed(Frame(new { p="response/fragments/-1/content",o="APPEND",v=" 🐟" })); Assert(!stream.Finished&&stream.Answer=="答案 🐟","thinking leaked or premature completion"); stream.Feed("event: error\ndata: {\"message\":\"error\"}\n\n"); Assert(stream.Error.Length>0&&!stream.Finished,"error became success"); await Task.Delay(1);
                });
                await check("website and API cache are isolated while website ignores API credentials",async delegate {
                    var s=new Settings { ApiKey="fixture" }; var cache=new SessionCache(); cache.Store(s,"term","translate","API"); s.Service="web"; CachedResult result; Assert(!cache.TryGet(s,"term","translate",out result),"web reused API result"); cache.Store(s,"term","translate","web"); s.ApiKey="another"; s.Model="another"; s.BaseUrl="https://another.test"; Assert(cache.TryGet(s,"term","translate",out result)&&result.Answer=="web","web depends on API configuration"); s.Thinking=true; Assert(!cache.TryGet(s,"term","translate",out result),"thinking reused fast answer"); await Task.Delay(1);
                });
                await check("canceled website answer never enters history or overwrites replacement",async delegate {
                    var s=new Settings { Service="web",ResidentOrb=false }; var c=new AppController(s,true); var began=new TaskCompletionSource<bool>();
                    c.WebStreamer=async delegate(Settings settings,string input,string mode,List<Dictionary<string,string>> conversation,Action<string> chunk,CancellationToken token) { if(input=="old") { chunk("old partial"); began.SetResult(true); await Task.Delay(2000,token); chunk("stale"); } else chunk("replacement"); };
                    try { await c.AcceptSelection("old",new Point(500,300)); var old=c.RequestAsync("translate",null); await began.Task; await c.AcceptSelection("new",new Point(500,300)); await c.RequestAsync("translate",null); await old; Assert(c.RecentResults.Count==1&&c.RecentResults[0].Source=="new"&&c.RecentResults[0].Answer=="replacement","canceled result persisted"); } finally { c.Dispose(); }
                });
                log.AppendLine(string.Format("RESULT {0} passed, {1} failed",passed,failed)); File.WriteAllText(Path.Combine(folder,"web-bridge-test.txt"),log.ToString(),new UTF8Encoding(true)); app.Shutdown(failed==0?0:1);
            })); return app.Run();
        }
        internal static int Live(string folder,Application app) {
            Directory.CreateDirectory(folder); app.Dispatcher.BeginInvoke(new Action(async delegate {
                var log=new StringBuilder(); var view=new DeepSeekWebView(); AppController controller=null; int exit=1;
                try {
                    var s=Settings.Load(Settings.DefaultPath).Copy(); s.Service="web"; s.ApiKey=""; s.Thinking=false; s.ResidentOrb=false; s.Mode="button";
                    s.TranslatePrompt="请用 Markdown 回答，只给出一个二级标题「连接成功」，然后写出公式 $E=mc^2$，再给出一张两列表格，内容为 mass 和 质量。保留公式的 LaTeX 源码。";
                    controller=new AppController(s,true); int chunks=0;
                    controller.WebStreamer=async delegate(Settings profile,string input,string mode,List<Dictionary<string,string>> conversation,Action<string> chunk,CancellationToken token) { await view.StreamAsync(profile,input,mode,conversation,delegate(string text) { chunks++; chunk(text); },token); };
                    await controller.AcceptSelection("mass-energy equivalence",new Point(550,250)); await controller.RequestAsync("translate",null);
                    Assert(controller.RecentResults.Count==1,"native answer not completed: "+controller.Popup.AnswerText); string answer=controller.RecentResults[0].Answer;
                    Assert(answer.Contains("E=mc^2")&&answer.Contains("##")&&answer.Contains("|"),"Markdown/math not preserved: "+answer);
                    Assert(controller.Popup.IsVisible&&!view.IsVisible&&chunks>0,"website replaced or hid native popup");
                    UiTests.Capture(controller.Popup,Path.Combine(folder,"官网回答-肥鱼浮窗.png")); log.AppendLine("PASS actual logged-in website response streamed into native popup with raw Markdown, table and LaTeX; no API key.");
                    await controller.RequestAsync("followup","刚才公式中 c 是什么？请用一句话回答，再写出 $F=ma$。");
                    Assert(controller.Popup.AnswerText.Contains("光速")&&controller.Popup.AnswerText.Contains("F=ma")&&!view.IsVisible,"follow-up lost context: "+controller.Popup.AnswerText);
                    UiTests.Capture(controller.Popup,Path.Combine(folder,"官网追问-肥鱼浮窗.png")); log.AppendLine("PASS actual website follow-up retained previous conversation and returned to same native popup.");
                    s.ExplainPrompt="请用一句中文说明所选概念，然后写出公式 $F=ma$，不要输出思考过程。"; controller.ChangeService("web",s.Model,true);
                    await controller.AcceptSelection("Newton's second law",new Point(550,250)); await controller.RequestAsync("explain",null);
                    Assert(controller.RecentResults.Count==2&&controller.RecentResults[0].Answer.Contains("F=ma")&&!view.IsVisible,"thinking answer not returned: "+controller.Popup.AnswerText);
                    log.AppendLine("PASS actual website deep thinking mode returned final Markdown/math answer to native popup."); s.Thinking=false;
                    using(var cancel=new CancellationTokenSource()) {
                        bool interrupted=false; try { await view.StreamAsync(s,"列出 100 条不同的物理知识，每条 30 字以上。","explain",null,delegate { cancel.Cancel(); },cancel.Token); } catch(OperationCanceledException) { interrupted=true; }
                        Assert(interrupted,"website stream did not cancel");
                    }
                    var recovered=new StringBuilder(); s.TranslatePrompt="仅输出中文「恢复成功」，不写其他内容。"; await view.StreamAsync(s,"recovered","translate",null,delegate(string text) { recovered.Append(text); },CancellationToken.None); Assert(recovered.ToString().Contains("恢复成功"),"new request failed after cancellation");
                    log.AppendLine("PASS actual website generation canceled and subsequent request completed without stale answer."); exit=0;
                } catch(Exception e) { log.AppendLine("FAIL "+e.Message); }
                finally { if(controller!=null) controller.Dispose(); view.Shutdown(); log.AppendLine(exit==0?"RESULT 4 passed, 0 failed":"RESULT live connection failed"); File.WriteAllText(Path.Combine(folder,"web-connect-test.txt"),log.ToString(),new UTF8Encoding(true)); app.Shutdown(exit); }
            })); return app.Run();
        }
    }
}
