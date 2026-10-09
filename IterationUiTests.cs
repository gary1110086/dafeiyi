using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace LightTranslate {
 internal static class IterationUiTests {
  static void Assert(bool value,string message) { if(!value) throw new Exception(message); }
  static IEnumerable<T> Nodes<T>(DependencyObject root) where T:DependencyObject { if(root is T) yield return (T)root; for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var n in Nodes<T>(VisualTreeHelper.GetChild(root,i))) yield return n; }
  internal static ScreenFrame Frame() {
   var pixels=new byte[240*180*4]; for(int y=0;y<180;y++) for(int x=0;x<240;x++) { int i=(y*240+x)*4; pixels[i]=(byte)(x<120?0:255); pixels[i+2]=(byte)(x<120?255:0); pixels[i+3]=255; }
   var bitmap=BitmapSource.Create(240,180,96,96,PixelFormats.Bgra32,null,pixels,960); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
   using(var stream=new MemoryStream()) { encoder.Save(stream); return ScreenFrame.FromPng(stream.ToArray(),new Rect(0,0,240,180)); }
  }
  internal static int Run(string folder,Application app) {
   Directory.CreateDirectory(folder); app.Dispatcher.BeginInvoke(new Action(async delegate {
    var log=new StringBuilder(); int passed=0,failed=0;
    Func<string,Func<Task>,Task> check=async delegate(string name,Func<Task> action) { try { await action(); passed++; log.AppendLine("PASS "+name); } catch(Exception e) { failed++; log.AppendLine("FAIL "+name+": "+e.Message); } };
    await check("verification cannot save a startup profile to the real user path",async delegate {
     byte[] before=File.Exists(Settings.DefaultPath)?File.ReadAllBytes(Settings.DefaultPath):null; bool rejected=false; try { new AppController(new Settings(),false); } catch(InvalidOperationException) { rejected=true; } Assert(rejected,"native test accepted default profile path");
     string isolated=Path.Combine(folder,"startup-profile.json"); var c=new AppController(new Settings { Mode="companion",ResidentOrb=false },false,null,null,isolated); try { Assert(File.Exists(isolated),"onboarding preference was not isolated"); if(before!=null) Assert(before.SequenceEqual(File.ReadAllBytes(Settings.DefaultPath)),"real profile changed"); await Task.Delay(1); } finally { c.Dispose(); foreach(Window w in Application.Current.Windows.Cast<Window>().ToArray()) if(w is WelcomeView) w.Close(); File.Delete(isolated); }
    });
    await check("crop contains only selected pixels and rejects outside region",async delegate {
     var frame=Frame(); var crop=ImageRequest.Crop(frame,new Rect(-10,30,60,40),"formula",""); Assert(crop.Width==50&&crop.Height==40,"crop was not clipped to selected area");
     var decoded=ScreenFrame.FromPng(crop.Png,new Rect()); var converted=new FormatConvertedBitmap(decoded.Image,PixelFormats.Bgra32,null,0); var bytes=new byte[50*40*4]; converted.CopyPixels(bytes,200,0); Assert(bytes[0]==0&&bytes[2]==255,"unselected blue half leaked");
     bool rejected=false; try { ImageRequest.Crop(frame,new Rect(400,400,30,30),"chart",""); } catch(ArgumentException) { rejected=true; } Assert(rejected,"outside region accepted"); await Task.Delay(1);
    });
    await check("image mode can submit a preview without OCR or automatic upload",async delegate {
     var frame=Frame(); frame.Bounds=new Rect(30,30,900,650); var overlay=new OcrOverlay(frame,null,true); ImageRequest sent=null;
     try { overlay.ImageSubmitted+=delegate(ImageRequest image) { sent=image; }; overlay.Show(); overlay.SetImageMode(true); overlay.SelectForTest(new Point(15,20),new Point(100,100),true); await Task.Delay(60); Assert(sent==null,"automatic mode uploaded without confirmation"); UiTests.Capture(overlay,Path.Combine(folder,"image-preview.png")); Nodes<Button>(overlay).First(x=>Convert.ToString(x.Content)=="解释公式").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(sent!=null&&sent.Action=="formula"&&sent.Width==85&&sent.Height==80,"formula mode required OCR or sent wrong region"); } finally { overlay.Close(); }
    });
    await check("failed website follow-up retries the exact question and never calls API",async delegate {
     using(var server=new MockServer("200 OK","data: [DONE]\n\n",false)) {
      var c=new AppController(new Settings { Service="web",ResidentOrb=false,BaseUrl=server.Url },true); int attempts=0; string previous="";
      c.WebStreamer=async delegate(Settings s,string input,string mode,List<Dictionary<string,string>> history,Action<string> chunk,System.Threading.CancellationToken token) { attempts++; if(attempts==1) throw new InvalidOperationException("fixture disconnect"); Assert(input==previous&&mode=="followup"&&history.Count==3,"retry lost question or original answer"); chunk("Recovered"); await Task.Delay(1); };
      try { c.SetConversationForTest("source","answer"); previous="What does the vertical axis mean?"; await c.RequestAsync("followup",previous); Assert(c.Popup.RecoveryVisible,"failure has no recovery controls"); Nodes<Button>(c.Popup).First(x=>Convert.ToString(x.Content)=="重试这次请求").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); for(int i=0;i<100&&c.Popup.AnswerText!="Recovered";i++) await Task.Delay(20); Assert(c.Popup.AnswerText=="Recovered"&&attempts==2&&server.RequestCount==0&&!c.Popup.RecoveryVisible,"retry failed or fell back to API"); } finally { c.Dispose(); }
     }
    });
    await check("image route keeps pixels on follow-up and clears them for a text selection",async delegate {
     var c=new AppController(new Settings { Service="web",ResidentOrb=false },true); int requests=0;
     c.WebStreamer=async delegate(Settings s,string input,string mode,List<Dictionary<string,string>> history,Action<string> chunk,System.Threading.CancellationToken token) { Assert((s.Image!=null)==(requests<2),"image lifetime crossed text selection"); requests++; chunk("Image answer"); await Task.Delay(1); };
     try { await c.SubmitImage(ImageRequest.Crop(Frame(),new Rect(0,0,100,100),"chart","What is shown?")); Assert(c.RecentResults.Count==1&&c.RecentResults[0].Source.StartsWith("截图"),"image history label missing"); await c.RequestAsync("followup","Why?"); await c.AcceptSelection("ordinary text",new Point(300,300)); await c.RequestAsync("translate",null); Assert(requests==3,"text transition was not exercised"); } finally { c.Dispose(); }
    });
    await check("website login commits website selection without discarding unrelated drafts",async delegate {
     Settings saved=null; var s=new Settings { ApiKey="test-kept-key",Mode="clipboard" }; var view=new SettingsView(s,delegate(Settings next) { saved=next; }); int opened=0;
     try { view.WebRequested+=delegate { opened++; }; view.Show(); Nodes<RadioButton>(view).First(x=>Convert.ToString(x.Content).StartsWith("官网账号")).IsChecked=true; Nodes<Button>(view).First(x=>Convert.ToString(x.Content)=="使用官网账号 / 登录").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(saved!=null&&saved.Service=="web"&&saved.ApiKey==s.ApiKey&&saved.Mode==s.Mode&&opened==1,"login did not select website or discarded profile"); await Task.Delay(1); } finally { view.Close(); }
    });
    await check("English interface preserves user source and answers while settings remain usable",async delegate {
     ProductLanguage.Interface="en"; var popup=new PopupView(); var settings=new SettingsView(new Settings { InterfaceLanguage="en",TargetLanguage="en",Service="web" },delegate {});
     try { popup.SetSource("翻译"); popup.SetAnswer("解释"); popup.Show(); settings.Show(); await Task.Delay(50); Assert(popup.SourceText=="翻译"&&popup.AnswerText=="解释","localization changed user content"); Assert(Nodes<Button>(settings).Any(x=>Convert.ToString(x.Content)=="Save & use"),"English controls absent"); settings.SelectSection("阅读外观"); await Task.Delay(50); UiTests.Capture(settings,Path.Combine(folder,"settings-english.png")); } finally { popup.Close(); settings.Close(); ProductLanguage.Interface="zh-CN"; }
    });
    await check("local onboarding needs selection and makes no network request",async delegate {
     var welcome=new WelcomeView(null,null); try { welcome.Show(); welcome.Translate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(welcome.Result.Text.Contains("先选"),"selection guidance missing"); welcome.Sample.SelectAll(); welcome.Translate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(welcome.Result.Text.Contains("晶格")&&welcome.Result.Text.Contains("未请求 AI"),"demo is misleading or unusable"); UiTests.Capture(welcome,Path.Combine(folder,"getting-started.png")); await Task.Delay(1); } finally { welcome.Close(); }
    });
    log.AppendLine(string.Format("RESULT {0} passed, {1} failed",passed,failed)); File.WriteAllText(Path.Combine(folder,"iteration-test.txt"),log.ToString(),new UTF8Encoding(true)); app.Shutdown(failed==0?0:1);
   })); return app.Run();
  }
 }
}
