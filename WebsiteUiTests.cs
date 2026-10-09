using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Wpf;
namespace LightTranslate {
 internal static class WebsiteUiTests {
  static void Assert(bool value,string message) { if(!value) throw new Exception(message); }
  internal static int Run(string folder,Application app) {
   Directory.CreateDirectory(folder); app.Dispatcher.BeginInvoke(new Action(async delegate {
    var log=new StringBuilder(); int passed=0,failed=0;
    var browser=new WebView2(); var view=new Window { Content=browser,Width=700,Height=500,ShowActivated=false,ShowInTaskbar=false };
    Func<string,Func<Task>,Task> check=async delegate(string name,Func<Task> action) { try { await action(); passed++; log.AppendLine("PASS "+name); } catch(Exception e) { failed++; log.AppendLine("FAIL "+name+": "+e.Message); } };
    try {
     view.Show(); await browser.EnsureCoreWebView2Async();
     Func<string,Task> page=async delegate(string body) { browser.NavigateToString("<html><body><style>textarea{width:440px;height:100px}button,[role=button]{display:inline-block;padding:12px}</style>"+body+"</body></html>"); for(int i=0;i<100;i++) { await Task.Delay(30); if(await browser.ExecuteScriptAsync("!!document.querySelector('#fixture')")=="true") return; } throw new Exception("fixture navigation timeout"); };
     await check("English semantic thinking/search controls configure without old CSS",async delegate {
      await page("<section id='fixture'><textarea placeholder='Message'></textarea><button aria-pressed='false' onclick=\"this.setAttribute('aria-pressed',this.getAttribute('aria-pressed')==='true'?'false':'true')\">Thinking</button><button aria-pressed='true' onclick=\"this.setAttribute('aria-pressed','false')\">Search</button><button aria-label='Send message'></button></section>");
      Assert(await browser.ExecuteScriptAsync(WebBridgeScript.Configure(true))=="true","modern thinking mode rejected");
      Assert(await browser.ExecuteScriptAsync("document.querySelectorAll('button')[0].getAttribute('aria-pressed')==='true'&&document.querySelectorAll('button')[1].getAttribute('aria-pressed')==='false'")=="true","requested thinking/search state not applied");
     });
     await check("ordinary website translation works without optional mode controls",async delegate {
      await page("<section id='fixture'><textarea placeholder='Message'></textarea><button aria-label='Send message'></button></section>");
      Assert(await browser.ExecuteScriptAsync(WebBridgeScript.Configure(false))=="true","missing optional thinking control blocks ordinary translation");
     });
     await check("contenteditable composer is recognized and filled without touching hidden editor",async delegate {
      await page("<section id='fixture'><textarea hidden></textarea><div contenteditable='true' role='textbox'></div><button aria-label='Send message'></button></section>");
      Assert(await browser.ExecuteScriptAsync(WebBridgeScript.Editor)=="true","visible contenteditable editor not detected");
      Assert(await browser.ExecuteScriptAsync(ServiceProfile.FillScript("a \"quoted\" question"))=="\"filled\"","fill failed");
      Assert(await browser.ExecuteScriptAsync("document.querySelector('[contenteditable]').innerText==='a \"quoted\" question'&&document.querySelector('textarea').value===''")=="true","wrong editor changed");
     });
     await check("semantic send selects composer button rather than unrelated page control",async delegate {
      await page("<button aria-label='Send message' onclick='window.wrong=true'>Outside composer</button><section id='fixture'><textarea>draft</textarea><button aria-label='Send message' onclick='window.sent=true'></button></section>");
      Assert(await browser.ExecuteScriptAsync(WebBridgeScript.Send)=="true","semantic send missing");
      Assert(await browser.ExecuteScriptAsync("window.sent===true&&window.wrong!==true")=="true","wrong send button clicked");
     });
     await check("disabled send never clicks and occupied draft stays intact",async delegate {
      await page("<section id='fixture'><textarea>my unsent message</textarea><button disabled aria-label='Send message' onclick='window.sent=true'></button></section>");
      Assert(await browser.ExecuteScriptAsync(WebBridgeScript.Send)=="false","disabled send accepted");
      Assert(await browser.ExecuteScriptAsync(ServiceProfile.FillScript("replacement"))=="\"occupied\"","personal draft overwritten");
     });
     await check("image attachment carries real PNG bytes and is protected as an unsent draft",async delegate {
      await page("<section id='fixture'><textarea></textarea><input type='file' accept='image/png'><button aria-label='Send message'></button></section>");
      var crop=ImageRequest.Crop(IterationUiTests.Frame(),new Rect(0,0,80,80),"chart","");
      Assert(await browser.ExecuteScriptAsync(WebBridgeScript.AttachImage(crop))=="\"attached\"","image not attached");
      Assert(await browser.ExecuteScriptAsync("document.querySelector('input').files[0].size==="+crop.Png.Length+"&&document.querySelector('input').files[0].type==='image/png'")=="true","attachment bytes or MIME lost");
      Assert((await browser.ExecuteScriptAsync(WebBridgeScript.Probe)).Contains("\"occupied\":true"),"personal image draft could be discarded");
      Assert(await browser.ExecuteScriptAsync(WebBridgeScript.AttachImage(crop))=="\"occupied\"","second upload replaced draft");
     });
     await check("cancel never clicks an unlabelled Send button or guesses missing thinking mode",async delegate {
      await page("<section id='fixture'><textarea></textarea><div class='ds-button ds-button--primary ds-button--circle' onclick='window.sent=true'></div></section>");
      Assert(await browser.ExecuteScriptAsync(WebBridgeScript.Configure(true))=="false","deep mode silently ignored");
      await browser.ExecuteScriptAsync("window.__dafeiyiCapture={armed:true,started:true,done:false,cancel:()=>window.cancelled=true}"); await browser.ExecuteScriptAsync(WebBridgeScript.Stop);
      Assert(await browser.ExecuteScriptAsync("window.cancelled===true&&window.sent!==true")=="true","cancel sent a new question");
     });
     await check("cancel aborts only the website HTTP request armed by this app",async delegate {
      await page("<section id='fixture'><textarea></textarea><button aria-label='Send message'></button></section>");
      await browser.ExecuteScriptAsync("window.fetch=async function(url,options){window.attachedSignal=!!options?.signal;return new Response(new ReadableStream({start(c){if(options?.signal)options.signal.addEventListener('abort',()=>{window.aborted=true;c.error(Error('aborted'));});}}));}");
      // Fixture origin differs; production match remains restricted to the DeepSeek origin.
      await browser.ExecuteScriptAsync(WebBridgeScript.Capture.Replace("location.origin!=='https://chat.deepseek.com'||","").Replace("u.origin===location.origin","u.origin==='https://chat.deepseek.com'"));
      await browser.ExecuteScriptAsync("window.__dafeiyiCapture.armed=true;fetch('https://chat.deepseek.com/unrelated')"); Assert(await browser.ExecuteScriptAsync("window.attachedSignal===false&&window.__dafeiyiCapture.armed===true")=="true","unrelated request intercepted");
      await browser.ExecuteScriptAsync("fetch('https://chat.deepseek.com/api/v0/chat/completion')"); await Task.Delay(50); await browser.ExecuteScriptAsync(WebBridgeScript.Stop); Assert(await browser.ExecuteScriptAsync("window.aborted===true&&window.__dafeiyiCapture.done===true")=="true","cancel only stopped display, not app's HTTP request");
     });
    } catch(Exception e) { failed++; log.AppendLine("FAIL setup: "+e.Message); }
    finally { view.Close(); browser.Dispose(); log.AppendLine(string.Format("RESULT {0} passed, {1} failed",passed,failed)); File.WriteAllText(Path.Combine(folder,"website-ui-test.txt"),log.ToString(),new UTF8Encoding(true)); app.Shutdown(failed==0?0:1); }
   })); return app.Run();
  }
 }
}
