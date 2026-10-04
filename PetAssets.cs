using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LightTranslate {
    public class PetFrame { public string sheet; public int cell; }
    public class PetClip { public int frameMs=42; public bool loop=true; public List<PetFrame> frames=new List<PetFrame>(); }
    public class PetManifest { public int width,height,columns,rows; public Dictionary<string,PetClip> clips=new Dictionary<string,PetClip>(); }
    internal sealed class PetAssets {
        internal static readonly PetAssets Shared=new PetAssets();
        readonly string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","Whale");
        readonly Dictionary<string,BitmapSource> sheets=new Dictionary<string,BitmapSource>();
        readonly LinkedList<string> recent=new LinkedList<string>();
        readonly PetManifest manifest;
        internal bool Available { get; private set; }
        internal Dictionary<string,PetClip> Clips { get { return manifest.clips; } }
        internal int CachedSheetCount { get { return sheets.Count; } }
        PetAssets() {
            try {
                manifest=new JavaScriptSerializer { MaxJsonLength=1024*1024 }.Deserialize<PetManifest>(File.ReadAllText(Path.Combine(folder,"manifest.json")));
                if(manifest.width<1||manifest.height<1||manifest.columns<1||manifest.clips.Count!=16) throw new IOException("Invalid whale assets");
                Available=true;
            } catch { manifest=new PetManifest(); manifest.clips.Add("idle",new PetClip()); Available=false; }
        }
        internal BitmapSource Image(string action,int index) {
            PetClip clip; if(!Available||!Clips.TryGetValue(action,out clip)||clip.frames.Count==0) return null;
            var frame=clip.frames[Math.Max(0,Math.Min(index,clip.frames.Count-1))]; BitmapSource sheet;
            try {
                if(!sheets.TryGetValue(frame.sheet,out sheet)) {
                    var path=Path.GetFullPath(Path.Combine(folder,frame.sheet));
                    if(!path.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) return null;
                    using(var stream=File.OpenRead(path)) { var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad; bitmap.StreamSource=stream; bitmap.EndInit(); bitmap.Freeze(); sheet=bitmap; }
                    sheets[frame.sheet]=sheet;
                }
                recent.Remove(frame.sheet); recent.AddFirst(frame.sheet);
                while(recent.Count>4) { string last=recent.Last.Value; recent.RemoveLast(); sheets.Remove(last); }
                var crop=new CroppedBitmap(sheet,new Int32Rect((frame.cell%manifest.columns)*manifest.width,(frame.cell/manifest.columns)*manifest.height,manifest.width,manifest.height)); crop.Freeze(); return crop;
            } catch { return null; }
        }
    }
}
