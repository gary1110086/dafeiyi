using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
namespace LightTranslate {
    public class EdgeReveal {
        public double Progress { get; private set; }
        int quiet;
        public void Tick(int milliseconds,bool near,bool locked,bool reduced) {
            int elapsed=Math.Max(0,milliseconds); bool visible=near||locked;
            if(visible) quiet=0; else quiet=Math.Min(10000,quiet+elapsed);
            bool reveal=visible||quiet<1800;
            // Initial state is tucked away; the grace period only applies after approaching.
            if(!visible&&Progress==0) return;
            if(reduced) { Progress=reveal?1:0; return; }
            double step=Math.Min(1,elapsed/640.0); Progress=Math.Max(0,Math.Min(1,Progress+(reveal?step:-step)));
        }
        public void Open() { Progress=1; quiet=0; }
        public void Reset() { Progress=0; quiet=1800; }
    }
    internal static class EdgeArt {
        static BitmapSource[] frames;
        internal static BitmapSource Frame(int index) {
            if(frames==null) {
                frames=new BitmapSource[8];
                try {
                    var sheet=new BitmapImage(); sheet.BeginInit(); sheet.CacheOption=BitmapCacheOption.OnLoad; sheet.UriSource=new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","Reading","edge-reveal.png")); sheet.EndInit(); sheet.Freeze();
                    int w=sheet.PixelWidth/4,h=sheet.PixelHeight/2;
                    for(int i=0;i<8;i++) { var cell=new CroppedBitmap(sheet,new Int32Rect((i%4)*w,(i/4)*h,w,h)); cell.Freeze(); frames[i]=cell; }
                } catch(IOException) { } catch(NotSupportedException) { }
            }
            return frames[Math.Max(0,Math.Min(7,index))];
        }
    }
}
