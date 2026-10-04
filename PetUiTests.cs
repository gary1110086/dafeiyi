using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
namespace LightTranslate {
    internal static class PetUiTests {
        static int pass,fail; static StringBuilder report=new StringBuilder();
        static void Assert(bool condition,string message) { if(!condition) throw new Exception(message); }
        static async Task Check(string name,Func<Task> task) { try { await task(); report.AppendLine("PASS "+name); pass++; } catch(Exception e) { report.AppendLine("FAIL "+name+": "+e.Message); fail++; } }
        static void CaptureMenu(ContextMenu menu,string path) { menu.UpdateLayout(); var bitmap=new RenderTargetBitmap((int)System.Math.Ceiling(menu.ActualWidth),(int)System.Math.Ceiling(menu.ActualHeight),96,96,PixelFormats.Pbgra32); bitmap.Render(menu); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using(var stream=File.Create(path)) png.Save(stream); }
        public static int Run(string folder,Application app) {
            Directory.CreateDirectory(folder);
            app.Dispatcher.BeginInvoke(new Action(async delegate {
                var settings=new Settings { ApiKey="local-test-key" }; var controller=new AppController(settings,true);
                await Check("whale resident reflects actual reading state",async delegate {
                    controller.SetResident(true); await Task.Delay(80);
                    var property=typeof(ResidentOrb).GetProperty("CurrentAnimation");
                    if(property==null) throw new Exception("resident has no whale animation");
                    var status=typeof(ResidentOrb).GetMethod("SetStatus");
                    status.Invoke(controller.Orb,new object[]{"working","正在翻译",0});
                    if((string)property.GetValue(controller.Orb,null)!="working") throw new Exception("real work not reflected");
                    status.Invoke(controller.Orb,new object[]{"error","连接失败",0});
                    if((string)property.GetValue(controller.Orb,null)!="error") throw new Exception("real error not reflected");
                });
                await Check("all atlas modes decode and resident cache is bounded",async delegate {
                    foreach(var clip in PetAssets.Shared.Clips) { Assert(PetAssets.Shared.Image(clip.Key,0)!=null&&PetAssets.Shared.Image(clip.Key,clip.Value.frames.Count-1)!=null,"mode failed to decode: "+clip.Key); Assert(PetAssets.Shared.CachedSheetCount<=4,"atlas cache grew unbounded"); }
                    await Task.Delay(30);
                });
                await Check("missing atlas frame keeps a visible usable resident entry",async delegate {
                    var frame=PetAssets.Shared.Clips["idle"].frames[0]; string sheet=frame.sheet; ResidentOrb view=null;
                    try {
                        frame.sheet="missing-test-atlas.png"; view=new ResidentOrb();
                        var fallback=typeof(ResidentOrb).GetProperty("VisibleFallback",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                        Assert(fallback!=null&&(bool)fallback.GetValue(view,null),"missing PNG left an invisible desktop entry");
                        frame.sheet=sheet; view.SetStatus("working","正在翻译",0);
                        Assert(!(bool)fallback.GetValue(view,null),"valid next frame did not restore character"); await Task.Delay(20);
                    } finally { frame.sheet=sheet; if(view!=null) view.Close(); }
                });
                await Check("native head and tail touches play their original interactions",async delegate {
                    var pet=controller.Orb; pet.RestorePosition(new Settings { OrbX=410,OrbY=180 }); pet.SetStatus("idle","阅读就绪",0); await Task.Delay(100);
                    var head=pet.TouchPoint("head"); await Task.Run(async delegate { await Native.TestClick(head); }); await Task.Delay(60);
                    Assert(pet.CurrentAnimation=="head_pat"&&!pet.MenuOpen,"head touch did not play pat");
                    pet.SetReducedMotionForTest(true); pet.SetReducedMotionForTest(false); var tail=pet.TouchPoint("tail"); await Task.Run(async delegate { await Native.TestClick(tail); }); await Task.Delay(60);
                    Assert(pet.CurrentAnimation=="tail"&&!pet.MenuOpen,"tail touch did not play tail");
                    UiTests.Capture(pet,Path.Combine(folder,"大肥鱼娘-互动.png"));
                });
                await Check("preview stays labeled when it matches current base and real work changes",async delegate {
                    var pet=controller.Orb; pet.SetStatus("idle","阅读就绪",0); pet.Preview("idle","待机"); await Task.Delay(120); Assert(pet.Caption.Contains("动作预览"),"idle demo passed as actual idle");
                    pet.SetStatus("working","正在翻译",0); await Task.Delay(90); Assert(pet.CurrentState=="working"&&pet.Caption.Contains("动作预览"),"demo overwrote real state or lost its label"); pet.SetReducedMotionForTest(true); pet.SetReducedMotionForTest(false);
                });
                await Check("hidden pet stops rendering and reduced motion retains current expression",async delegate {
                    var pet=controller.Orb; pet.SetStatus("thinking","正在准备",0); pet.SetReducedMotionForTest(true); await Task.Delay(100); Assert(pet.CurrentAnimation=="thinking","reduced motion lost work expression");
                    pet.Suspend(); Assert(!pet.AnimationRunning,"hidden timer still rendering"); pet.RestorePosition(new Settings { OrbX=410,OrbY=180,ReducedMotion=true }); Assert(pet.AnimationRunning&&pet.CurrentState=="thinking","restore lost state"); pet.SetReducedMotionForTest(false);
                });
                await Check("real streaming, touch override, completion, cache and cancellation agree",async delegate {
                    string line="data: {\"choices\":[{\"delta\":{\"content\":\"翻译\"}}]}\n\n"; var body=new StringBuilder(); for(int i=0;i<8;i++) body.Append(line); body.Append("data: [DONE]\n\n");
                    using(var server=new MockServer("200 OK",body.ToString(),false)) {
                        settings.BaseUrl=server.Url; settings.Mode="button"; await controller.AcceptSelection("whale stream state",new Point(650,300)); var request=controller.RequestAsync("explain",null);
                        for(int i=0;i<40&&controller.Orb.CurrentState!="working";i++) await Task.Delay(40);
                        Assert(controller.Orb.CurrentState=="working","streaming not connected to work state"); controller.Orb.PlayInteraction("head_pat"); Assert(controller.Orb.CurrentAnimation=="head_pat"&&controller.Orb.CurrentState=="working","touch overwrote real work"); await request;
                        Assert(controller.Orb.CurrentState=="success","completion lost success: "+controller.Orb.CurrentState);
                        await controller.RequestAsync("explain",null); Assert(server.RequestCount==1&&controller.Orb.CurrentState=="success","cache sent request or lacks completion expression"); controller.Dismiss(); Assert(controller.Orb.CurrentState=="idle","cancel retained busy state");
                    }
                });
                await Check("API failure keeps error expression and OCR cancel returns ready",async delegate {
                    using(var server=new MockServer("401 Unauthorized","private server error",false)) { settings.BaseUrl=server.Url; await controller.AcceptSelection("whale failure state",new Point(650,300)); await controller.RequestAsync("translate",null); Assert(controller.Orb.CurrentState=="error","failed API pretended to finish"); }
                    controller.Dismiss(); controller.CaptureScreen=UiTests.OcrTestFrame; controller.Recognizer=delegate(byte[] png,System.Threading.CancellationToken token) { return Task.FromResult(UiTests.OcrTestDocument()); };
                    await controller.StartOcr(); Assert(!controller.Orb.IsVisible&&controller.Orb.CurrentState=="waiting","OCR did not hide pet or reach selection state"); controller.Dismiss(); Assert(controller.Orb.IsVisible&&controller.Orb.CurrentState=="idle","OCR cancel did not restore idle pet");
                });
                await Check("matching result, settings, companion and menu render",async delegate {
                    var popup=controller.Popup; popup.SetSource("Spin–orbit torque enables current-induced magnetization switching."); popup.SelectMode("explain"); popup.SetAnswer("## 自旋轨道力矩\n电流经过具有较强自旋轨道耦合的材料时，可以向相邻磁层传递角动量。\n\n**核心直觉**：电流不只是搬运电荷，也能帮助推动磁化方向。\n\n这就是 spin–orbit torque（SOT）在磁存储写入中的作用。"); popup.SetBusy(false,"解释完成 · 本地演示内容"); popup.Show(); Native.Place(popup,new Point(680,250)); await Task.Delay(100); UiTests.Capture(popup,Path.Combine(folder,"大肥鱼娘-阅读面板.png")); popup.Hide();
                    var view=new SettingsView(new Settings { ApiKey="" },delegate(Settings next) { }); view.Show(); await Task.Delay(100); UiTests.Capture(view,Path.Combine(folder,"大肥鱼娘-设置.png")); view.Close();
                    var pet=controller.Orb; pet.SetStatus("idle","阅读就绪",0); pet.SetReducedMotionForTest(true); pet.SetReducedMotionForTest(false); await Task.Delay(80); UiTests.Capture(pet,Path.Combine(folder,"大肥鱼娘-常驻.png"));
                    pet.OpenMenu(); await Task.Delay(120); CaptureMenu(pet.Menu,Path.Combine(folder,"大肥鱼娘-功能菜单.png")); pet.Suspend();
                });
                controller.Dispose();
                report.AppendLine(String.Format("RESULT {0} passed, {1} failed",pass,fail)); File.WriteAllText(Path.Combine(folder,"pet-test.txt"),report.ToString(),new UTF8Encoding(true)); app.Shutdown(fail==0?0:1);
            })); return app.Run();
        }
    }
}
