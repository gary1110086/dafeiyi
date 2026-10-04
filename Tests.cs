using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Web.Script.Serialization;

namespace LightTranslate {
    internal static class Tests {
        static int passed, failed;
        static StringBuilder report = new StringBuilder();
        static void Check(string name, Action action) {
            try { action(); passed++; report.AppendLine("PASS " + name); }
            catch(Exception e) { failed++; report.AppendLine("FAIL " + name + ": " + e.Message); }
        }
        static void Assert(bool value, string reason) { if(!value) throw new Exception(reason); }
        public static int Run(string path) { ReadingChecks.Run(Check); CompanionChecks.Run(Check);
            Check("all whale action atlases are packaged with original frame order and attribution",delegate {
                string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","Whale");
                Assert(File.Exists(Path.Combine(folder,"manifest.json")),"whale assets not packaged");
                var manifest=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(folder,"manifest.json")));
                var clips=(Dictionary<string,object>)manifest["clips"]; Assert(clips.Count==16,"lost original modes"); int frames=0;
                foreach(var value in clips.Values) { var clip=(Dictionary<string,object>)value; foreach(var item in (System.Collections.IEnumerable)clip["frames"]) { var frame=(Dictionary<string,object>)item; Assert(File.Exists(Path.Combine(folder,Convert.ToString(frame["sheet"]))),"atlas missing"); frames++; } }
                Assert(frames==2600,"source frames dropped"); Assert(File.Exists(Path.Combine(folder,"dsh-pet-LICENSE.txt"))&&File.Exists(Path.Combine(folder,"dsh-dafeiyu-LICENSE.txt")),"attribution missing");
            });
            Check("whale animation overlays return to latest real work state",delegate {
                var type=typeof(Settings).Assembly.GetType("LightTranslate.PetAnimation"); Assert(type!=null,"animation model missing"); var model=Activator.CreateInstance(type);
                type.GetMethod("SetBase").Invoke(model,new object[]{"working"}); type.GetMethod("Play").Invoke(model,new object[]{"head_pat",0});
                type.GetMethod("SetBase").Invoke(model,new object[]{"error"}); Assert((string)type.GetProperty("ActiveClip").GetValue(model,null)=="head_pat","new state interrupted touch");
                type.GetMethod("Tick").Invoke(model,new object[]{42*96}); Assert((string)type.GetProperty("ActiveClip").GetValue(model,null)=="error","touch returned to obsolete work state");
                type.GetMethod("Play").Invoke(model,new object[]{"dragging_dizzy",840}); type.GetMethod("Tick").Invoke(model,new object[]{840}); Assert((string)type.GetProperty("ActiveClip").GetValue(model,null)=="error","single-frame overlay never released");
                type.GetProperty("ReducedMotion").SetValue(model,true,null); type.GetMethod("SetBase").Invoke(model,new object[]{"idle"}); type.GetMethod("Tick").Invoke(model,new object[]{10000}); Assert((int)type.GetProperty("Index").GetValue(model,null)==0,"reduced-motion animated");
            });
            Check("whale size and reduced motion persist independently of translation mode",delegate {
                var size=typeof(Settings).GetField("PetSize"); var motion=typeof(Settings).GetField("ReducedMotion"); Assert(size!=null&&motion!=null,"pet preferences missing");
                string temp=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
                try { var original=new Settings { Mode="auto" }; size.SetValue(original,208); motion.SetValue(original,true); original.Save(temp); var loaded=Settings.Load(temp); Assert((int)size.GetValue(loaded)==208&&(bool)motion.GetValue(loaded)&&loaded.Mode=="auto","pet preference overwrite"); }
                finally { if(File.Exists(temp)) File.Delete(temp); }
            });
            Check("resident mode and physical position survive settings restart", delegate {
                var resident=typeof(Settings).GetField("ResidentOrb"); var x=typeof(Settings).GetField("OrbX"); var y=typeof(Settings).GetField("OrbY");
                Assert(resident!=null&&x!=null&&y!=null,"resident settings missing");
                string temp=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
                try {
                    var original=new Settings(); Assert((bool)resident.GetValue(original),"new or migrated settings must enable orb");
                    resident.SetValue(original,false); x.SetValue(original,-750); y.SetValue(original,325); original.Save(temp);
                    var loaded=Settings.Load(temp); Assert(!(bool)resident.GetValue(loaded)&&(int)x.GetValue(loaded)==-750&&(int)y.GetValue(loaded)==325,"restart lost mode or position");
                } finally { if(File.Exists(temp)) File.Delete(temp); }
            });
            Check("button mode has no automatic request", delegate {
                // Existing selection modes remain independent of clipboard monitoring.
                var policy = new SelectionPolicy();
                Assert(policy.Observe("hello", "button", 1000) == SelectionAction.Button, "expected button only");
            });
            Check("automatic mode requests translation", delegate {
                Assert(new SelectionPolicy().Observe("hello", "auto", 1000) == SelectionAction.Translate, "expected translation");
            });
            Check("clipboard mode survives encrypted settings restart",delegate {
                string temp=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
                try { new Settings { Mode="clipboard" }.Save(temp); Assert(Settings.Load(temp).Mode=="clipboard","clipboard mode lost on restart"); }
                finally { if(File.Exists(temp)) File.Delete(temp); }
            });
            Check("companion mode persists while keeping the desktop pet",delegate {
                string companionFile=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
                try { new Settings { Mode="companion",ResidentOrb=true }.Save(companionFile); var loaded=Settings.Load(companionFile); Assert(loaded.Mode=="companion"&&loaded.ResidentOrb,"companion mode lost on restart"); }
                finally { if(File.Exists(companionFile)) File.Delete(companionFile); }
            });
            Check("reading background preference survives restart independently of mode",delegate {
                var field=typeof(Settings).GetField("BackgroundTheme"); var strength=typeof(Settings).GetField("BackgroundStrength"); Assert(field!=null&&strength!=null,"background preferences missing");
                string themeFile=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
                try { var s=new Settings { Mode="clipboard" }; field.SetValue(s,"angry"); strength.SetValue(s,72); s.Save(themeFile); var loaded=Settings.Load(themeFile); Assert((string)field.GetValue(loaded)=="angry"&&(int)strength.GetValue(loaded)==72&&loaded.Mode=="clipboard","appearance overwrote mode or was lost"); }
                finally { if(File.Exists(themeFile)) File.Delete(themeFile); }
            });
            Check("clipboard watcher ignores existing content and debounces latest copy",delegate {
                var type=typeof(Settings).Assembly.GetType("LightTranslate.ClipboardWatchPolicy"); Assert(type!=null,"watcher missing"); var p=Activator.CreateInstance(type);
                type.GetMethod("Reset").Invoke(p,new object[]{(uint)10});
                Func<uint,long,bool,bool,bool> ready=(seq,now,enabled,own)=>(bool)type.GetMethod("ShouldRead").Invoke(p,new object[]{seq,now,enabled,own});
                Assert(!ready(10,1000,true,false),"old clipboard translated"); Assert(!ready(11,1000,true,false),"copy not debounced");
                Assert(!ready(12,1100,true,false)&&!ready(12,1300,true,false)&&ready(12,1400,true,false),"latest copy not debounced");
                Assert((SelectionAction)type.GetMethod("Accept").Invoke(p,new object[]{" hello "})==SelectionAction.Translate,"new text ignored"); Assert(!ready(12,1500,true,false),"same sequence reread");
                ready(13,1600,true,false); ready(13,1900,true,false); Assert((SelectionAction)type.GetMethod("Accept").Invoke(p,new object[]{"hello"})==SelectionAction.Ignore,"same text resubmitted");
                Assert(!ready(14,2000,true,true)&&!ready(14,2400,true,false),"own copy looped");
                Assert(!ready(15,2500,false,false)&&!ready(15,2900,true,false),"paused copy replayed");
                ready(16,3000,true,false); ready(16,3300,true,false); Assert((SelectionAction)type.GetMethod("Accept").Invoke(p,new object[]{new string('x',6001)})==SelectionAction.TooLong,"oversized text submitted");
            });
            Check("duplicate selection suppressed; clear permits same text", delegate {
                var p = new SelectionPolicy(); p.Observe("hello", "auto", 1000);
                Assert(p.Observe("hello", "auto", 1400) == SelectionAction.Ignore, "duplicate request");
                p.Clear(); Assert(p.Observe("hello", "auto", 1500) == SelectionAction.Translate, "cannot reselect");
            });
            Check("empty and oversized selection never submitted", delegate {
                var p = new SelectionPolicy();
                Assert(p.Observe("  ", "auto", 1000) == SelectionAction.Ignore, "empty");
                Assert(p.Observe(new string('a', 6001), "auto", 1200) == SelectionAction.TooLong, "must reject, not truncate");
            });
            Check("new request invalidates old generation", delegate {
                var g = new RequestGeneration(); var old = g.Next(); var next = g.Next();
                Assert(!g.IsCurrent(old) && g.IsCurrent(next), "stale response visible");
                g.Cancel(); Assert(!g.IsCurrent(next), "close did not invalidate");
            });
            Check("HTTPS endpoint and loopback only", delegate {
                Assert(ApiClient.Endpoint("https://api.deepseek.com/v1").AbsoluteUri == "https://api.deepseek.com/v1/chat/completions", "v1 duplicated");
                Assert(ApiClient.Endpoint("https://api.deepseek.com/chat/completions").AbsoluteUri == "https://api.deepseek.com/chat/completions", "path duplicated");
                Assert(ApiClient.Endpoint("http://127.0.0.1:8888").IsLoopback, "localhost disallowed");
                bool rejected = false; try { ApiClient.Endpoint("http://example.com"); } catch(ArgumentException) { rejected = true; }
                Assert(rejected, "remote plaintext accepted");
                rejected = false; try { ApiClient.Endpoint("https://user:pass@example.com"); } catch(ArgumentException) { rejected=true; }
                Assert(rejected, "embedded credentials accepted");
            });
            Check("JSON request escapes text and includes custom prompt", delegate {
                var s = new Settings(); s.ExplainPrompt = "custom teaching prompt";
                var body = ApiClient.BuildBody(s, "a\"b\n中文", "explain", null);
                var d = new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(body);
                Assert((bool)d["stream"], "stream disabled");
                Assert(body.Contains("custom teaching prompt"), "custom prompt missing");
                Assert(body.Contains("a\\\"b"), "JSON quote missing");
                Assert(body.Contains("disabled"), "thinking needs explicit off for responsive translations");
            });
            Check("SSE skips reasoning and usage; reads text", delegate {
                Assert(ApiClient.ParseDelta("{\"choices\":[{\"delta\":{\"content\":\"你好\"}}]}") == "你好", "wrong content");
                Assert(ApiClient.ParseDelta("{\"choices\":[{\"delta\":{\"reasoning_content\":\"private\"}}]}") == "", "reasoning leaked");
                Assert(ApiClient.ParseDelta("{\"choices\":[]}") == "", "usage chunk");
                bool rejected=false; try { ApiClient.ParseDelta("not json"); } catch(InvalidDataException) { rejected=true; }
                Assert(rejected, "malformed SSE silently accepted");
            });
            Check("popup clamped at negative monitor and lower edge", delegate {
                var p = Geometry.Place(-1900, 1050, 440, 500, -1920, 0, 1920, 1080);
                Assert(p.X >= -1908 && p.X+440 <= -12 && p.Y+500 <= 1068, "popup outside work area");
            });
            Check("DPAPI persistence keeps key out of plaintext", delegate {
                var temp = Path.Combine(Path.GetTempPath(), "LightTranslate-test-"+Guid.NewGuid().ToString("N")+".json");
                try {
                    var s = new Settings(); s.ApiKey = "unit-test-secret-never-real"; s.Mode="auto"; s.Save(temp);
                    var raw = File.ReadAllText(temp); Assert(!raw.Contains(s.ApiKey), "key is plaintext");
                    var loaded = Settings.Load(temp); Assert(loaded.ApiKey==s.ApiKey && loaded.Mode=="auto", "roundtrip failed");
                } finally { if(File.Exists(temp)) File.Delete(temp); }
            });
            Check("HTTP integration streams UTF8 across chunks and sends authorization", delegate { HttpIntegration().GetAwaiter().GetResult(); });
            Check("HTTP authorization failure is readable and redacts body", delegate { HttpError().GetAwaiter().GetResult(); });
            Check("network cancellation finishes promptly", delegate { HttpCancel().GetAwaiter().GetResult(); });
            Check("abrupt SSE close is reported, not success", delegate {
                using(var server=new MockServer("200 OK","data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n\n",false)) {
                    var s=new Settings(); s.BaseUrl=server.Url; s.ApiKey="test-key"; bool detected=false;
                    try { new ApiClient().StreamAsync(s,"hello","translate",null,delegate(string c){},CancellationToken.None).GetAwaiter().GetResult(); }
                    catch(InvalidDataException e) { detected=e.Message.Contains("断开"); }
                    Assert(detected,"interrupted response claimed complete");
                }
            });
            Check("token limited SSE is not marked complete", delegate {
                using(var server=new MockServer("200 OK","data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n\ndata: {\"choices\":[{\"delta\":{},\"finish_reason\":\"length\"}]}\n\ndata: [DONE]\n\n",false)) {
                    var s=new Settings(); s.BaseUrl=server.Url; s.ApiKey="test-key"; bool detected=false;
                    try { new ApiClient().StreamAsync(s,"hello","translate",null,delegate(string c){},CancellationToken.None).GetAwaiter().GetResult(); }
                    catch(InvalidDataException e) { detected=e.Message.Contains("上限"); }
                    Assert(detected,"truncated answer reported success");
                }
            });
            Check("token limited JSON is not marked complete", delegate {
                using(var server=new MockServer("200 OK","{\"choices\":[{\"message\":{\"content\":\"partial\"},\"finish_reason\":\"length\"}]}",false,"application/json")) {
                    var s=new Settings(); s.BaseUrl=server.Url; s.ApiKey="test-key"; bool detected=false;
                    try { new ApiClient().StreamAsync(s,"hello","translate",null,delegate(string c){},CancellationToken.None).GetAwaiter().GetResult(); }
                    catch(InvalidDataException e) { detected=e.Message.Contains("上限"); }
                    Assert(detected,"truncated JSON reported success");
                }
            });
            Check("incomplete JSON body cancels promptly", delegate { JsonCancel().GetAwaiter().GetResult(); });
            Check("response body stream itself honors cancellation", delegate { BodyCancel().GetAwaiter().GetResult(); });
            Check("clipboard from other app or intervening input is rejected", delegate {
                Assert(!ClipboardCapture.CanAccept(10,12,200,100,true,true),"other app clipboard submitted");
                Assert(!ClipboardCapture.CanAccept(10,12,100,100,true,false),"intervening user input ignored");
                Assert(!ClipboardCapture.CanAccept(10,12,100,100,false,true),"foreground changed");
                Assert(!ClipboardCapture.CanAccept(10,10,100,100,true,true),"stale clipboard reused");
                Assert(ClipboardCapture.CanAccept(10,12,100,100,true,true),"attributable copy rejected");
                Assert(ClipboardCapture.CanAccept(10,12,101,100,true,true,true),"same executable child process rejected");
                Assert(!ClipboardCapture.CanAccept(10,12,0,100,true,true,true),"missing owner accepted");
            });
            Check("session cache reuses completed answer only in matching profile", delegate {
                var s=new Settings(); s.ApiKey="cache-test-key"; var cache=new SessionCache(); CachedResult hit;
                cache.Store(s,"phrase","translate","answer"); Assert(cache.TryGet(s,"phrase","translate",out hit)&&hit.Answer=="answer","result not reused");
                Assert(!cache.TryGet(s,"phrase","explain",out hit),"explanation reused translation");
                var changed=s.Copy(); changed.TranslatePrompt="different prompt"; Assert(!cache.TryGet(changed,"phrase","translate",out hit),"prompt change ignored");
                changed=s.Copy(); changed.ApiKey="another-test-key"; Assert(!cache.TryGet(changed,"phrase","translate",out hit),"account change ignored");
            });
            Check("recent results bounded and duplicate-free", delegate {
                var s=new Settings(); var cache=new SessionCache(); CachedResult hit;
                for(int i=0;i<40;i++) cache.Store(s,"phrase "+i,"translate","answer "+i);
                Assert(cache.Recent.Count==30,"history must retain latest 30");
                Assert(!cache.TryGet(s,"phrase 0","translate",out hit),"old cache not evicted");
                cache.Store(s,"phrase 39","translate","updated"); Assert(cache.Recent.Count==30 && cache.Recent[0].Answer=="updated","duplicate recent result");
            });
            Check("history persists last 30 privately and clears across restart",delegate {
                var type=typeof(SessionCache); var ctor=type.GetConstructor(new[]{typeof(string)}); Assert(ctor!=null,"persistent history missing");
                string historyFile=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".dat");
                try {
                    var s=new Settings { ApiKey="history-test-secret" }; var cache=(SessionCache)ctor.Invoke(new object[]{historyFile});
                    for(int i=0;i<35;i++) cache.Store(s,"private phrase "+i,"translate","private answer "+i);
                    var restored=(SessionCache)ctor.Invoke(new object[]{historyFile}); CachedResult result;
                    Assert(restored.Recent.Count==30&&restored.Recent[0].Source=="private phrase 34"&&restored.Recent[29].Source=="private phrase 5","history order or bound lost");
                    Assert(restored.TryGet(s,"private phrase 34","translate",out result)&&result.Answer=="private answer 34","restored result unusable");
                    var changed=s.Copy(); changed.ApiKey="different"; Assert(!restored.TryGet(changed,"private phrase 34","translate",out result),"history bypassed profile check");
                    string raw=Encoding.UTF8.GetString(File.ReadAllBytes(historyFile)); Assert(!raw.Contains("private phrase")&&!raw.Contains(s.ApiKey),"history persisted plaintext");
                    var clear=type.GetMethod("Clear"); Assert(clear!=null,"clear history missing"); clear.Invoke(restored,null);
                    Assert(((SessionCache)ctor.Invoke(new object[]{historyFile})).Recent.Count==0,"cleared history returned");
                    File.WriteAllText(historyFile,"broken history"); Assert(((SessionCache)ctor.Invoke(new object[]{historyFile})).Recent.Count==0,"corrupt history crashed startup");
                } finally { if(File.Exists(historyFile)) File.Delete(historyFile); }
            });
            Check("copy fallback only permits configured executable names",delegate {
                var s=new Settings(); Assert(CopyFallbackPolicy.Allows(s,"Obsidian"),"default Obsidian missing");
                Assert(!CopyFallbackPolicy.Allows(s,"explorer"),"unrelated application permitted");
                s.CopyApplications=" Obsidian.exe, msedge ; Acrobat\nchrome "; Assert(CopyFallbackPolicy.Allows(s,"MSEDGE"),"case/extension matching failed");
                s.CopyFallbackEnabled=false; Assert(!CopyFallbackPolicy.Allows(s,"Obsidian"),"disabled fallback still active");
            });
            Check("copy compatibility settings survive encrypted persistence",delegate {
                string temp=Path.Combine(Path.GetTempPath(),"lighttranslate-input-"+Guid.NewGuid()+".json");
                try { var s=new Settings(); s.CopyApplications="Obsidian,Acrobat"; s.CopyFallbackEnabled=false; s.Save(temp); var loaded=Settings.Load(temp); Assert(!loaded.CopyFallbackEnabled && loaded.CopyApplications==s.CopyApplications,"compatibility setting lost"); }
                finally { if(File.Exists(temp)) File.Delete(temp); }
            });
            Check("OCR linear selection follows word order across lines",delegate {
                var doc=OcrDocument.TestDocument();
                Assert(doc.Select(new System.Windows.Point(62,20),new System.Windows.Point(12,60),false)=="orbit torque\nSwitching","paragraph selection order incorrect");
                Assert(doc.Select(new System.Windows.Point(12,60),new System.Windows.Point(62,20),false)=="orbit torque\nSwitching","reverse selection differs");
                Assert(doc.Select(new System.Windows.Point(490,300),new System.Windows.Point(510,350),false)=="","empty area invented selection");
            });
            Check("OCR rectangle excludes neighboring words and preserves lines",delegate {
                var doc=OcrDocument.TestDocument();
                Assert(doc.Select(new System.Windows.Point(0,0),new System.Windows.Point(51,90),true)=="Spin\nSwitching","rectangle contains outside text");
            });
            Check("native Windows OCR recognizes image text and word geometry",delegate {
                using(var bitmap=new System.Drawing.Bitmap(800,150)) using(var graphics=System.Drawing.Graphics.FromImage(bitmap)) using(var font=new System.Drawing.Font("Arial",32)) using(var memory=new MemoryStream()) {
                    graphics.Clear(System.Drawing.Color.White); graphics.DrawString("Spin orbit torque",font,System.Drawing.Brushes.Black,20,35); bitmap.Save(memory,System.Drawing.Imaging.ImageFormat.Png);
                    var doc=OcrService.RecognizeAsync(memory.ToArray(),CancellationToken.None).GetAwaiter().GetResult();
                    Assert(doc.Words.Count>=3 && doc.Text(System.Linq.Enumerable.Range(0,doc.Words.Count)).Contains("Spin"),"native OCR did not recognize text");
                    Assert(doc.Words[0].Bounds.Width>0 && doc.Words[0].Bounds.X>=0,"OCR word bounds missing");
                }
            });
            Check("OCR respects already cancelled capture",delegate {
                using(var token=new CancellationTokenSource()) { token.Cancel(); bool cancelled=false; try { OcrService.RecognizeAsync(new byte[0],token.Token).GetAwaiter().GetResult(); } catch(OperationCanceledException) { cancelled=true; } Assert(cancelled,"cancelled OCR still started"); }
            });
            Check("copy fallback rejects Shift and Windows modifiers",delegate {
                Assert(ClipboardCapture.ModifiersClear(false,false,false,false),"normal copy blocked");
                Assert(!ClipboardCapture.ModifiersClear(false,false,true,false),"would send Ctrl+Shift+C");
                Assert(!ClipboardCapture.ModifiersClear(false,false,false,true),"would send Ctrl+Win+C");
            });
            Check("cancelled OCR waits for native terminal state before releasing input",delegate { NativeOcrCancel().GetAwaiter().GetResult(); });
            Check("mouse selection must start and end in same external window",delegate {
                var gate=new GestureOrigins(); var external=new IntPtr(1); var own=new IntPtr(2);
                gate.Mouse(true,own,false); Assert(!gate.Mouse(false,external,true),"OCR/popup release became external selection");
                gate.Mouse(true,external,true); Assert(gate.Mouse(false,external,true),"normal selection suppressed");
                gate.Mouse(true,external,true); Assert(!gate.Mouse(false,new IntPtr(3),true),"changed window selection accepted");
            });
            Check("Shift selection cannot carry over from OCR or paused app",delegate {
                var gate=new GestureOrigins(); var external=new IntPtr(1); var own=new IntPtr(2);
                gate.Shift(true,own,false); Assert(!gate.Shift(false,external,true),"OCR Shift release triggered outside probe");
                gate.Shift(true,external,true); Assert(gate.Shift(false,external,true),"normal Shift selection suppressed");
                gate.Shift(true,external,true); gate.Shift(true,external,false); Assert(!gate.Shift(false,external,true),"paused gesture replayed");
            });
            report.AppendLine(String.Format("RESULT {0} passed, {1} failed", passed, failed));
            File.WriteAllText(path, report.ToString(), new UTF8Encoding(true));
            return failed==0 ? 0 : 1;
        }
        static async Task HttpIntegration() {
            using(var server = new MockServer("200 OK", "data: {\"choices\":[{\"delta\":{\"content\":\"你好\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"，世界\"}}]}\n\ndata: [DONE]\n\n", false)) {
                var s=new Settings(); s.BaseUrl=server.Url; s.ApiKey="test-key";
                var result=new StringBuilder();
                await new ApiClient().StreamAsync(s,"hello","translate",null,delegate(string chunk){result.Append(chunk);},CancellationToken.None);
                Assert(result.ToString()=="你好，世界", "stream corrupted");
                Assert(server.Request.Contains("Bearer test-key") && server.Request.Contains("\"stream\":true"), "invalid HTTP request");
            }
        }
        static async Task NativeOcrCancel() {
            var operation=new DelayedOcrOperation(); using(var token=new CancellationTokenSource()) {
                var wait=OcrService.WaitAsync(operation,token.Token); token.Cancel(); await Task.Delay(45);
                Assert(operation.CancelRequested,"native cancellation not requested");
                Assert(!wait.IsCompleted,"returned while native OCR still using input");
                operation.Finish(); bool cancelled=false; try { await wait; } catch(OperationCanceledException) { cancelled=true; }
                Assert(cancelled,"cancelled OCR returned success");
            }
        }
        static async Task HttpError() {
            using(var server=new MockServer("401 Unauthorized", "secret-key-should-not-be-shown", false)) {
                var s=new Settings(); s.BaseUrl=server.Url; s.ApiKey="test-key";
                bool expected=false;
                try { await new ApiClient().StreamAsync(s,"hello","translate",null,delegate(string c){},CancellationToken.None); }
                catch(Exception e) { expected=e.Message.Contains("401") && !e.Message.Contains("secret-key"); }
                Assert(expected,"401 not handled safely");
            }
        }
        static async Task HttpCancel() {
            using(var server=new MockServer("200 OK", "", true)) {
                var s=new Settings(); s.BaseUrl=server.Url; s.ApiKey="test-key";
                using(var c=new CancellationTokenSource(300)) {
                    bool cancelled=false;
                    try { await new ApiClient().StreamAsync(s,"hello","translate",null,delegate(string chunk){},c.Token); }
                    catch(OperationCanceledException) { cancelled=true; }
                    Assert(cancelled,"request was not cancelled");
                }
            }
        }
        static async Task JsonCancel() {
            using(var server=new MockServer("200 OK","{",false,"application/json",true)) {
                var s=new Settings(); s.BaseUrl=server.Url; s.ApiKey="test-key";
                using(var c=new CancellationTokenSource(130)) {
                    var task=new ApiClient().StreamAsync(s,"hello","translate",null,delegate(string chunk){},c.Token);
                    Assert(await Task.WhenAny(task,Task.Delay(700))==task,"body read ignored cancellation");
                    bool cancelled=false; try { await task; } catch(OperationCanceledException) { cancelled=true; }
                    Assert(cancelled,"body read not cancelled");
                }
            }
        }
        static async Task BodyCancel() {
            using(var stream=new BlockingStream()) using(var c=new CancellationTokenSource(120)) {
                var task=ApiClient.ReadBodyAsync(stream,c.Token);
                Assert(await Task.WhenAny(task,Task.Delay(500))==task,"body reader not cancellation-bound");
                bool cancelled=false; try { await task; } catch(OperationCanceledException) { cancelled=true; }
                Assert(cancelled,"body read not cancelled explicitly");
            }
        }
    }
    internal class DelayedOcrOperation: Windows.Foundation.IAsyncInfo {
        volatile bool finished; public bool CancelRequested;
        public uint Id { get { return 1; } } public Exception ErrorCode { get { return null; } }
        public Windows.Foundation.AsyncStatus Status { get { return finished?Windows.Foundation.AsyncStatus.Canceled:Windows.Foundation.AsyncStatus.Started; } }
        public void Cancel() { CancelRequested=true; } public void Close() { } public void Finish() { finished=true; }
    }
    internal class BlockingStream: Stream {
        TaskCompletionSource<int> wait=new TaskCompletionSource<int>();
        public override Task<int> ReadAsync(byte[] b,int o,int n,CancellationToken token) { return wait.Task; }
        public override int Read(byte[] b,int o,int n) { throw new NotSupportedException(); }
        public override bool CanRead { get { return true; } } public override bool CanWrite { get { return false; } } public override bool CanSeek { get { return false; } }
        public override long Length { get { throw new NotSupportedException(); } } public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override void Flush() { } public override long Seek(long o,SeekOrigin s) { throw new NotSupportedException(); } public override void SetLength(long l) { throw new NotSupportedException(); } public override void Write(byte[] b,int o,int n) { throw new NotSupportedException(); }
        protected override void Dispose(bool disposing) { wait.TrySetException(new ObjectDisposedException("test body stream")); base.Dispose(disposing); }
    }
    internal sealed class MockServer : IDisposable {
        TcpListener listener; public string Url; public string Request=""; public int RequestCount;
        public MockServer(string status, string body, bool hang,string contentType="text/event-stream",bool hangBody=false,int maxRequests=1) {
            listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
            Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port;
            Task.Run(async delegate {
                try {
                    for(int requestIndex=0;requestIndex<maxRequests;requestIndex++) using(var socket=await listener.AcceptTcpClientAsync()) using(var stream=socket.GetStream()) {
                        var bytes=new List<byte>(); var one=new byte[1];
                        while(await stream.ReadAsync(one,0,1)>0) { bytes.Add(one[0]); if(bytes.Count>=4 && bytes[bytes.Count-4]==13 && bytes[bytes.Count-3]==10 && bytes[bytes.Count-2]==13 && bytes[bytes.Count-1]==10) break; }
                        var header=Encoding.UTF8.GetString(bytes.ToArray()); int size=0;
                        foreach(var line in header.Split('\n')) if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)) size=int.Parse(line.Substring(15).Trim());
                        var data=new byte[size]; int n=0; while(n<size) { int read=await stream.ReadAsync(data,n,size-n); if(read==0) break; n+=read; }
                        Request=header+Encoding.UTF8.GetString(data);
                        Interlocked.Increment(ref RequestCount);
                        if(hang) { await Task.Delay(1500); return; }
                        var content=Encoding.UTF8.GetBytes(body);
                        var response=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+"\r\nContent-Type: "+contentType+"\r\nContent-Length: "+(content.Length+(hangBody?100:0))+"\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(response,0,response.Length);
                        for(int i=0;i<content.Length;i+=3) { await stream.WriteAsync(content,i,Math.Min(3,content.Length-i)); await Task.Delay(1); }
                        if(hangBody) await Task.Delay(1500);
                    }
                } catch(Exception) { }
            });
        }
        public void Dispose() { listener.Stop(); }
    }
}
