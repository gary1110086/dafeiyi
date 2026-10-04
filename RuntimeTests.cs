using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace LightTranslate {
    internal static class RuntimeTests {
        public static int Run(string folder,Application app) {
            Directory.CreateDirectory(folder);
            app.Dispatcher.BeginInvoke(new Action(async delegate {
                int fail=0; var report=new StringBuilder(); AppController controller=null; Process process=null; var cursor=Native.Cursor(); System.Windows.IDataObject oldClipboard=null;
                string info=Path.Combine(folder,"runtime-fixture.txt");
                try {
                    oldClipboard=Clipboard.GetDataObject();
                    using(var server=new MockServer("200 OK","data: {\"choices\":[{\"delta\":{\"content\":\"实际快捷键结果\"}}]}\n\ndata: [DONE]\n\n",false)) {
                        var s=new Settings(); s.ApiKey="runtime-test-key"; s.BaseUrl=server.Url;
                        controller=new AppController(s,false);
                        controller.SettingsSavePath=Path.Combine(folder,"runtime-settings.json");
                        if(!controller.ShortcutsReady) throw new Exception("global shortcuts not registered"); report.AppendLine("PASS native tray and global hotkeys initialized");
                        process=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--fixture \""+info+"\"") { UseShellExecute=false });
                        for(int i=0;i<60&&!File.Exists(info);i++) await Task.Delay(100);
                        if(!File.Exists(info)) throw new Exception("fixture not ready");
                        var hwnd=new IntPtr(long.Parse(File.ReadAllText(info))); var bounds=File.ReadAllText(info+".bounds").Split(',');
                        double x=double.Parse(bounds[0],System.Globalization.CultureInfo.InvariantCulture),y=double.Parse(bounds[1],System.Globalization.CultureInfo.InvariantCulture);
                        controller.Dismiss(); controller.Orb.RestorePosition(new Settings { OrbX=80,OrbY=140 }); await Task.Delay(100);
                        // Windows can reject programmatic foreground activation from a hidden test runner.
                        // Click the fixture's empty client margin before checking that the pet preserves focus.
                        await Native.TestClick(new Point(x+50,y-18)); Native.TestFocus(hwnd); await Task.Delay(100);
                        if(Native.GetForegroundWindow()!=hwnd) throw new Exception("resident test fixture did not acquire focus before drag");
                        var residentReader=controller.Reader; int reads=0; controller.Reader=delegate(IntPtr target,Point point) { reads++; return Task.FromResult(new SelectionResult()); };
                        try {
                            var orbPoint=controller.Orb.DragPoint;
                            if(Native.RootAt(orbPoint)!=new System.Windows.Interop.WindowInteropHelper(controller.Orb).Handle) throw new Exception("resident test drag target misses orb: "+orbPoint+" bounds="+Native.Bounds(controller.Orb));
                            await Native.TestDrag(IntPtr.Zero,orbPoint,new Point(orbPoint.X+70,orbPoint.Y+50)); await Task.Delay(300);
                            if(Native.GetForegroundWindow()!=hwnd||reads!=0||controller.Chip.IsVisible||controller.Popup.IsVisible) throw new Exception("resident drag: external focus="+(Native.GetForegroundWindow()==hwnd)+", own focus="+Native.IsOwnWindow(Native.GetForegroundWindow())+", foreground app="+Native.ProcessName(Native.GetForegroundWindow())+", point="+orbPoint+", bounds="+Native.Bounds(controller.Orb)+", reads="+reads+", chip="+controller.Chip.IsVisible+", popup="+controller.Popup.IsVisible);
                            var stored=Settings.Load(controller.SettingsSavePath); var orbBounds=Native.Bounds(controller.Orb);
                            if(stored.OrbX!=(int)orbBounds.X||stored.OrbY!=(int)orbBounds.Y) throw new Exception("resident native drag failed to persist position");
                            report.AppendLine("PASS native resident drag preserves external focus, avoids selection replay and saves position");
                        } finally { controller.Reader=residentReader; }
                        await Native.TestDrag(hwnd,new Point(x+9,y+17),new Point(x+240,y+17));
                        for(int i=0;i<45&&!controller.Chip.IsVisible;i++) await Task.Delay(100);
                        if(!controller.Chip.IsVisible) {
                            var diagnostic=await SelectionService.ReadAsync(hwnd,new Point(x+240,y+17));
                            var element=System.Windows.Automation.AutomationElement.FromHandle(hwnd).FindFirst(System.Windows.Automation.TreeScope.Descendants,new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty,System.Windows.Automation.ControlType.Edit));
                            throw new Exception("real mouse drag did not display chip; foreground="+(Native.GetForegroundWindow()==hwnd)+", selected length="+diagnostic.Text.Length+", recorded="+x+","+y+", live="+(element==null?"none":element.Current.BoundingRectangle.ToString()));
                        }
                        if(server.Request.Length>0) throw new Exception("button mode sent request before click");
                        report.AppendLine("PASS real mouse drag in another process shows chip without API request");
                        UiTests.Capture(controller.Chip,Path.Combine(folder,"小按钮预览.png"));
                        Native.TestHotkeyD();
                        for(int i=0;i<45&&controller.Popup.AnswerText!="实际快捷键结果";i++) await Task.Delay(100);
                        if(controller.Popup.AnswerText!="实际快捷键结果") throw new Exception("native Ctrl+Alt+D did not translate selected text");
                        report.AppendLine("PASS real Ctrl+Alt+D reads selection and streams local API result");
                        using(var automatic=new MockServer("200 OK","data: {\"choices\":[{\"delta\":{\"content\":\"真实自动翻译结果\"}}]}\n\ndata: [DONE]\n\n",false)) {
                            s.Mode="auto"; s.BaseUrl=automatic.Url;
                            await Native.TestDrag(hwnd,new Point(x+9,y+17),new Point(x+370,y+17));
                            for(int i=0;i<45&&controller.Popup.AnswerText!="真实自动翻译结果";i++) await Task.Delay(100);
                            if(controller.Popup.AnswerText!="真实自动翻译结果") throw new Exception("real drag in automatic mode did not translate");
                            report.AppendLine("PASS real mouse drag in automatic mode streams result without click");
                        }
                        var clipboard=new DataObject(); clipboard.SetData(DataFormats.UnicodeText,"original clipboard fixture"); clipboard.SetData(DataFormats.Html,"<b>original html fixture</b>"); Clipboard.SetDataObject(clipboard,true);
                        using(var watched=new MockServer("200 OK","data: {\"choices\":[{\"delta\":{\"content\":\"剪贴板自动翻译结果\"}}]}\n\ndata: [DONE]\n\n",false)) {
                            s.BaseUrl=watched.Url; controller.ChangeMode("clipboard"); await Task.Delay(450);
                            if(watched.RequestCount!=0) throw new Exception("clipboard mode replayed existing text");
                            Native.TestFocus(hwnd); await Task.Delay(120);
                            string text=await Native.TryCopySelection(hwnd);
                            if(text.Length==0) throw new Exception("external clipboard test copy failed: "+Native.CopyDiagnostic+", foreground="+(Native.GetForegroundWindow()==hwnd));
                            for(int i=0;i<45&&controller.Popup.AnswerText!="剪贴板自动翻译结果";i++) await Task.Delay(100);
                            if(controller.Popup.AnswerText!="剪贴板自动翻译结果"||watched.RequestCount!=1||!watched.Request.Contains(text)) throw new Exception("new external clipboard text did not translate");
                            report.AppendLine("PASS external Ctrl+C automatically translates through the native clipboard monitor");
                            controller.Popup.CopyText(false); await Task.Delay(500);
                            if(watched.RequestCount!=1) throw new Exception("copying result triggered self translation");
                            Native.TestFocus(hwnd); await Task.Delay(120); await Native.TryCopySelection(hwnd); await Task.Delay(500);
                            if(watched.RequestCount!=1) throw new Exception("duplicate external copy sent another request");
                            report.AppendLine("PASS copying own result and repeated external text do not send another API request");
                            controller.ChangeMode("button"); s.BaseUrl=server.Url;
                        }
                        string copied=await Native.TryCopySelection(hwnd);
                        if(string.IsNullOrEmpty(copied) || Clipboard.GetText()!=copied) throw new Exception("copy fallback did not leave the latest clipboard contents; foreground="+(Native.GetForegroundWindow()==hwnd)+", modifiers="+Native.ModifiersReleased()+", copied length="+copied.Length+", reason="+Native.CopyDiagnostic);
                        report.AppendLine("PASS explicit copy fallback retains latest clipboard without automatic rollback");
                        var oldReader=controller.Reader; var oldCopier=controller.Copier; var oldApps=s.CopyApplications; int fallbackReads=0,fallbackCopies=0; s.Mode="button"; s.CopyApplications=Process.GetCurrentProcess().ProcessName;
                        try {
                            controller.Reader=delegate(IntPtr target,Point point) { fallbackReads++; return Task.FromResult(new SelectionResult()); }; controller.Copier=async delegate(IntPtr target) { fallbackCopies++; return await oldCopier(target); }; controller.Dismiss();
                            await Native.TestDrag(hwnd,new Point(x+9,y+17),new Point(x+310,y+17));
                            for(int i=0;i<40&&!controller.Chip.IsVisible;i++) await Task.Delay(70);
                            if(!controller.Chip.IsVisible || !Clipboard.GetText().Contains("selected")) throw new Exception("native compatible copy fallback did not produce selection chip; chip="+controller.Chip.IsVisible+", reads="+fallbackReads+", copies="+fallbackCopies+", foreground="+(Native.GetForegroundWindow()==hwnd)+", modifiers="+Native.ModifiersReleased()+", diagnostic="+Native.CopyDiagnostic);
                            report.AppendLine("PASS native selection gesture uses configured copy fallback when UIA returns empty"); controller.Dismiss();
                        } finally { controller.Reader=oldReader; controller.Copier=oldCopier; s.CopyApplications=oldApps; }
                        var capture=controller.CaptureScreen; var recognize=controller.Recognizer;
                        using(var ocrServer=new MockServer("200 OK","data: {\"choices\":[{\"delta\":{\"content\":\"截图快捷键结果\"}}]}\n\ndata: [DONE]\n\n",false)) {
                            try {
                                var actualFrame=ScreenFrame.Capture(); if(actualFrame.Png.Length<1000 || actualFrame.Image.PixelWidth<100) throw new Exception("native screen capture is empty"); report.AppendLine("PASS native monitor capture produces in-memory screen image");
                                controller.CaptureScreen=UiTests.OcrTestFrame; controller.Recognizer=OcrService.RecognizeAsync; s.BaseUrl=ocrServer.Url; s.Mode="auto";
                                Native.TestHotkeyS(); for(int i=0;i<40&&controller.OcrWindow==null;i++) await Task.Delay(70);
                                if(controller.OcrWindow==null) throw new Exception("Ctrl+Alt+S did not open OCR overlay");
                                for(int i=0;i<80&&controller.OcrWindow!=null&&!controller.OcrWindow.Ready;i++) await Task.Delay(50);
                                if(controller.OcrWindow==null || !controller.OcrWindow.Ready) throw new Exception("native OCR did not become ready");
                                var view=controller.OcrWindow; var doc=await OcrService.RecognizeAsync(UiTests.OcrTestFrame().Png,System.Threading.CancellationToken.None);
                                var first=System.Linq.Enumerable.First(doc.Words,w=>w.Text=="Spin").Bounds; var last=System.Linq.Enumerable.First(doc.Words,w=>w.Text=="torque").Bounds;
                                var start=view.ImageToScreen(new Point(first.X+first.Width/2,first.Y+first.Height/2)); var end=view.ImageToScreen(new Point(last.X+last.Width/2,last.Y+last.Height/2));
                                var ocrHandle=new System.Windows.Interop.WindowInteropHelper(view).Handle;
                                await Task.Run(async delegate { await Native.TestDrag(ocrHandle,start,end); });
                                for(int i=0;i<40&&controller.Popup.AnswerText!="截图快捷键结果";i++) await Task.Delay(70);
                                if(controller.Popup.AnswerText!="截图快捷键结果"||!ocrServer.Request.Contains("Spin orbit torque")) throw new Exception("native OCR drag failed: submitted="+view.WasSubmitted+", selected="+view.SelectedText+", requests="+ocrServer.RequestCount+", popup="+controller.Popup.AnswerText+", source="+controller.Popup.SourceText);
                                report.AppendLine("PASS native Ctrl+Alt+S with real OCR and mouse selection translates through local API");
                            } finally { controller.CaptureScreen=capture; controller.Recognizer=recognize; }
                        }
                        report.AppendLine("WorkingSet MB: "+(Process.GetCurrentProcess().WorkingSet64/(1024.0*1024)).ToString("0.0"));
                    }
                } catch(Exception e) { report.AppendLine("FAIL "+e.Message); fail++; }
                finally {
                    if(controller!=null) controller.Dispose();
                    if(process!=null) { if(!process.HasExited) { process.CloseMainWindow(); if(!process.WaitForExit(500)) process.Kill(); } process.Dispose(); }
                    Native.TestRestoreCursor(cursor);
                    try { if(oldClipboard!=null) Clipboard.SetDataObject(oldClipboard,true); } catch { }
                    if(File.Exists(info)) File.Delete(info); if(File.Exists(info+".bounds")) File.Delete(info+".bounds");
                }
                report.AppendLine(fail==0?"RESULT "+System.Text.RegularExpressions.Regex.Matches(report.ToString(),"^PASS ",System.Text.RegularExpressions.RegexOptions.Multiline).Count+" passed, 0 failed":"RESULT runtime test failed");
                File.WriteAllText(Path.Combine(folder,"runtime-test.txt"),report.ToString(),new UTF8Encoding(true)); app.Shutdown(fail==0?0:1);
            }));
            return app.Run();
        }
    }
}
