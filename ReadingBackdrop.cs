using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;
using System.Collections.Generic;
using SkiaSharp;
namespace LightTranslate {
    internal sealed class ReadingBackdrop: Grid {
        static Dictionary<string,BitmapSource> cache=new Dictionary<string,BitmapSource>();
        readonly Image art=new Image { IsHitTestVisible=false };
        readonly Border veil=new Border { IsHitTestVisible=false };
        readonly Border edges=new Border { IsHitTestVisible=false };
        readonly Border focus=new Border { IsHitTestVisible=false,Background=new SolidColorBrush(Color.FromArgb(92,12,25,44)),Opacity=0 };
        internal string Theme { get; private set; }
        static Color Ink(byte alpha) { return Color.FromArgb(alpha,18,32,53); }
        internal ReadingBackdrop(FrameworkElement content) {
            Children.Add(art); Children.Add(veil); Children.Add(edges); Children.Add(focus);
            content.Margin=new Thickness(16); Children.Add(content);
            SizeChanged+=delegate { Clip=new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight),19,19); FitArtwork(); };
            Apply(new Settings());
        }
        internal static ReadingBackdrop Wrap(FrameworkElement content) { return new ReadingBackdrop(content); }
        internal double FocusOpacity { get { return focus.Opacity; } }
        internal void SetReadingLoad(string text,bool expanded) { double amount=Math.Max(0,Math.Min(1,((text??"").Length-220)/900.0)); focus.Opacity=amount; }
        internal static BitmapSource Load(string path) {
            if(string.IsNullOrWhiteSpace(path)) return null;
            BitmapSource saved; if(cache.TryGetValue(path,out saved)) return saved;
            if(!File.Exists(path)||new FileInfo(path).Length>20*1024*1024) throw new IOException("请选择不超过 20 MB 的图片。");
            using(var bitmap=SKBitmap.Decode(path)) {
                if(bitmap==null||bitmap.Width>10000||bitmap.Height>10000||(long)bitmap.Width*bitmap.Height>36000000) throw new IOException("图片无法读取或尺寸过大。");
                using(var image=SKImage.FromBitmap(bitmap)) using(var data=image.Encode(SKEncodedImageFormat.Png,100)) using(var stream=new MemoryStream(data.ToArray())) {
                    var result=new BitmapImage(); result.BeginInit(); result.CacheOption=BitmapCacheOption.OnLoad; result.StreamSource=stream; result.EndInit(); result.Freeze();
                    if(cache.Count>=3) cache.Clear(); cache[path]=result; return result;
                }
            }
        }
        internal static string Import(string path,string destinationFolder=null) {
            var image=Load(path); if(image==null) throw new IOException("请选择图片。");
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using(var memory=new MemoryStream()) {
                encoder.Save(memory); byte[] bytes=memory.ToArray(); string name;
                using(var hash=SHA256.Create()) name=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()+".png";
                string folder=destinationFolder??Path.Combine(Path.GetDirectoryName(Settings.DefaultPath),"backgrounds"); Directory.CreateDirectory(folder);
                string target=Path.Combine(folder,name); if(!File.Exists(target)) File.WriteAllBytes(target,bytes); return target;
            }
        }
        internal void Apply(Settings settings) {
            Theme=settings.BackgroundTheme;
            string file=Theme=="angry"?"angry-maid.webp":"thinking-maid.png";
            string path=Theme=="custom"?settings.BackgroundPath:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","Reading",file);
            try { art.Source=Theme=="none"?null:Load(path); } catch(Exception) { art.Source=null; }
            bool fitted=Theme=="angry"||Theme=="custom";
            art.Stretch=fitted?Stretch.Uniform:Stretch.UniformToFill;
            art.VerticalAlignment=fitted?VerticalAlignment.Bottom:VerticalAlignment.Stretch;
            FitArtwork();
            art.Opacity=Math.Max(30,Math.Min(100,settings.BackgroundStrength))/100.0;
            var reading=new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=new Point(1,0) };
            reading.GradientStops.Add(new GradientStop(Ink(fitted?(byte)224:(byte)239),0));
            reading.GradientStops.Add(new GradientStop(Ink(fitted?(byte)198:(byte)206),0.48));
            reading.GradientStops.Add(new GradientStop(Ink(fitted?(byte)168:(byte)157),1)); veil.Background=reading;
            var edge=new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=new Point(0,1) };
            edge.GradientStops.Add(new GradientStop(Ink(236),0)); edge.GradientStops.Add(new GradientStop(Ink(105),0.25));
            edge.GradientStops.Add(new GradientStop(Ink(0),0.52)); edge.GradientStops.Add(new GradientStop(Ink(fitted?(byte)20:(byte)75),0.80));
            edge.GradientStops.Add(new GradientStop(Ink(240),1)); edges.Background=edge;
        }
        void FitArtwork() { bool fitted=Theme=="angry"||Theme=="custom"; art.Margin=fitted?new Thickness(0,Math.Min(16,ActualHeight*0.06),0,Math.Min(65,ActualHeight*0.12)):new Thickness(0); }
    }
}
