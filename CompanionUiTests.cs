using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.InteropServices;
namespace LightTranslate {
    internal static class CompanionUiTests {
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
        static int pass,fail; static StringBuilder report=new StringBuilder();
        static void Assert(bool ok,string reason) { if(!ok) throw new Exception(reason); }
        static async Task Check(string name,Func<Task> action) { try { await action(); pass++; report.AppendLine("PASS "+name); } catch(Exception e) { fail++; report.AppendLine("FAIL "+name+": "+e); } }
        internal static int Run(string folder,Application app) {
            Directory.CreateDirectory(folder);
            app.Dispatcher.BeginInvoke(new Action(async delegate {
                Point cursor=Native.Cursor();
                await Check("term book renders fields, Markdown, math, notes and searchable empty state",async delegate {
                    var store=new TermStore(null); store.SaveTerm("","lattice","## 晶格\n\n**Bravais lattice**：平移对称的点阵。\n\n| 术语 | 含义 |\n| --- | --- |\n| lattice | 点阵 |\n| basis | 基元 |\n\n\\(\\mathbf{R}=n_1\\mathbf{a}_1+n_2\\mathbf{a}_2\\)","The crystal is a lattice with a basis.","注意：晶体结构 = 点阵 + 基元。");
                    var book=new TermBook(store); try { book.Show(); await Task.Delay(200); Assert(book.VisibleCount==1&&book.Selection.Term=="lattice","term selection missing"); UiTests.Capture(book,Path.Combine(folder,"术语收藏-阅读.png")); var search=Find<TextBox>(book); search.Text="BASIS"; await Task.Delay(80); Assert(book.VisibleCount==1,"notes/context search failed"); search.Text="不存在"; await Task.Delay(80); Assert(book.VisibleCount==0,"empty search reused previous term"); UiTests.Capture(book,Path.Combine(folder,"术语收藏-空搜索.png")); } finally { book.Close(); }
                    var editor=new TermEditor(store,new TermEntry { Term="spin–orbit torque",Explanation="自旋轨道力矩",Context="current-induced switching" }); try { editor.Show(); await Task.Delay(100); UiTests.Capture(editor,Path.Combine(folder,"术语收藏-编辑.png")); } finally { editor.Close(); }
                });
                await Check("collection entry opens editor without API",async delegate {
                    var controller=new AppController(new Settings { Mode="companion" },true); try {
                        controller.Popup.SetSource("lattice"); controller.Popup.SetAnswer("**晶格**"); controller.CollectTerm(); await Task.Delay(80);
                        var editor=app.Windows.OfType<TermEditor>().First(); editor.Save(); await Task.Delay(50); controller.OpenTerms(); await Task.Delay(80);
                        var book=app.Windows.OfType<TermBook>().First(); Assert(book.VisibleCount==1,"favorite action did not persist in controller"); UiTests.Capture(book,Path.Combine(folder,"术语收藏-首次.png"));
                    } finally { controller.Dispose(); }
                });
                await Check("left edge peeks, hover reveals continuously, preserves position and never activates",async delegate {
                    var pet=new ResidentOrb(); try {
                        var work=System.Windows.Forms.Screen.PrimaryScreen.WorkingArea; SetCursorPos(work.Left+work.Width/2,work.Top+50);
                        pet.RestorePosition(new Settings { Mode="companion",OrbX=work.Left,OrbY=work.Top+200,EdgeHide=true }); pet.SetStatus("idle","陪伴模式 · 划词已关闭",0);
                        await Task.Delay(2800); Assert(pet.DockSide=="left"&&pet.EdgeProgress==0,"left did not tuck"); Assert(Native.Bounds(pet).Width<100*VisualTreeHelper.GetDpi(pet).DpiScaleX,"large invisible window remained"); UiTests.Capture(pet,Path.Combine(folder,"贴边-左探头.png"));
                        var saved=pet.SavedPosition; var foreground=Native.GetForegroundWindow(); var b=Native.Bounds(pet); SetCursorPos((int)b.X+25,(int)b.Y+45); await Task.Delay(260); Assert(pet.EdgeProgress>0&&pet.EdgeProgress<1,"hover snapped without sequence"); UiTests.Capture(pet,Path.Combine(folder,"贴边-出来中.png"));
                        await Task.Delay(650); Assert(pet.EdgeProgress==1,"hover did not fully reveal"); Assert(Native.GetForegroundWindow()==foreground,"pet stole focus"); UiTests.Capture(pet,Path.Combine(folder,"贴边-左出来.png")); Assert(pet.SavedPosition==saved,"animation overwrote dock position");
                        pet.OpenMenu(); SetCursorPos(work.Left+work.Width/2,work.Top+50); await Task.Delay(2300); Assert(pet.EdgeProgress==1,"menu collapsed mid interaction"); pet.Menu.IsOpen=false; await Task.Delay(2600); Assert(pet.EdgeProgress==0,"leave did not tuck after grace");
                    } finally { pet.Close(); }
                });
                await Check("right edge mirrors pose, reduced motion settles, work keeps pet open",async delegate {
                    var pet=new ResidentOrb(); try {
                        var work=System.Windows.Forms.Screen.PrimaryScreen.WorkingArea; SetCursorPos(work.Left+work.Width/2,work.Top+50);
                        pet.RestorePosition(new Settings { Mode="companion",OrbX=work.Right-176,OrbY=work.Top+250,EdgeHide=true,ReducedMotion=true }); pet.SetStatus("idle","陪伴模式",0); await Task.Delay(2300);
                        Assert(pet.DockSide=="right"&&pet.EdgeProgress==0,"right not tucked"); UiTests.Capture(pet,Path.Combine(folder,"贴边-右探头.png")); Assert(Math.Abs(Native.Bounds(pet).Right-work.Right)<=1,"right alignment drift");
                        pet.SetStatus("working","正在翻译",0); await Task.Delay(300); Assert(pet.EdgeProgress==1&&pet.CurrentState=="working","work hidden or expression overwritten");
                        pet.ApplyPreferences(new Settings { EdgeHide=false,ReducedMotion=true }); Assert(pet.DockSide==""&&pet.Width==176,"cannot disable edge hiding");
                    } finally { pet.Close(); }
                });
                await Check("right return plays reverse frames and moves continuously toward the right edge",async delegate {
                    var pet=new ResidentOrb(); try {
                        var work=System.Windows.Forms.Screen.PrimaryScreen.WorkingArea; SetCursorPos(work.Left+work.Width/2,work.Top+50);
                        pet.RestorePosition(new Settings { Mode="companion",OrbX=work.Right-176,OrbY=work.Top+280,ReducedMotion=false }); pet.SetStatus("idle","陪伴模式",0); await Task.Delay(2800);
                        var b=Native.Bounds(pet); SetCursorPos((int)b.Right-25,(int)b.Y+45); await Task.Delay(850); Assert(pet.EdgeProgress==1,"right hover did not open"); double openedX=Native.Bounds(pet).X; UiTests.Capture(pet,Path.Combine(folder,"右侧收回-1展开.png"));
                        SetCursorPos(work.Left+work.Width/2,work.Top+50); await Task.Delay(1930); double mid=pet.EdgeProgress,midX=Native.Bounds(pet).X; Assert(mid>0&&mid<1,"right return skipped continuous animation"); Assert(midX>openedX,"right return did not move right"); UiTests.Capture(pet,Path.Combine(folder,"右侧收回-2退回中.png"));
                        await Task.Delay(210); Assert(pet.EdgeProgress<mid&&pet.EdgeProgress>0,"right return did not reverse frames"); UiTests.Capture(pet,Path.Combine(folder,"右侧收回-3扶边.png")); await Task.Delay(500); Assert(pet.EdgeProgress==0&&Native.Bounds(pet).X>midX,"right return failed to settle at the edge"); UiTests.Capture(pet,Path.Combine(folder,"右侧收回-4探头.png"));
                    } finally { pet.Close(); }
                });
                await Check("unified menu icons and display submenu expose all original actions",async delegate {
                        var pet=new ResidentOrb(); try { pet.RestorePosition(new Settings { OrbX=420,OrbY=200,HideIdleCaption=false }); pet.OpenMenu(); await Task.Delay(130); Assert(pet.Menu.Items.OfType<MenuItem>().All(x=>x.Icon!=null),"top-level action has no icon"); var display=pet.Menu.Items.OfType<MenuItem>().First(x=>(string)x.Header=="显示与收起"); Assert(display.Items.OfType<MenuItem>().Count()==4,"display preferences missing"); var play=pet.Menu.Items.OfType<MenuItem>().First(x=>(string)x.Header=="和肥鱼玩"); var previews=play.Items.OfType<MenuItem>().First(x=>(string)x.Header=="动作预览"); Assert(previews.Items.Count==16,"old motions dropped"); CaptureMenu(pet.Menu,Path.Combine(folder,"肥鱼菜单-统一.png")); }
                    finally { pet.Close(); }
                });
                SetCursorPos((int)cursor.X,(int)cursor.Y); report.AppendLine(string.Format("RESULT {0} passed, {1} failed",pass,fail)); File.WriteAllText(Path.Combine(folder,"companion-test.txt"),report.ToString(),new UTF8Encoding(true)); app.Shutdown(fail==0?0:1);
            })); return app.Run();
        }
        static T Find<T>(DependencyObject p) where T:DependencyObject { if(p is T) return (T)p; for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++) { var found=Find<T>(VisualTreeHelper.GetChild(p,i)); if(found!=null) return found; } return null; }
        static void CaptureMenu(ContextMenu menu,string path) { menu.UpdateLayout(); var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth),(int)Math.Ceiling(menu.ActualHeight),96,96,PixelFormats.Pbgra32); bitmap.Render(menu); var png=new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); using(var stream=File.Create(path)) png.Save(stream); }
    }
}
