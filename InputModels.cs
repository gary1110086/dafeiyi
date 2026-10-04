using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace LightTranslate {
    public static class CopyFallbackPolicy {
        static string Name(string name) { name=(name??"").Trim(); return name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)?name.Substring(0,name.Length-4):name; }
        public static bool Allows(Settings settings,string process) {
            if(!settings.CopyFallbackEnabled || string.IsNullOrWhiteSpace(process)) return false;
            return (settings.CopyApplications??"").Split(new[]{',',';','\r','\n','，'},StringSplitOptions.RemoveEmptyEntries).Any(item=>string.Equals(Name(item),Name(process),StringComparison.OrdinalIgnoreCase));
        }
    }
    public class RecognizedWord { public string Text; public Rect Bounds; public int Line; }
    public class OcrDocument {
        public List<RecognizedWord> Words=new List<RecognizedWord>();
        public static OcrDocument TestDocument() {
            return new OcrDocument { Words=new List<RecognizedWord> {
                new RecognizedWord { Text="Spin",Bounds=new Rect(0,10,40,20),Line=0 },new RecognizedWord { Text="orbit",Bounds=new Rect(55,10,40,20),Line=0 },
                new RecognizedWord { Text="torque",Bounds=new Rect(105,10,60,20),Line=0 },new RecognizedWord { Text="Switching",Bounds=new Rect(0,50,48,20),Line=1 },
                new RecognizedWord { Text="dynamics",Bounds=new Rect(60,50,70,20),Line=1 } } };
        }
        int At(Point point) {
            int best=-1; double distance=12;
            for(int i=0;i<Words.Count;i++) {
                var box=Words[i].Bounds; double dx=Math.Max(Math.Max(box.Left-point.X,point.X-box.Right),0),dy=Math.Max(Math.Max(box.Top-point.Y,point.Y-box.Bottom),0);
                double d=Math.Sqrt(dx*dx+dy*dy); if(d<=distance) { best=i; distance=d; if(d==0) break; }
            }
            return best;
        }
        public IList<int> Indices(Point first,Point last,bool rectangle) {
            var found=new List<int>();
            if(rectangle) {
                var region=new Rect(first,last);
                for(int i=0;i<Words.Count;i++) { var box=Words[i].Bounds; if(region.Contains(new Point(box.Left+box.Width/2,box.Top+box.Height/2))) found.Add(i); }
            } else {
                int start=At(first),end=At(last); if(start<0||end<0) return found;
                for(int i=Math.Min(start,end);i<=Math.Max(start,end);i++) found.Add(i);
            }
            return found;
        }
        static bool Han(char c) { return c>=0x3400 && c<=0x9fff; }
        public string Text(IEnumerable<int> indices) {
            var text=new StringBuilder(); int lastLine=-1;
            foreach(int index in indices) {
                var word=Words[index]; if(string.IsNullOrEmpty(word.Text)) continue;
                if(text.Length>0) { if(lastLine!=word.Line) text.Append('\n'); else if(!(Han(text[text.Length-1]) && Han(word.Text[0]))) text.Append(' '); }
                text.Append(word.Text); lastLine=word.Line;
            }
            return text.ToString();
        }
        public string Select(Point first,Point last,bool rectangle) { return Text(Indices(first,last,rectangle)); }
    }
}
