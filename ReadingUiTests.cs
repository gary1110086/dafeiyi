using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
namespace LightTranslate {
    internal static class ReadingUiTests {
        static int pass,fail; static StringBuilder report=new StringBuilder(); static string reportPath;
        static void Assert(bool ok,string reason) { if(!ok) throw new Exception(reason); }
        static async Task Check(string name,Func<Task> action) { try { await action(); pass++; report.AppendLine("PASS "+name); } catch(Exception e) { fail++; report.AppendLine("FAIL "+name+": "+e); } File.WriteAllText(reportPath,report.ToString(),new UTF8Encoding(true)); }
        static IEnumerable<DependencyObject> Nodes(DependencyObject root) { yield return root; foreach(object child in LogicalTreeHelper.GetChildren(root)) { var node=child as DependencyObject; if(node!=null) foreach(var next in Nodes(node)) yield return next; } }
        static RichTextBox Answer(PopupView p) { return Nodes(p).OfType<RichTextBox>().First(); }
        static AppController Controller() { var c=new AppController(new Settings { Mode="button",ResidentOrb=false },true); c.Foreground=delegate { return new IntPtr(42); }; return c; }
        static async Task Tick(AppController c,int ms) { var end=DateTime.UtcNow.AddMilliseconds(ms); do { c.UpdateChipVisibility(); await Task.Delay(40); } while(DateTime.UtcNow<end); c.UpdateChipVisibility(); }
        const string Reading="## 晶格，是重复排列的骨架\n\n**Bravais lattice** 描述平移对称的点阵。具体放什么原子，由基元决定。\n\n| 术语 | 含义 |\n|---|---|\n| lattice | 点阵 |\n| basis | 基元 |\n\n晶格向量 $\\vec{R}=n_1\\vec{a}_1+n_2\\vec{a}_2$。\n\n> 晶体结构 = 晶格 + 基元\n\n个人理解可以保存在术语本，下次遇到时点击细下划线查看。";
        internal static int Run(string folder,Application app) {
            Directory.CreateDirectory(folder); reportPath=Path.Combine(folder,"reading-test.txt"); app.Dispatcher.BeginInvoke(new Action(async delegate {
                var cursor=Native.Cursor();
                try {
                    await Check("stationary selection expires within 2.6 seconds independently of pinned result",async delegate {
                        var c=Controller(); try { var point=new Point(620,300); Native.TestRestoreCursor(point); c.Popup.Pinned=true; c.Popup.Show(); await c.AcceptSelection("stationary selection",point); Assert(c.Chip.IsVisible,"chip not shown"); await Tick(c,2600); Assert(!c.Chip.IsVisible&&c.Popup.IsVisible,"stationary chip or pinned result lifecycle incorrect"); } finally { c.Dispose(); }
                    });
                    await Check("approaching protects chip then leaving gets a short grace",async delegate {
                        var c=Controller(); try { var point=new Point(620,300); Native.TestRestoreCursor(point); await c.AcceptSelection("hover selection",point); await Task.Delay(80); c.Chip.UpdateLayout(); var b=c.Chip.HoverBounds; Native.TestRestoreCursor(new Point(b.X+b.Width/2,b.Y+b.Height/2)); Assert(b.Contains(Native.Cursor())&&b.Width>0,"hover fixture did not reach laid-out chip"); await Tick(c,2600); Assert(c.Chip.IsVisible,"hovered chip timed out; start="+b+", now="+c.Chip.HoverBounds+", cursor="+Native.Cursor()); Native.TestRestoreCursor(new Point(point.X-60,point.Y)); c.UpdateChipVisibility(); await Tick(c,200); Assert(c.Chip.IsVisible,"leave had no grace"); await Tick(c,420); Assert(!c.Chip.IsVisible,"leave grace never ended"); } finally { c.Dispose(); }
                    });
                    await Check("move away and focus change dismiss without waiting for idle timeout",async delegate {
                        var c=Controller(); try { var point=new Point(620,300); Native.TestRestoreCursor(point); await c.AcceptSelection("far selection",point); Native.TestRestoreCursor(new Point(950,300)); await Tick(c,320); Assert(!c.Chip.IsVisible,"moving far did not dismiss"); await c.AcceptSelection("focus selection",point); c.Foreground=delegate { return new IntPtr(43); }; c.UpdateChipVisibility(); Assert(!c.Chip.IsVisible,"focus change did not dismiss"); } finally { c.Dispose(); }
                    });
                    await Check("native key wheel outside click close; chip button clicks remain usable",async delegate {
                        var fixture=new Window { Title="大肥译 reading input fixture",Width=600,Height=250,Left=200,Top=160,Content=new TextBox { Text="本地输入测试",Margin=new Thickness(30) } }; var c=Controller(); InputActionObserver observer=null;
                        try { fixture.Show(); fixture.Activate(); ((TextBox)fixture.Content).Focus(); await Task.Delay(120); var hwnd=new WindowInteropHelper(fixture).Handle; c.Foreground=Native.GetForegroundWindow; observer=new InputActionObserver(app.Dispatcher); Assert(observer.Ready,"native observation hooks unavailable"); observer.Acted+=delegate(long seq,int kind,Point p) { c.HandleChipAction(kind,p); }; var point=new Point(620,300);
                            await c.AcceptSelection("key selection",point); Native.TestKey(0x27); await Task.Delay(100); Assert(!c.Chip.IsVisible,"keyboard did not close");
                            await c.AcceptSelection("wheel selection",point); Native.TestRestoreCursor(((TextBox)fixture.Content).PointToScreen(new Point(20,20))); await Task.Delay(80); long beforeWheel=observer.Sequence; Native.TestWheel(); for(int i=0;i<10&&c.Chip.IsVisible;i++) await Task.Delay(50); Assert(!c.Chip.IsVisible,"wheel did not close; before="+beforeWheel+", after="+observer.Sequence);
                            await c.AcceptSelection("click selection",point); c.Popup.Pinned=true; c.Popup.Show(); await Native.TestClick(((TextBox)fixture.Content).PointToScreen(new Point(20,20))); await Task.Delay(100); Assert(!c.Chip.IsVisible&&c.Popup.IsVisible,"outside click did not independently close chip");
                            c.Popup.Hide(); await c.AcceptSelection("inside selection",point); var button=Nodes(c.Chip).OfType<Button>().First(); bool visibleOnDown=false; button.PreviewMouseLeftButtonDown+=delegate { visibleOnDown=c.Chip.IsVisible; }; await Native.TestClick(button.PointToScreen(new Point(15,15))); await Task.Delay(80); Assert(visibleOnDown&&Native.GetForegroundWindow()==hwnd,"native hook swallowed button click or chip stole focus");
                        } finally { if(observer!=null) observer.Dispose(); c.Dispose(); fixture.Close(); }
                    });
                    await Check("underline only local prose and source; code math and web links remain intact",async delegate {
                        var p=new PopupView(); try { p.SetTerms(new[]{new TermEntry { Id="a",Term="lattice",Explanation="点阵" }}); string source="A lattice, not lattices."; string raw="lattice and **lattice**; lattices. `lattice` [lattice](https://example.com) $lattice$\n\n```text\nlattice\n```"; p.SetSource(source); p.SetAnswer(raw); p.Show(); await Task.Delay(80); var links=Nodes(Answer(p).Document).OfType<Hyperlink>().ToList(); Assert(links.Count(x=>x.Tag is TermEntry)==2&&links.Count(x=>x.NavigateUri!=null)==1,"prose boundary, code or web links changed"); Assert(Nodes(p).OfType<TextBlock>().SelectMany(x=>Nodes(x).OfType<Hyperlink>()).Any(x=>x.Tag is TermEntry),"source lacks local term link"); Assert(Nodes(Answer(p).Document).OfType<Image>().Any(x=>Equals(x.Tag,"latex")),"math lost"); int clicks=0; p.TermClicked+=delegate { clicks++; }; links.First(x=>x.Tag is TermEntry).RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent)); Assert(clicks==1&&p.SourceText==source&&p.AnswerText==raw,"link changed canonical copy or failed"); } finally { p.Close(); }
                    });
                    await Check("Aa updates controller preference, scales tables math, and long reading shade",async delegate {
                        var settings=new Settings { Mode="button",ResidentOrb=false }; var c=new AppController(settings,true); try { c.Popup.SetTerms(new[]{new TermEntry { Id="demo",Term="lattice",Explanation="点阵" }}); c.Popup.SetSource("A Bravais lattice with a basis."); c.Popup.SetAnswer(Reading); c.Popup.Show(); await Task.Delay(80); Assert(Nodes(Answer(c.Popup).Document).OfType<Hyperlink>().Count(x=>x.Tag is TermEntry)>=2,"table or emphasized prose lost term links"); double shade=c.Popup.ReadingShade; var before=Nodes(Answer(c.Popup).Document).OfType<Image>().First(x=>Equals(x.Tag,"latex")).Height;
                            c.Popup.SetReadingStyle(18,1.9); Assert(settings.ReadingFontSize==18&&Math.Abs(settings.ReadingLineSpacing-1.9)<0.01,"Aa preference not propagated"); Assert(Answer(c.Popup).Document.FontSize==18,"body size ignored"); Assert(Nodes(Answer(c.Popup).Document).OfType<Table>().Any(),"table disappeared"); Assert(Nodes(Answer(c.Popup).Document).OfType<Image>().First(x=>Equals(x.Tag,"latex")).Height>before,"math did not scale"); c.Popup.ToggleExpanded(); UiTests.Capture(c.Popup,Path.Combine(folder,"阅读精修-舒适字号.png"));
                            c.Popup.SetAnswer(Reading+"\n\n"+string.Join("\n\n",Enumerable.Repeat("更长的阅读段落，文字区域会加深遮罩。肥鱼背景继续柔和地透出来。",30))); Assert(c.Popup.ReadingShade>shade,"long text did not strengthen shading"); await Task.Delay(80); UiTests.Capture(c.Popup,Path.Combine(folder,"阅读精修-长文遮罩.png"));
                        } finally { c.Dispose(); }
                    });
                    await Check("collection previews searchable preferred wording and separated reading cards",async delegate {
                        var store=new TermStore(null); var e=store.SaveTermWithPreference("","lattice",Reading,"A lattice is not the full crystal structure.","注意：点阵 + 基元才是晶体结构。","点阵"); Assert(store.Search("点阵").Count==1&&e.Preview=="点阵","preference not searchable or preview missing"); var book=new TermBook(store,new Settings { ReadingFontSize=16 }); try { book.Show(); await Task.Delay(100); var text=new TextRange(Nodes(book).OfType<RichTextBox>().First().Document.ContentStart,Nodes(book).OfType<RichTextBox>().First().Document.ContentEnd).Text; Assert(new[]{"我的常用译法","收藏解释","我的笔记","原文上下文"}.All(text.Contains),"reading cards missing"); Assert(Nodes(book).OfType<RichTextBox>().First().Document.Blocks.OfType<Section>().Count()==4,"fields not separated"); UiTests.Capture(book,Path.Combine(folder,"术语助手-分层与预览.png")); } finally { book.Close(); }
                    });
                    await Check("local peek costs zero calls; matching preferences sent without notes; edits invalidate cache",async delegate {
                        string path=Path.Combine(folder,Guid.NewGuid().ToString("N")+".dat"); var store=new TermStore(path); var e=store.SaveTermWithPreference("","lattice","私有收藏解释", "private context", "private personal note", "点阵"); var settings=new Settings { Mode="button",ApiKey="local-test-key",ResidentOrb=false }; var c=new AppController(settings,true,null,path);
                        try { using(var server=new MockServer("200 OK","data: {\"choices\":[{\"delta\":{\"content\":\"lattice 的本地模拟解释\"}}]}\n\ndata: [DONE]\n\n",false,"text/event-stream",false,3)) {
                            settings.BaseUrl=server.Url; await c.AcceptSelection("A lattice with a basis.",new Point(620,300)); await c.RequestAsync("translate",null); Assert(server.RequestCount==1&&server.Request.Contains("preferred_translation")&&!server.Request.Contains("private personal note")&&!server.Request.Contains("私有收藏解释"),"private fields leaked or reference missing"); var link=Nodes(Answer(c.Popup).Document).OfType<Hyperlink>().First(x=>x.Tag is TermEntry); link.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent)); await Task.Delay(120); var peek=app.Windows.OfType<TermPeek>().FirstOrDefault(); Assert(peek!=null&&peek.IsVisible&&server.RequestCount==1,"peek missing or made API call"); UiTests.Capture(peek,Path.Combine(folder,"术语助手-本地卡片.png")); peek.Close();
                            await c.RequestAsync("translate",null); Assert(server.RequestCount==1,"identical term preferences missed cache"); var controllerStore=(TermStore)typeof(AppController).GetField("terms",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(c); controllerStore.SaveTermWithPreference(e.Id,e.Term,e.Explanation,e.Context,e.Note,"晶格"); await c.RequestAsync("translate",null); Assert(server.RequestCount==2,"edited preferences reused stale cache"); settings.UseTermPreferences=false; await c.RequestAsync("translate",null); Assert(server.RequestCount==3&&!server.Request.Contains("preferred_translation"),"preference toggle ignored");
                        } } finally { c.Dispose(); File.Delete(path); }
                    });
                } finally { Native.TestRestoreCursor(cursor); report.AppendLine(string.Format("RESULT {0} passed, {1} failed",pass,fail)); File.WriteAllText(Path.Combine(folder,"reading-test.txt"),report.ToString(),new UTF8Encoding(true)); app.Shutdown(fail==0?0:1); }
            })); return app.Run();
        }
    }
}
