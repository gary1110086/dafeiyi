using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace LightTranslate {
 internal static class VisionLive {
  internal static ImageRequest Canary() {
   var visual=new DrawingVisual(); using(var draw=visual.RenderOpen()) {
    draw.DrawRectangle(Brushes.White,null,new Rect(0,0,320,190));
    draw.DrawEllipse(Brushes.Red,null,new Point(85,95),48,48);
    draw.DrawRectangle(Brushes.Blue,null,new Rect(195,47,96,96));
   }
   var bitmap=new RenderTargetBitmap(320,190,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
   var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
   using(var stream=new MemoryStream()) { png.Save(stream); return new ImageRequest(stream.ToArray(),320,190,"explain","请观察图片，只回答两个图形的形状和颜色。不要猜测。用中文回答。"); }
  }
  internal static int Run(string folder,string provider,Application app) {
   Directory.CreateDirectory(folder); app.Dispatcher.BeginInvoke(new Action(async delegate {
    var answer=new StringBuilder(); var web=new DeepSeekWebView(); int exit=1;
    try {
     var settings=Settings.Load(Settings.DefaultPath).Copy(); settings.Image=Canary(); settings.Model=settings.VisionModel; settings.Thinking=false;
     if(provider=="api") await new ApiClient().StreamAsync(settings,settings.Image.Prompt,"explain",null,delta=>answer.Append(delta),CancellationToken.None);
     else await web.StreamAsync(settings,settings.Image.Prompt,"explain",null,delta=>answer.Append(delta),CancellationToken.None);
     string text=answer.ToString();
     if(!text.Contains("红")||!text.Contains("圆")||!text.Contains("蓝")||!(text.Contains("方")||text.Contains("矩形"))) throw new Exception("Model did not identify the unlabelled colored shapes; image understanding is not verified.");
     File.WriteAllText(Path.Combine(folder,"vision-connect-test.txt"),"PASS "+provider+" recognized an unlabelled red circle and blue square from generated PNG pixels. No personal screenshot used.\n"); exit=0;
    } catch(Exception e) { File.WriteAllText(Path.Combine(folder,"vision-connect-test.txt"),"FAIL "+provider+": "+e.Message+"\n"); }
    finally { web.Shutdown(); app.Shutdown(exit); }
   })); return app.Run();
  }
 }
}
