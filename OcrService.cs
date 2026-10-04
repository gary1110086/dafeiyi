using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Windows.Media.Ocr;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Windows.Media.Imaging;
namespace LightTranslate {
    public class ScreenFrame {
        public byte[] Png; public System.Windows.Rect Bounds; public BitmapImage Image;
        public static ScreenFrame Capture() {
            var cursor=Native.Cursor(); var bounds=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)cursor.X,(int)cursor.Y)).Bounds;
            using(var bitmap=new System.Drawing.Bitmap(bounds.Width,bounds.Height)) using(var graphics=System.Drawing.Graphics.FromImage(bitmap)) using(var memory=new MemoryStream()) {
                graphics.CopyFromScreen(bounds.Left,bounds.Top,0,0,bounds.Size,System.Drawing.CopyPixelOperation.SourceCopy);
                bitmap.Save(memory,System.Drawing.Imaging.ImageFormat.Png); return FromPng(memory.ToArray(),new System.Windows.Rect(bounds.Left,bounds.Top,bounds.Width,bounds.Height));
            }
        }
        public static ScreenFrame FromPng(byte[] png,System.Windows.Rect bounds) {
            var image=new BitmapImage(); using(var memory=new MemoryStream(png)) { image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.StreamSource=memory; image.EndInit(); image.Freeze(); }
            return new ScreenFrame { Png=png,Bounds=bounds,Image=image };
        }
    }
    public static class OcrService {
        internal static async Task WaitAsync(Windows.Foundation.IAsyncInfo operation,CancellationToken token) {
            bool cancellationRequested=false;
            while(operation.Status==Windows.Foundation.AsyncStatus.Started) {
                if(token.IsCancellationRequested && !cancellationRequested) { cancellationRequested=true; try { operation.Cancel(); } catch { } }
                await Task.Delay(15).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            if(operation.Status==Windows.Foundation.AsyncStatus.Canceled) throw new OperationCanceledException(token);
            if(operation.Status==Windows.Foundation.AsyncStatus.Error) throw operation.ErrorCode ?? new InvalidOperationException("系统识字失败。");
        }
        public static string LanguageSummary() { try { return string.Join(" / ",OcrEngine.AvailableRecognizerLanguages.Select(x=>x.LanguageTag)); } catch { return "无法读取系统识字语言"; } }
        public static async Task<OcrDocument> RecognizeAsync(byte[] png,CancellationToken token) {
            token.ThrowIfCancellationRequested();
            var language=OcrEngine.AvailableRecognizerLanguages.FirstOrDefault(x=>x.LanguageTag.StartsWith("zh-Hans",StringComparison.OrdinalIgnoreCase)) ?? OcrEngine.AvailableRecognizerLanguages.FirstOrDefault(x=>x.LanguageTag.StartsWith("en",StringComparison.OrdinalIgnoreCase)) ?? OcrEngine.AvailableRecognizerLanguages.FirstOrDefault();
            if(language==null) throw new InvalidOperationException("系统没有可用的 OCR 语言。请在 Windows 设置 → 时间和语言 → 语言和区域中添加英文或简体中文语言的 OCR 功能。");
            var engine=OcrEngine.TryCreateFromLanguage(language); if(engine==null) throw new InvalidOperationException("无法启动系统识字引擎，请检查 Windows 语言功能。");
            using(var stream=new InMemoryRandomAccessStream()) {
                using(var writer=new DataWriter(stream)) {
                    writer.WriteBytes(png); var store=writer.StoreAsync();
                    try { await WaitAsync(store,token).ConfigureAwait(false); store.GetResults(); } finally { store.Close(); }
                    writer.DetachStream();
                }
                stream.Seek(0); var decode=Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream); Windows.Graphics.Imaging.BitmapDecoder decoder;
                try { await WaitAsync(decode,token).ConfigureAwait(false); decoder=decode.GetResults(); } finally { decode.Close(); }
                double ratio=Math.Max(1,Math.Max(decoder.PixelWidth,decoder.PixelHeight)/(double)OcrEngine.MaxImageDimension);
                var transform=new BitmapTransform { ScaledWidth=(uint)Math.Max(1,decoder.PixelWidth/ratio),ScaledHeight=(uint)Math.Max(1,decoder.PixelHeight/ratio) };
                var pixels=decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Ignore,transform,ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage); SoftwareBitmap image;
                try { await WaitAsync(pixels,token).ConfigureAwait(false); image=pixels.GetResults(); } finally { pixels.Close(); }
                using(var bitmap=image) {
                    var recognize=engine.RecognizeAsync(bitmap); OcrResult result;
                    try { await WaitAsync(recognize,token).ConfigureAwait(false); result=recognize.GetResults(); } finally { recognize.Close(); }
                    token.ThrowIfCancellationRequested();
                    var doc=new OcrDocument(); int line=0;
                    foreach(var row in result.Lines) { foreach(var word in row.Words) { var r=word.BoundingRect; doc.Words.Add(new RecognizedWord { Text=word.Text,Line=line,Bounds=new System.Windows.Rect(r.X*ratio,r.Y*ratio,r.Width*ratio,r.Height*ratio) }); } line++; }
                    return doc;
                }
            }
        }
    }
}
