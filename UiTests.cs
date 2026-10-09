using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Linq;

namespace LightTranslate {
    internal static class UiTests {
        static StringBuilder report=new StringBuilder(); static int pass,fail;
        static void Assert(bool value,string text) { if(!value) throw new Exception(text); }
        static async Task Check(string name,Func<Task> task) {
            try { await task(); report.AppendLine("PASS "+name); pass++; }
            catch(Exception e) { report.AppendLine("FAIL "+name+": "+e.Message); fail++; }
        }
        public static int Run(string folder,Application app) {
            Directory.CreateDirectory(folder);
            app.Dispatcher.BeginInvoke(new Action(async delegate {
                var s=new Settings(); s.ApiKey="local-test-key";
                var controller=new AppController(s,true);
                await Check("selection shortcut during OCR startup cannot strand resident state",async delegate {
                    var capture=controller.CaptureScreen; var recognize=controller.Recognizer; var reader=controller.Reader; var foreground=controller.Foreground; int reads=0;
                    try {
                        controller.SetResident(true); controller.CaptureScreen=OcrTestFrame; controller.Recognizer=delegate(byte[] png,System.Threading.CancellationToken token) { return Task.FromResult(OcrTestDocument()); };
                        controller.Foreground=delegate { return new IntPtr(555); }; controller.Reader=delegate(IntPtr hwnd,Point point) { reads++; return Task.FromResult(new SelectionResult { Password=true }); };
                        var starting=controller.StartOcr(); await controller.ShortcutSelection(); await starting;
                        Assert(reads==0&&controller.OcrWindow!=null,"shortcut superseded OCR startup or stranded hidden orb"); controller.Dismiss(); Assert(controller.Orb.IsVisible,"orb did not return");
                    } finally { controller.Dismiss(); controller.Orb.Suspend(); controller.CaptureScreen=capture; controller.Recognizer=recognize; controller.Reader=reader; controller.Foreground=foreground; }
                });
                await Check("invalidated OCR startup releases its own resident suspension",async delegate {
                    var capture=controller.CaptureScreen; var reader=controller.Reader; var foreground=controller.Foreground;
                    try {
                        controller.SetResident(true); controller.CaptureScreen=OcrTestFrame; controller.Foreground=delegate { return new IntPtr(555); }; controller.Reader=delegate(IntPtr hwnd,Point point) { return Task.FromResult(new SelectionResult { Password=true }); };
                        var starting=controller.StartOcr(); await controller.ProbeForTest(new IntPtr(555),new Point(500,300)); await starting;
                        Assert(controller.OcrWindow==null&&controller.Orb.IsVisible,"obsolete startup retained resident suspension");
                    } finally { controller.Dismiss(); controller.Orb.Suspend(); controller.CaptureScreen=capture; controller.Reader=reader; controller.Foreground=foreground; }
                });
                await Check("resident interaction invalidates a pending selection reader",async delegate {
                    var reader=controller.Reader; var foreground=controller.Foreground; string mode=s.Mode;
                    try {
                        s.Mode="button"; controller.SetResident(true); controller.Foreground=delegate { return new IntPtr(555); }; var delayed=new TaskCompletionSource<SelectionResult>();
                        controller.Reader=delegate(IntPtr hwnd,Point point) { return delayed.Task; }; var probe=controller.ProbeForTest(new IntPtr(555),new Point(500,300));
                        controller.Orb.OpenMenu(); delayed.SetResult(new SelectionResult { Text="obsolete selection" }); await probe; Assert(!controller.Chip.IsVisible,"old reader reopened chip during resident interaction");
                    } finally { controller.Dismiss(); controller.Orb.Suspend(); controller.Reader=reader; controller.Foreground=foreground; s.Mode=mode; }
                });
                await Check("resident drag cancels pending automatic request before API call",async delegate {
                    string mode=s.Mode;
                    using(var server=new MockServer("200 OK","data: {\"choices\":[{\"delta\":{\"content\":\"obsolete auto\"}}]}\n\ndata: [DONE]\n\n",false)) {
                        try {
                            s.Mode="auto"; s.BaseUrl=server.Url; controller.Orb.RestorePosition(new Settings { OrbX=340,OrbY=160 }); await Task.Delay(80);
                            var pending=controller.AcceptSelection("automatic selection pending",new Point(500,300)); var point=controller.Orb.DragPoint;
                            await Task.Run(async delegate { await Native.TestDrag(IntPtr.Zero,point,new Point(point.X+70,point.Y+40)); }); await pending;
                            Assert(server.RequestCount==0,"automatic delay sent API while user dragged orb");
                        } finally { controller.Dismiss(); controller.Orb.Suspend(); s.Mode=mode; }
                    }
                });
                await Check("resident mode toggle and dismiss keep the desktop entry",async delegate {
                    controller.SetResident(true); controller.Dismiss(); Assert(controller.Orb.IsVisible,"dismiss hid resident entry");
                    controller.SetResident(false); controller.Dismiss(); Assert(!controller.Orb.IsVisible,"disabled entry reopened");
                    controller.SetResident(true); Assert(controller.Orb.IsVisible,"entry cannot be restored"); controller.Orb.Suspend(); await Task.Delay(30);
                });
                await Check("resident click opens menu without moving and result action makes no request",async delegate {
                    var orb=controller.Orb; orb.RestorePosition(new Settings { OrbX=430,OrbY=260 }); await Task.Delay(80); Rect before=Native.Bounds(orb);
                    try {
                        var clickPoint=orb.DragPoint; await Task.Run(async delegate { await Native.TestClick(clickPoint); }); await Task.Delay(120);
                        Assert(orb.MenuOpen&&Native.Bounds(orb)==before,"click failed or moved orb");
                        var menu=orb.Menu; menu.UpdateLayout(); var bitmap=new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth),(int)Math.Ceiling(menu.ActualHeight),96,96,PixelFormats.Pbgra32); bitmap.Render(menu);
                        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(var stream=File.Create(Path.Combine(folder,"常驻菜单预览.png"))) encoder.Save(stream);
                        MenuItem result=null; foreach(object entry in menu.Items) { var item=entry as MenuItem; if(item!=null&&Convert.ToString(item.Header)=="打开结果窗口") result=item; } Assert(result!=null,"result action absent"); menu.IsOpen=false; result.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        Assert(controller.Popup.IsVisible&&controller.Popup.AnswerText.Contains("选中文字")&&controller.RecentResults.Count==0,"result action made a request or lacked empty guidance");
                    } finally { controller.Dismiss(); orb.Suspend(); }
                });
                await Check("cancel during OCR startup restores orb without late overlay",async delegate {
                    var capture=controller.CaptureScreen; int calls=0;
                    try {
                        controller.SetResident(true); controller.CaptureScreen=delegate { calls++; return OcrTestFrame(); };
                        var pending=controller.StartOcr(); controller.Dismiss(); await pending;
                        Assert(calls==0&&controller.OcrWindow==null&&controller.Orb.IsVisible,"cancel created late overlay or stranded orb");
                    } finally { controller.CaptureScreen=capture; controller.Orb.Suspend(); }
                });
                await Check("resident orb hides before OCR capture and returns on cancel",async delegate {
                    var capture=controller.CaptureScreen; var recognize=controller.Recognizer; bool hidden=false;
                    try {
                        controller.Orb.RestorePosition(s);
                        controller.CaptureScreen=delegate { hidden=!controller.Orb.IsVisible; return OcrTestFrame(); };
                        controller.Recognizer=delegate(byte[] png,System.Threading.CancellationToken token) { return Task.FromResult(OcrTestDocument()); };
                        await controller.StartOcr(); Assert(hidden,"orb contaminated OCR capture");
                        controller.Dismiss(); Assert(controller.Orb.IsVisible,"OCR cancel hid resident orb permanently");
                    } finally { controller.Dismiss(); controller.Orb.Suspend(); controller.CaptureScreen=capture; controller.Recognizer=recognize; }
                });
                await Check("resident orb real drag persists without opening menu",async delegate {
                    var orb=controller.Orb; int saved=0; Action changed=delegate { saved++; }; orb.PositionChanged+=changed;
                    try {
                        var placed=new Settings { OrbX=350,OrbY=230 }; orb.RestorePosition(placed); await Task.Delay(100);
                        Capture(orb,Path.Combine(folder,"常驻浮球预览.png"));
                        Rect before=Native.Bounds(orb); var hwnd=new WindowInteropHelper(orb).Handle; var center=orb.DragPoint;
                        await Task.Run(async delegate { await Native.TestDrag(hwnd,center,new Point(center.X+90,center.Y+60)); }); await Task.Delay(100);
                        Rect after=Native.Bounds(orb); Assert(after.X-before.X>65&&after.Y-before.Y>40&&saved==1&&!orb.MenuOpen,"drag: before="+before+", after="+after+", saves="+saved+", menu="+orb.MenuOpen);
                        orb.Suspend(); placed.OrbX=(int)after.X; placed.OrbY=(int)after.Y; orb.RestorePosition(placed); await Task.Delay(70);
                        Assert(Math.Abs(Native.Bounds(orb).X-after.X)<3,"restored position changed");
                        orb.RestorePosition(new Settings { OrbX=100000,OrbY=-100000 }); Rect clamped=Native.Bounds(orb);
                        var work=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)(clamped.X+clamped.Width/2),(int)(clamped.Y+clamped.Height/2))).WorkingArea;
                        Assert(clamped.X>=work.Left&&clamped.Y>=work.Top&&clamped.Right<=work.Right&&clamped.Bottom<=work.Bottom,"off-screen position inaccessible");
                        Capture(orb,Path.Combine(folder,"常驻浮球预览.png"));
                    } finally { orb.PositionChanged-=changed; orb.Suspend(); }
                });
                await Check("configured app copy fallback shows chip without API request",async delegate {
                    var reader=controller.Reader; var copier=controller.Copier; var foreground=controller.Foreground; var name=controller.WindowProcessName; string previousMode=s.Mode;
                    try {
                        s.Mode="button"; controller.Foreground=delegate { return new IntPtr(555); }; controller.WindowProcessName=delegate(IntPtr hwnd) { return "Obsidian"; };
                        controller.Reader=delegate(IntPtr hwnd,Point point) { return Task.FromResult(new SelectionResult()); }; int calls=0;
                        controller.Copier=delegate(IntPtr hwnd) { calls++; return Task.FromResult("PDF copied selection"); };
                        await controller.ProbeForTest(new IntPtr(555),new Point(500,300));
                        Assert(calls==1 && controller.Chip.IsVisible,"compatible PDF fallback did not show chip");
                        controller.Dismiss(); controller.WindowProcessName=delegate(IntPtr hwnd) { return "explorer"; };
                        await controller.ProbeForTest(new IntPtr(555),new Point(500,300)); Assert(calls==1&&!controller.Chip.IsVisible,"unconfigured app copied");
                    } finally { controller.Reader=reader; controller.Copier=copier; controller.Foreground=foreground; controller.WindowProcessName=name; s.Mode=previousMode; }
                });
                await Check("obsolete compatible capture does not copy or reopen",async delegate {
                    var reader=controller.Reader; var copier=controller.Copier; var foreground=controller.Foreground; var name=controller.WindowProcessName;
                    try {
                        controller.Foreground=delegate { return new IntPtr(555); }; controller.WindowProcessName=delegate(IntPtr hwnd) { return "Obsidian"; }; int calls=0; var delayed=new TaskCompletionSource<SelectionResult>();
                        controller.Reader=delegate(IntPtr hwnd,Point point) { return delayed.Task; }; controller.Copier=delegate(IntPtr hwnd) { calls++; return Task.FromResult("obsolete copy"); };
                        var probe=controller.ProbeForTest(new IntPtr(555),new Point(500,300)); controller.Dismiss(); delayed.SetResult(new SelectionResult()); await probe;
                        Assert(calls==0&&!controller.Chip.IsVisible,"dismissed capture copied or reopened");
                    } finally { controller.Reader=reader; controller.Copier=copier; controller.Foreground=foreground; controller.WindowProcessName=name; }
                });
                await Check("new user input prevents delayed compatible copy",async delegate {
                    var reader=controller.Reader; var copier=controller.Copier; var foreground=controller.Foreground; var name=controller.WindowProcessName; var activity=controller.InputActivity;
                    try {
                        controller.Foreground=delegate { return new IntPtr(555); }; controller.WindowProcessName=delegate(IntPtr hwnd) { return "Obsidian"; }; int calls=0; uint stamp=10; controller.InputActivity=delegate { return stamp; }; var delayed=new TaskCompletionSource<SelectionResult>();
                        controller.Reader=delegate(IntPtr hwnd,Point point) { return delayed.Task; }; controller.Copier=delegate(IntPtr hwnd) { calls++; return Task.FromResult("wrong capture"); };
                        var probe=controller.ProbeForTest(new IntPtr(555),new Point(500,300)); stamp++; delayed.SetResult(new SelectionResult()); await probe;
                        Assert(calls==0,"copied after intervening user input");
                    } finally { controller.Reader=reader; controller.Copier=copier; controller.Foreground=foreground; controller.WindowProcessName=name; controller.InputActivity=activity; }
                });
                await Check("OCR overlay previews editable selected text before submit",async delegate {
                    using(var bitmap=new System.Drawing.Bitmap(800,500)) using(var graphics=System.Drawing.Graphics.FromImage(bitmap)) using(var memory=new MemoryStream()) {
                        graphics.Clear(System.Drawing.Color.White); bitmap.Save(memory,System.Drawing.Imaging.ImageFormat.Png);
                        var frame=ScreenFrame.FromPng(memory.ToArray(),new Rect(300,150,800,500)); var view=new OcrOverlay(frame,OcrDocument.TestDocument(),false); string submitted="";
                        view.Submitted+=delegate(string text,string mode) { submitted=text+":"+mode; }; view.Show(); await Task.Delay(80);
                        try { view.SelectForTest(new Point(62,20),new Point(12,60),false); Assert(view.SelectedText=="orbit torque\nSwitching"&&submitted=="","OCR preview missing or auto-submitted"); view.EditForTest("corrected OCR phrase"); view.SubmitForTest("explain"); Assert(submitted=="corrected OCR phrase:explain","edited OCR not submitted"); }
                        finally { view.Close(); }
                    }
                });
                await Check("OCR overlay rejects oversized text before any submission",async delegate {
                    var view=new OcrOverlay(OcrTestFrame(),OcrDocument.TestDocument(),false); int calls=0; view.Submitted+=delegate { calls++; }; view.Show(); await Task.Delay(60);
                    try { view.EditForTest(new string('a',6001)); view.SubmitForTest("translate"); Assert(calls==0&&view.IsVisible,"oversized OCR submitted"); view.EditForTest("valid text"); view.SubmitForTest("translate"); Assert(calls==1,"valid OCR could not submit"); }
                    finally { view.Close(); }
                });
                await Check("cancelled OCR ignores late recognition and restores reading",async delegate {
                    var capture=controller.CaptureScreen; var recognize=controller.Recognizer;
                    try {
                        controller.Popup.SetAnswer("reading before OCR"); controller.Popup.Show(); controller.CaptureScreen=OcrTestFrame;
                        var delayed=new TaskCompletionSource<OcrDocument>(); controller.Recognizer=delegate(byte[] bytes,System.Threading.CancellationToken token) { return delayed.Task; };
                        var task=controller.StartOcr(); await Task.Delay(240); Assert(controller.OcrWindow!=null,"OCR did not open"); controller.OcrWindow.Close();
                        delayed.SetResult(OcrDocument.TestDocument()); await task;
                        Assert(controller.OcrWindow==null && controller.Popup.IsVisible && controller.Popup.AnswerText=="reading before OCR","cancel changed reading or reopened OCR"); controller.Dismiss();
                    } finally { controller.CaptureScreen=capture; controller.Recognizer=recognize; }
                });
                await Check("dismiss prevents pending screenshot opening",async delegate {
                    var capture=controller.CaptureScreen; int calls=0;
                    try { controller.CaptureScreen=delegate { calls++; return OcrTestFrame(); }; var task=controller.StartOcr(); controller.Dismiss(); await task; Assert(calls==0&&controller.OcrWindow==null,"dismissed screenshot capture reopened"); }
                    finally { controller.CaptureScreen=capture; }
                });
                await Check("edited OCR explanation sends one request through existing API",async delegate {
                    var capture=controller.CaptureScreen; var recognize=controller.Recognizer; string mode=s.Mode;
                    using(var server=new MockServer("200 OK","data: {\"choices\":[{\"delta\":{\"content\":\"识字解释结果\"}}]}\n\ndata: [DONE]\n\n",false)) {
                        try {
                            s.Mode="button"; s.BaseUrl=server.Url; controller.CaptureScreen=OcrTestFrame; controller.Recognizer=delegate(byte[] bytes,System.Threading.CancellationToken token) { return Task.FromResult(OcrDocument.TestDocument()); };
                            await controller.StartOcr(); var view=controller.OcrWindow; Assert(view!=null,"OCR missing");
                            view.SelectForTest(new Point(62,20),new Point(12,60),false); view.EditForTest("edited physics OCR"); view.SubmitForTest("explain");
                            for(int i=0;i<35&&controller.Popup.AnswerText!="识字解释结果";i++) await Task.Delay(60);
                            Assert(server.RequestCount==1 && server.Request.Contains("edited physics OCR") && controller.Popup.CurrentMode=="explain" && controller.Popup.AnswerText=="识字解释结果","OCR request flow incorrect"); controller.Dismiss();
                        } finally { controller.CaptureScreen=capture; controller.Recognizer=recognize; s.Mode=mode; }
                    }
                });
                await Check("native drag in OCR automatic mode submits selected words",async delegate {
                    var frame=OcrTestFrame(); var doc=OcrTestDocument(); var view=new OcrOverlay(frame,doc,true); string submitted=""; int calls=0; view.Submitted+=delegate(string text,string mode) { calls++; submitted=text; }; view.Show(); await Task.Delay(100);
                    try {
                        var hwnd=new WindowInteropHelper(view).Handle; var first=view.ImageToScreen(new Point(22,205)); var last=view.ImageToScreen(new Point(257,205));
                        await Task.Run(async delegate { await Native.TestDrag(hwnd,first,last); }); await Task.Delay(60);
                        Assert(calls==1&&submitted=="Spin orbit torque"&&!view.IsVisible,"native OCR automatic selection failed");
                    } finally { view.Close(); }
                });
                await Check("OCR layout renders word highlights and editable preview",async delegate {
                    var frame=OcrTestFrame(); var doc=await OcrService.RecognizeAsync(frame.Png,System.Threading.CancellationToken.None); var view=new OcrOverlay(frame,doc,false); view.Show(); await Task.Delay(80);
                    var first=doc.Words.First(x=>x.Text=="Spin").Bounds; var last=doc.Words.First(x=>x.Text=="torque").Bounds;
                    try { view.SelectForTest(new Point(first.X+first.Width/2,first.Y+first.Height/2),new Point(last.X+last.Width/2,last.Y+last.Height/2),false); await Task.Delay(50); Capture(view,Path.Combine(folder,"屏幕识字预览.png")); Assert(view.SelectedText=="Spin orbit torque","overlay display selection lost"); Assert(doc.Text(Enumerable.Range(0,doc.Words.Count)).Contains("屏幕"),"native Chinese OCR missing"); }
                    finally { view.Close(); }
                });
                await Check("button mode selection shows chip, no network", async delegate {
                    using(var server=new MockServer("200 OK", "data: {\"choices\":[{\"delta\":{\"content\":\"翻译结果\"}}]}\n\ndata: [DONE]\n\n",false)) {
                        s.BaseUrl=server.Url; s.Mode="button";
                        await controller.AcceptSelection("Selected words",new Point(600,300));
                        await Task.Delay(180);
                        Assert(controller.Chip.IsVisible,"chip not shown");
                        Assert(server.Request.Length==0,"selection itself sent HTTP request");
                        await controller.RequestAsync("translate",null);
                        Assert(controller.Popup.AnswerText=="翻译结果","click did not translate");
                    }
                });
                await Check("repeated query reuses result without second API request", async delegate {
                    using(var server=new MockServer("200 OK", "data: {\"choices\":[{\"delta\":{\"content\":\"复用结果\"}}]}\n\ndata: [DONE]\n\n",false)) {
                        s.BaseUrl=server.Url; await controller.AcceptSelection("cached repeated phrase",new Point(600,300)); await controller.RequestAsync("translate",null);
                        var repeat=controller.RequestAsync("translate",null);
                        bool completed=await Task.WhenAny(repeat,Task.Delay(600))==repeat;
                        if(!completed) controller.Dismiss();
                        Assert(completed,"repeat created another network request"); await repeat;
                        Assert(controller.Popup.AnswerText=="复用结果" && controller.Popup.HistoryCount>0,"cached result/history missing");
                    }
                });
                await Check("automatic mode requests and displays result", async delegate {
                    using(var server=new MockServer("200 OK", "data: {\"choices\":[{\"delta\":{\"content\":\"自动结果\"}}]}\n\ndata: [DONE]\n\n",false)) {
                        s.BaseUrl=server.Url; s.Mode="auto";
                        await controller.AcceptSelection("automatic new selection",new Point(600,300));
                        Assert(controller.Popup.AnswerText=="自动结果","automatic mode did not render");
                    }
                });
                await Check("explicit regeneration bypasses completed cache", async delegate {
                    using(var server=new MockServer("200 OK", "data: {\"choices\":[{\"delta\":{\"content\":\"刷新结果\"}}]}\n\ndata: [DONE]\n\n",false,"text/event-stream",false,2)) {
                        s.BaseUrl=server.Url; s.Mode="button"; await controller.AcceptSelection("explicit refreshed phrase",new Point(600,300));
                        await controller.RequestAsync("translate",null); await controller.RequestAsync("translate",null);
                        Assert(server.RequestCount==1,"regular repeat was not cached");
                        var refresh=controller.RequestAsync("translate",null,true);
                        Assert(await Task.WhenAny(refresh,Task.Delay(2500))==refresh,"refresh did not finish"); await refresh;
                        Assert(server.RequestCount==2 && controller.Popup.AnswerText=="刷新结果","force regeneration reused cache");
                    }
                });
                await Check("rapid automatic selections make only latest request", async delegate {
                    using(var server=new MockServer("200 OK", "data: {\"choices\":[{\"delta\":{\"content\":\"最新结果\"}}]}\n\ndata: [DONE]\n\n",false)) {
                        s.BaseUrl=server.Url; s.Mode="auto"; s.AutoDelay=240;
                        var first=controller.AcceptSelection("outdated selection",new Point(600,300)); await Task.Delay(70);
                        var second=controller.AcceptSelection("latest selection",new Point(600,300));
                        var combined=Task.WhenAll(first,second);
                        Assert(await Task.WhenAny(combined,Task.Delay(2500))==combined,"duplicate automatic request stalled");
                        await combined;
                        Assert(server.Request.Contains("latest selection")&&!server.Request.Contains("outdated selection"),"outdated automatic request submitted");
                        Assert(controller.Popup.AnswerText=="最新结果","latest answer missing");
                    }
                });
                await Check("missing API key shows actionable message", async delegate {
                    s.ApiKey=""; await controller.RequestAsync("explain",null);
                    Assert(controller.Popup.AnswerText.Contains("API Key"),"missing key not explained"); s.ApiKey="local-test-key";
                });
                await Check("close cancels and prevents stale answer", async delegate {
                    using(var server=new MockServer("200 OK","",true)) {
                        s.BaseUrl=server.Url;
                        var task=controller.RequestAsync("translate",null); await Task.Delay(120); controller.Dismiss();
                        await task; Assert(!controller.Popup.IsVisible,"dismiss reopened window");
                    }
                });
                await Check("dismiss invalidates slow shortcut capture", async delegate {
                    var oldForeground=controller.Foreground; var oldReader=controller.Reader; string oldKey=s.ApiKey; s.ApiKey="";
                    var delayed=new TaskCompletionSource<SelectionResult>();
                    try {
                        controller.Foreground=delegate { return new IntPtr(123456); }; controller.Reader=delegate(IntPtr h,Point p) { return delayed.Task; };
                        var capture=controller.ShortcutSelection(); controller.Dismiss(); delayed.SetResult(new SelectionResult { Text="obsolete captured words" });
                        await capture; Assert(!controller.Popup.IsVisible,"old capture reopened dismissed popup");
                    } finally { controller.Foreground=oldForeground; controller.Reader=oldReader; s.ApiKey=oldKey; }
                });
                await Check("new selection supersedes slow shortcut capture", async delegate {
                    var oldForeground=controller.Foreground; var oldReader=controller.Reader; string oldKey=s.ApiKey; s.ApiKey=""; s.Mode="button";
                    var delayed=new TaskCompletionSource<SelectionResult>();
                    try {
                        controller.Foreground=delegate { return new IntPtr(123456); }; controller.Reader=delegate(IntPtr h,Point p) { return delayed.Task; };
                        var capture=controller.ShortcutSelection(); await controller.AcceptSelection("newest selected words",new Point(600,300));
                        delayed.SetResult(new SelectionResult { Text="obsolete captured words" }); await capture;
                        Assert(controller.Chip.IsVisible&&!controller.Popup.IsVisible,"old capture replaced new selection");
                    } finally { controller.Foreground=oldForeground; controller.Reader=oldReader; s.ApiKey=oldKey; }
                });
                await Check("follow-up includes original conversation", async delegate {
                    using(var server=new MockServer("200 OK", "data: {\"choices\":[{\"delta\":{\"content\":\"第二轮回答\"}}]}\n\ndata: [DONE]\n\n",false)) {
                        s.BaseUrl=server.Url;
                        controller.SetConversationForTest("original phrase","first answer");
                        await controller.RequestAsync("followup","为什么？");
                        Assert(server.Request.Contains("original phrase") && server.Request.Contains("first answer") && server.Request.Contains("为什么"),"context missing");
                    }
                });
                await Check("popup wraps long source and shows Markdown", async delegate {
                    controller.Popup.SetSource("Spin–orbit torque induced switching in perpendicular magnetic nanodots.");
                    controller.Popup.SelectMode("explain");
                    controller.Popup.SetAnswer("### 中文解释\n自旋轨道力矩（**spin–orbit torque**）让磁化方向发生翻转。\n\n- 电流产生自旋极化\n- 自旋对磁化施加力矩\n\n公式：τ ∝ m × (σ × m)");
                    controller.Popup.Show(); await Task.Delay(120);
                    Capture(controller.Popup,Path.Combine(folder,"浮窗预览.png"));
                    Assert(controller.Popup.ActualWidth>=350 && controller.Popup.ActualHeight>=300,"layout collapsed");
                });
                await Check("default popup is compact", async delegate {
                    await Task.Delay(30); Assert(controller.Popup.Width<=410 && controller.Popup.Height<=440,"popup still oversized");
                });
                await Check("long answer scrolls with compact scrollbar", async delegate {
                    string previous=controller.Popup.AnswerText;
                    try {
                        controller.Popup.SetAnswer(string.Join("\n",System.Linq.Enumerable.Repeat("长结果需要滚动查看，完整内容应保留。",50)));
                        await Task.Delay(80); var box=FindVisual<RichTextBox>(controller.Popup); var scroll=FindVisual<ScrollViewer>(box);
                        Assert(scroll!=null && scroll.ScrollableHeight>0,"long answer has no scroll range");
                        var bar=FindVerticalScrollbar(scroll);
                        Assert(bar!=null && Ui.Interactive(bar,controller.Popup.Content as DependencyObject),"scrollbar captured by window dragging");
                        Assert(bar.ActualWidth<=9,"scrollbar ignores compact styling: width="+bar.Width+", actual="+bar.ActualWidth+", min="+bar.MinWidth+", max="+bar.MaxWidth+", desired="+bar.DesiredSize+", loaded="+bar.IsLoaded);
                        scroll.ScrollToBottom(); await Task.Delay(40); Assert(scroll.VerticalOffset>0,"cannot reach long answer below fold");
                    } finally { controller.Popup.SetAnswer(previous); }
                });
                await Check("blank header area actually drags the native window", async delegate {
                    var popup=controller.Popup; var before=Native.Bounds(popup); var start=popup.PointToScreen(new Point(popup.ActualWidth*0.46,47));
                    var hwnd=new WindowInteropHelper(popup).Handle;
                    await Task.Run(async delegate { await Native.TestDrag(hwnd,start,new Point(start.X+70,start.Y+35)); });
                    await Task.Delay(100); var after=Native.Bounds(popup);
                    Assert(after.X-before.X>40 && after.Y-before.Y>15,"native drag had no effect");
                });
                await Check("expanded reading preserves content", async delegate {
                    var popup=controller.Popup; double width=popup.Width; string text=popup.AnswerText; popup.ToggleExpanded(); await Task.Delay(80);
                    Assert(popup.Width>width && popup.AnswerText==text,"expand did not preserve reading"); popup.ToggleExpanded();
                });
                await Check("changing translation mode keeps dragged position", async delegate {
                    using(var server=new MockServer("200 OK", "data: {\"choices\":[{\"delta\":{\"content\":\"新解释结果\"}}]}\n\ndata: [DONE]\n\n",false)) {
                        s.BaseUrl=server.Url; controller.SetConversationForTest(controller.Popup.SourceText,controller.Popup.AnswerText);
                        var before=Native.Bounds(controller.Popup); await controller.RequestAsync("explain",null); var after=Native.Bounds(controller.Popup);
                        Assert(Math.Abs(after.X-before.X)<2 && Math.Abs(after.Y-before.Y)<2,"mode switch reset window position");
                    }
                });
                await Check("recent result restores source and answer immediately", async delegate {
                    var item=controller.RecentResults[0]; controller.Dismiss(); controller.RestoreRecent(item); await Task.Delay(60);
                    Assert(controller.Popup.SourceText==item.Source && controller.Popup.AnswerText==item.Answer && controller.Popup.IsVisible,"recent result not restored");
                });
                await Check("bilingual copy contains both source and answer", async delegate {
                    var original=Clipboard.GetDataObject();
                    try { controller.Popup.CopyText(true); string copied=Clipboard.GetText(); Assert(copied.Contains(controller.Popup.SourceText)&&copied.Contains(controller.Popup.AnswerText),"bilingual copy incomplete"); }
                    finally { if(original!=null) Clipboard.SetDataObject(original,true); }
                    await Task.Delay(20);
                });
                await Check("history menu scrolls 30 records and opens without a network request",async delegate {
                    var view=new PopupView(); var entries=new System.Collections.Generic.List<CachedResult>();
                    for(int i=0;i<30;i++) entries.Add(new CachedResult { Source="第 "+i+" 条原文，历史内容可以重新查看",Answer="已保存的结果 "+i,Mode="translate",Model="local-preview",CreatedUtc=DateTime.UtcNow.ToString("o") });
                    CachedResult opened=null; int clears=0; view.RecentRequested+=delegate(CachedResult r) { opened=r; }; view.ClearHistoryRequested+=delegate { clears++; };
                    view.SetRecent(entries); view.Show(); view.ShowRecent(); await Task.Delay(100);
                    try {
                        var menu=view.HistoryMenu; Assert(menu.Items.Count==33&&menu.ActualHeight<=431,"history menu unbounded or records missing");
                        var scroll=FindVisual<ScrollViewer>(menu); Assert(scroll!=null&&scroll.ScrollableHeight>0,"30 rows cannot scroll");
                        Capture(view,Path.Combine(folder,"历史入口预览.png"));
                        ((MenuItem)menu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Assert(opened==entries[0],"history selection lost saved result");
                        ((MenuItem)menu.Items[32]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Assert(clears==1,"clear history action missing");
                    } finally { view.HistoryMenu.IsOpen=false; view.Hide(); }
                });
                await Check("popup switches trigger mode without dismissing result", async delegate {
                    s.Mode="button"; controller.Popup.SetTriggerMode("button");
                    var control=FindButton(controller.Popup,"点击模式"); Assert(control!=null,"trigger switch missing");
                    control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(30);
                    Assert(s.Mode=="auto" && controller.Popup.IsVisible,"trigger switch did not preserve reading");
                });
                await Check("settings contains key field, model and mode", async delegate {
                    var view=new SettingsView(new Settings(),delegate(Settings x){});
                    view.Show(); await Task.Delay(100); Capture(view,Path.Combine(folder,"设置预览.png"));
                    Assert(view.HasRequiredFields,"settings fields missing"); view.Close();
                });
                await Check("companion mode keeps the pet and suppresses selection without disabling manual translation",async delegate {
                    using(var server=new MockServer("200 OK","data: {\"choices\":[{\"delta\":{\"content\":\"手动翻译仍可用\"}}]}\n\ndata: [DONE]\n\n",false)) {
                        var preferences=new Settings { ApiKey="local-test-key",BaseUrl=server.Url,ResidentOrb=false }; var companion=new AppController(preferences,true);
                        try {
                            companion.ChangeMode("companion"); await companion.AcceptSelection("must not translate selection",new Point(600,300)); await Task.Delay(100);
                            Assert(companion.Orb.IsVisible&&!companion.Chip.IsVisible&&!companion.Popup.IsVisible&&server.RequestCount==0,"companion activated automatic selection or hid pet");
                            var option=companion.Orb.Menu.Items.OfType<MenuItem>().FirstOrDefault(x=>Convert.ToString(x.Header)=="仅陪伴 · 关闭划词"); Assert(option!=null&&option.IsChecked,"companion menu missing or unchecked");
                            companion.RestoreRecent(new CachedResult { Source="manual source",Answer="saved",Mode="translate",Model="local",SystemPrompt="translate" }); await companion.RequestAsync("translate",null,true);
                            Assert(server.RequestCount==1&&companion.Popup.AnswerText=="手动翻译仍可用","manual translation disabled"); companion.Dismiss(); Capture(companion.Orb,Path.Combine(folder,"仅陪伴模式.png"));
                        } finally { companion.Dispose(); }
                    }
                });
                await Check("background setting previews and saves without changing clipboard mode",async delegate {
                    Settings saved=null; var view=new SettingsView(new Settings { Mode="clipboard" },delegate(Settings next) { saved=next; }); view.Show();
                    try {
                        view.SelectSection("阅读外观"); await Task.Delay(80);
                        var expander=FindVisual<Expander>(view); Assert(expander!=null,"background section missing"); expander.IsExpanded=true; await Task.Delay(80);
                        var choice=FindVisual<ComboBox>(expander); Assert(choice!=null&&choice.Items.Count==4,"background choices missing"); choice.SelectedIndex=1; await Task.Delay(80);
                        Capture(view,Path.Combine(folder,"背景设置预览.png")); var button=FindButton(view,"保存并开始使用"); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert(saved!=null&&saved.BackgroundTheme=="angry"&&saved.Mode=="clipboard","appearance failed to save or changed trigger mode");
                    } finally { view.Close(); }
                });
                await Check("settings blank header drags and controls stay interactive", async delegate {
                    // Background controls live in their own collapsible section.
                    var view=new SettingsView(new Settings(),delegate(Settings x){}); view.Show(); await Task.Delay(70);
                    try {
                        var before=Native.Bounds(view); var start=view.PointToScreen(new Point(view.ActualWidth*0.65,50)); var hwnd=new WindowInteropHelper(view).Handle;
                        await Task.Run(async delegate { await Native.TestDrag(hwnd,start,new Point(start.X+60,start.Y+30)); }); await Task.Delay(80);
                        var after=Native.Bounds(view); Assert(after.X-before.X>35 && after.Y-before.Y>12,"settings drag failed");
                        var save=FindButton(view,"保存并开始使用"); Assert(Ui.Interactive(save,view.Content as DependencyObject),"button treated as drag surface");
                    } finally { view.Close(); }
                });
                await Check("cross-process UIA reads real selected text", async delegate {
                    string info=Path.Combine(folder,"fixture-handle.txt");
                    using(var process=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--fixture \""+info+"\"") { UseShellExecute=false })) {
                        try {
                            for(int i=0;i<60&&!File.Exists(info);i++) await Task.Delay(100);
                            Assert(File.Exists(info),"fixture not ready");
                            long hwnd=long.Parse(File.ReadAllText(info));
                            var selected=await SelectionService.ReadAsync(new IntPtr(hwnd),new Point(500,400));
                            Assert(selected.Text=="A real selected phrase about spin orbit torque.","UIA selection mismatch: "+selected.Text);
                        } finally { if(!process.HasExited) { process.CloseMainWindow(); if(!process.WaitForExit(500)) process.Kill(); } if(File.Exists(info)) File.Delete(info); if(File.Exists(info+".bounds")) File.Delete(info+".bounds"); }
                    }
                });
                controller.Dispose();
                report.AppendLine(String.Format("RESULT {0} passed, {1} failed",pass,fail));
                File.WriteAllText(Path.Combine(folder,"ui-test.txt"),report.ToString(),new UTF8Encoding(true));
                app.Shutdown(fail==0?0:1);
            }));
            return app.Run();
        }
        public static void Capture(Window view,string path) {
            view.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(view);
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var f=File.Create(path)) encoder.Save(f);
        }
        static Button FindButton(DependencyObject root,string text) {
            var button=root as Button; if(button!=null && Convert.ToString(button.Content)==text) return button;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) { var found=FindButton(VisualTreeHelper.GetChild(root,i),text); if(found!=null) return found; }
            return null;
        }
        internal static OcrDocument OcrTestDocument() {
            return new OcrDocument { Words=new System.Collections.Generic.List<RecognizedWord> {
                new RecognizedWord { Text="Spin",Line=0,Bounds=new Rect(20,190,70,35) },new RecognizedWord { Text="orbit",Line=0,Bounds=new Rect(103,190,78,35) },new RecognizedWord { Text="torque",Line=0,Bounds=new Rect(198,190,110,35) } } };
        }
        internal static ScreenFrame OcrTestFrame() {
            using(var bitmap=new System.Drawing.Bitmap(1200,800)) using(var graphics=System.Drawing.Graphics.FromImage(bitmap)) using(var font=new System.Drawing.Font("Arial",26)) using(var body=new System.Drawing.Font("Microsoft YaHei UI",18)) using(var memory=new MemoryStream()) {
                graphics.Clear(System.Drawing.Color.FromArgb(247,248,251)); graphics.DrawString("Spin orbit torque",font,System.Drawing.Brushes.Black,20,185);
                graphics.DrawString("Current-induced magnetization switching",body,System.Drawing.Brushes.DimGray,20,260); graphics.DrawString("屏幕上的文字，也可以轻松理解。",body,System.Drawing.Brushes.DimGray,20,320);
                bitmap.Save(memory,System.Drawing.Imaging.ImageFormat.Png); return ScreenFrame.FromPng(memory.ToArray(),new Rect(200,80,1200,800));
            }
        }
        static T FindVisual<T>(DependencyObject root) where T:DependencyObject {
            if(root==null) return null; var match=root as T; if(match!=null) return match;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) { var found=FindVisual<T>(VisualTreeHelper.GetChild(root,i)); if(found!=null) return found; }
            return null;
        }
        static System.Windows.Controls.Primitives.ScrollBar FindVerticalScrollbar(DependencyObject root) {
            var bar=root as System.Windows.Controls.Primitives.ScrollBar; if(bar!=null && bar.Orientation==Orientation.Vertical) return bar;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) { var found=FindVerticalScrollbar(VisualTreeHelper.GetChild(root,i)); if(found!=null) return found; }
            return null;
        }
        public static void Fixture(Application app,string path) {
            var box=new TextBox { Text="A real selected phrase about spin orbit torque.",FontSize=20,Margin=new Thickness(30),AcceptsReturn=true,TextWrapping=TextWrapping.Wrap };
            var window=new Window { Title="LightTranslate selection fixture",Width=700,Height=300,Left=300,Top=280,Content=box };
            window.Loaded+=delegate {
                box.Focus(); box.SelectAll(); var top=box.PointToScreen(new Point(0,0));
                File.WriteAllText(path+".bounds",top.X.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+top.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                File.WriteAllText(path,new WindowInteropHelper(window).Handle.ToInt64().ToString());
            };
            window.Closed+=delegate { app.Shutdown(); };
            app.Run(window);
        }
    }
}
