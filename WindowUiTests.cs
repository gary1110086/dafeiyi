using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
namespace LightTranslate {
    internal static class WindowUiTests {
        static int pass,fail; static StringBuilder report=new StringBuilder(); static string reportPath;
        static void Assert(bool value,string reason) { if(!value) throw new Exception(reason); }
        static IEnumerable<DependencyObject> Nodes(DependencyObject root) { yield return root; foreach(object child in LogicalTreeHelper.GetChildren(root)) { var node=child as DependencyObject; if(node!=null) foreach(var x in Nodes(node)) yield return x; } }
        static async Task Check(string name,Func<Task> test) { try { await test(); pass++; report.AppendLine("PASS "+name); } catch(Exception e) { fail++; report.AppendLine("FAIL "+name+": "+e.Message); } File.WriteAllText(reportPath,report.ToString(),new UTF8Encoding(true)); }
        static void Poll(AppController c) { var method=typeof(AppController).GetMethod("UpdatePopupVisibility",BindingFlags.Instance|BindingFlags.NonPublic); Assert(method!=null,"result window has no independent focus lifecycle"); method.Invoke(c,null); }
        static void Outside(AppController c,Point point) { var method=typeof(AppController).GetMethod("HandlePopupAction",BindingFlags.Instance|BindingFlags.NonPublic); Assert(method!=null,"result window has no outside click handling"); method.Invoke(c,new object[]{2,point}); }
        internal static int Run(string folder,Application app) {
            Directory.CreateDirectory(folder); reportPath=Path.Combine(folder,"window-test.txt"); app.Dispatcher.BeginInvoke(new Action(async delegate {
                var cursor=Native.Cursor(); IDataObject oldClipboard=null; bool clipboardSaved=false; try { oldClipboard=Clipboard.GetDataObject(); clipboardSaved=true; } catch { }
                try {
                    await Check("all trigger modes close unpinned result when foreground changes",async delegate {
                        foreach(string mode in new[]{"button","auto","clipboard","companion"}) { var s=new Settings { Mode=mode,Enabled=false,ResidentOrb=false }; var c=new AppController(s,true); try { IntPtr foreground=new IntPtr(41); c.Foreground=delegate { return foreground; }; c.Popup.SetAnswer("本地阅读结果"); c.Popup.Show(); Poll(c); Assert(c.Popup.IsVisible,"passive result closed as soon as shown"); foreground=new IntPtr(42); Poll(c); Assert(!c.Popup.IsVisible,"focus loss did not close in "+mode); } finally { c.Dispose(); } } await Task.Delay(30);
                    });
                    await Check("click back in same source window dismisses even without foreground change; pinned result stays",async delegate {
                        var c=new AppController(new Settings { Mode="clipboard",ResidentOrb=false },true); try { c.Foreground=delegate { return new IntPtr(41); }; c.Popup.Show(); Poll(c); var outside=new Point(1,1); Outside(c,outside); Assert(!c.Popup.IsVisible,"same-owner outside click did not dismiss"); c.Popup.Pinned=true; c.Popup.Show(); Outside(c,outside); c.Foreground=delegate { return new IntPtr(42); }; Poll(c); Assert(c.Popup.IsVisible,"pinned result dismissed"); c.Popup.Pinned=false; Poll(c); Assert(!c.Popup.IsVisible,"unpin did not resume focus lifecycle"); } finally { c.Dispose(); } await Task.Delay(30);
                    });
                    await Check("focused result closes on deactivation but local menus remain usable",async delegate {
                        var c=new AppController(new Settings { ResidentOrb=false },true); var local=new Window { Title="大肥译 local companion dialog",Width=320,Height=180,Left=80,Top=90 };
                        try { c.Popup.Show(); c.Popup.Activate(); await Task.Delay(100); Poll(c); local.Show(); local.Activate(); await Task.Delay(100); Poll(c); Assert(c.Popup.IsVisible,"related local dialog dismissed result"); var aa=Nodes(c.Popup).OfType<Button>().First(x=>Equals(x.Content,"Aa")); aa.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(80); Poll(c); Assert(c.Popup.IsVisible,"Aa menu dismissed result"); foreach(var menu in app.Windows.Cast<Window>().SelectMany(x=>Nodes(x).OfType<ContextMenu>())) menu.IsOpen=false;
                            c.Foreground=delegate { return new IntPtr(43); }; Poll(c); Assert(!c.Popup.IsVisible,"previously focused result did not dismiss");
                        } finally { c.Dispose(); local.Close(); }
                    });
                    await Check("eight resize handles respond to real drag and keep custom dimensions through new content",async delegate {
                        var p=new PopupView(); try { p.SetSource("Spin orbit torque"); p.SetAnswer("## 自旋轨道力矩\n\n公式 $E=mc^2$。\n\n拖动窗口边缘，让阅读更舒适。"); p.Show(); Native.Place(p,new Point(400,200)); await Task.Delay(100); var handles=Nodes(p).OfType<Thumb>().Where(x=>(x.Tag as string??"").StartsWith("resize:")).ToArray(); Assert(handles.Length==8,"eight resize handles missing"); var grip=handles.First(x=>Equals(x.Tag,"resize:10")); var start=grip.PointToScreen(new Point(grip.ActualWidth/2,grip.ActualHeight/2)); var before=Native.Bounds(p); var hwnd=new WindowInteropHelper(p).Handle; await Task.Run(async delegate { await Native.TestDrag(hwnd,start,new Point(start.X+160,start.Y+100)); }); await Task.Delay(120); var after=Native.Bounds(p); Assert(after.Width>before.Width+100&&after.Height>before.Height+60,"corner drag did not enlarge window"); p.SetAnswer("短结果。"); p.SetBusy(false,"本地测试"); await Task.Delay(80); var final=Native.Bounds(p); Assert(Math.Abs(final.Width-after.Width)<=2&&Math.Abs(final.Height-after.Height)<=2,"new content reset manual size"); UiTests.Capture(p,Path.Combine(folder,"窗口-自由缩放.png")); p.ToggleExpanded(); Assert(p.Width==500,"expanded preset stopped working"); p.ToggleExpanded(); Assert(p.Width==400,"compact preset stopped working"); } finally { p.Hide(); p.Close(); }
                    });
                    await Check("resize respects minimum dimensions and working area for each edge",async delegate {
                        var p=new PopupView(); try { p.Show(); Native.Place(p,new Point(400,200)); await Task.Delay(80); var handles=Nodes(p).OfType<Thumb>().Where(x=>(x.Tag as string??"").StartsWith("resize:")).ToArray(); Assert(handles.Length==8,"resize handles missing"); var right=handles.First(x=>Equals(x.Tag,"resize:2")); right.RaiseEvent(new DragDeltaEventArgs(-2000,0)); await Task.Delay(50); Assert(p.Width>=p.MinWidth-1,"minimum width violated"); var bottom=handles.First(x=>Equals(x.Tag,"resize:8")); bottom.RaiseEvent(new DragDeltaEventArgs(0,-2000)); await Task.Delay(50); Assert(p.Height>=p.MinHeight-1,"minimum height violated"); var corner=handles.First(x=>Equals(x.Tag,"resize:10")); corner.RaiseEvent(new DragDeltaEventArgs(5000,5000)); await Task.Delay(50); var b=Native.Bounds(p); var work=System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(p).Handle).WorkingArea; Assert(b.Right<=work.Right&&b.Bottom<=work.Bottom,"resize crossed taskbar or monitor edge"); } finally { p.Hide(); p.Close(); }
                    });
                    await Check("opening another saved result re-arms passive focus origin without reopening hidden requests",async delegate {
                        var c=new AppController(new Settings { ResidentOrb=false },true); try { IntPtr foreground=new IntPtr(41); c.Foreground=delegate { return foreground; }; c.Popup.Show(); Poll(c); foreground=new IntPtr(42); c.RestoreRecent(new CachedResult { Source="another source",Answer="saved result",Mode="translate",Model="local",SystemPrompt="local" }); Poll(c); Assert(c.Popup.IsVisible&&c.Popup.AnswerText=="saved result","new explicit result kept stale focus origin"); foreground=new IntPtr(43); Poll(c); Assert(!c.Popup.IsVisible,"re-armed result did not dismiss on subsequent switch"); } finally { c.Dispose(); } await Task.Delay(20);
                    });
                    await Check("native outside clicks and focus changes close passive result; pin protects; dismissal cancels stream",async delegate {
                        Assert(clipboardSaved,"cannot snapshot clipboard before native copy test");
                        var c=new AppController(new Settings { Mode="companion",ResidentOrb=false,ApiKey="local-test-key" },false); c.SettingsSavePath=Path.Combine(folder,"window-settings.json"); System.Diagnostics.Process fixture=null; string info=Path.Combine(folder,"window-fixture-"+Guid.NewGuid().ToString("N"));
                        try {
                            Assert(c.ShortcutsReady,"native shortcut setup failed"); fixture=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,"--fixture \""+info+"\"") { UseShellExecute=false });
                            for(int i=0;i<60&&!File.Exists(info);i++) await Task.Delay(50); Assert(File.Exists(info),"external fixture not ready"); var hwnd=new IntPtr(long.Parse(File.ReadAllText(info))); var coords=File.ReadAllText(info+".bounds").Split(','); var point=new Point(double.Parse(coords[0],System.Globalization.CultureInfo.InvariantCulture)+50,double.Parse(coords[1],System.Globalization.CultureInfo.InvariantCulture)+20);
                            await Native.TestClick(point); Native.TestFocus(hwnd); await Task.Delay(100); Assert(Native.GetForegroundWindow()==hwnd,"external fixture could not acquire focus");
                            Native.TestSelectAll(); await Task.Delay(100); var selected=await SelectionService.ReadAsync(hwnd,point); Assert(selected.Text.Length>0,"guard fixture could not select text"); Assert((await Native.TryCopySelection(hwnd)).Length>0,"copy guard baseline selection empty; selected="+selected.Text.Length+", reason="+Native.CopyDiagnostic); var interrupted=Native.TryCopySelection(hwnd); Native.TestKey(0x27); Assert((await interrupted).Length==0,"copy accepted intervening key input");
                            c.Popup.SetAnswer("被动显示，本地结果"); c.Popup.Show(); Native.Place(c.Popup,new Point(1200,120)); await Task.Delay(120); Assert(c.Popup.IsVisible&&Native.GetForegroundWindow()==hwnd,"passive result vanished or stole focus");
                            await Native.TestClick(point); await Task.Delay(120); Assert(!c.Popup.IsVisible,"native outside click in same source did not close");
                            c.Popup.Pinned=true; c.Popup.Show(); c.Popup.Activate(); await Task.Delay(100); await Native.TestClick(point); await Task.Delay(120); Assert(c.Popup.IsVisible,"native focus loss closed pinned result");
                            c.Popup.Pinned=false; c.Popup.Activate(); await Task.Delay(100); Assert(c.Popup.IsVisible,"unpin closed active result prematurely"); Native.TestFocus(hwnd); for(int i=0;i<10&&c.Popup.IsVisible;i++) await Task.Delay(50); Assert(!c.Popup.IsVisible,"programmatic focus switch did not close");
                            using(var server=new MockServer("200 OK","",true)) {
                                var settings=(Settings)typeof(AppController).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c); settings.BaseUrl=server.Url; c.SetConversationForTest("local streaming cancellation",""); var task=c.RequestAsync("translate",null);
                                for(int i=0;i<20&&server.RequestCount==0;i++) await Task.Delay(50); Assert(server.RequestCount==1&&c.Popup.IsVisible,"stream fixture did not begin"); Native.Place(c.Popup,new Point(1200,120)); await Task.Delay(60); Assert(Native.RootAt(point)==hwnd,"stream dismissal click missed external fixture"); await Native.TestClick(point); await task; Assert(!c.Popup.IsVisible&&c.RecentResults.Count==0,"dismissed stream reopened or stored partial result");
                            }
                        } finally { c.Dispose(); if(fixture!=null) { if(!fixture.HasExited) { fixture.CloseMainWindow(); if(!fixture.WaitForExit(500)) fixture.Kill(); } fixture.Dispose(); } if(File.Exists(info)) File.Delete(info); if(File.Exists(info+".bounds")) File.Delete(info+".bounds"); }
                    });
                } finally { Native.TestRestoreCursor(cursor); if(clipboardSaved) { try { if(oldClipboard!=null) Clipboard.SetDataObject(oldClipboard,true); else Clipboard.Clear(); } catch { } } report.AppendLine(string.Format("RESULT {0} passed, {1} failed",pass,fail)); File.WriteAllText(reportPath,report.ToString(),new UTF8Encoding(true)); app.Shutdown(fail==0?0:1); }
            })); return app.Run();
        }
    }
}
