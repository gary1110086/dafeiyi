using System;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Windows;
using System.Windows.Media;
using CSharpMath.SkiaSharp;
using SkiaSharp;
namespace LightTranslate {
    internal static class ExtendedMath {
        internal static DrawingImage Render(string latex,bool display) {
            // CSharpMath names bold-italic math style "mathbfit"; boldsymbol has identical argument scope.
            latex=Regex.Replace(latex,@"\\boldsymbol\b",@"\mathbfit");
            var color=((SolidColorBrush)Ui.Brush("#303B50")).Color;
            var painter=new MathPainter { LaTeX=latex,FontSize=18,TextColor=new SKColor(color.R,color.G,color.B),DisplayErrorInline=false,LineStyle=display?CSharpMath.Atom.LineStyle.Display:CSharpMath.Atom.LineStyle.Text };
            if(painter.ErrorMessage!=null) throw new InvalidOperationException(painter.ErrorMessage);
            var size=painter.Measure();
            if(size.Width<=0||size.Height<=0||size.Width>20000||size.Height>5000) throw new InvalidOperationException("Invalid formula size");
            using(var stream=new SKDynamicMemoryWStream()) {
                using(var canvas=SKSvgCanvas.Create(new SKRect(0,0,size.Width+4,size.Height+4),stream)) painter.Draw(canvas,2,2-size.Y);
                using(var data=stream.DetachAsData()) {
                    var document=new XmlDocument { XmlResolver=null };
                    using(var reader=XmlReader.Create(new StringReader(System.Text.Encoding.UTF8.GetString(data.ToArray())),new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null })) document.Load(reader);
                    var drawing=Read(document.DocumentElement,Ui.Brush("#303B50")); if(drawing.Bounds.IsEmpty) throw new InvalidOperationException("Empty vector formula");
                    var image=new DrawingImage(drawing); image.Freeze(); return image;
                }
            }
        }
        static double Number(string value,double fallback) { double number; return double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number)?number:fallback; }
        static string Attr(XmlElement element,string name) {
            if(element.HasAttribute(name)) return element.GetAttribute(name);
            var match=Regex.Match(element.GetAttribute("style"),@"(?:^|;)\s*"+Regex.Escape(name)+@"\s*:\s*([^;]+)"); return match.Success?match.Groups[1].Value.Trim():"";
        }
        static Brush Paint(string value,Brush inherited) {
            if(value=="none") return null; if(value==""||value=="currentColor") return inherited;
            if(!Regex.IsMatch(value,@"^#[0-9a-fA-F]{3,8}$")) throw new InvalidOperationException("Unsupported vector paint");
            return (Brush)new BrushConverter().ConvertFromString(value);
        }
        static DrawingGroup Read(XmlElement element,Brush inherited) {
            var group=new DrawingGroup(); Brush fill=Paint(Attr(element,"fill"),inherited); var transform=Attr(element,"transform");
            if(transform!="") group.Transform=Transform(transform);
            System.Windows.Media.Geometry geometry=null;
            if(element.LocalName=="path") geometry=System.Windows.Media.Geometry.Parse("F1 "+element.GetAttribute("d"));
            if(element.LocalName=="rect") geometry=new RectangleGeometry(new Rect(Number(Attr(element,"x"),0),Number(Attr(element,"y"),0),Number(Attr(element,"width"),0),Number(Attr(element,"height"),0)));
            if(element.LocalName=="line") geometry=new LineGeometry(new Point(Number(Attr(element,"x1"),0),Number(Attr(element,"y1"),0)),new Point(Number(Attr(element,"x2"),0),Number(Attr(element,"y2"),0)));
            if(geometry!=null) {
                string stroke=Attr(element,"stroke"); Pen pen=stroke==""||stroke=="none"?null:new Pen(Paint(stroke,inherited),Number(Attr(element,"stroke-width"),1));
                group.Children.Add(new GeometryDrawing(fill,pen,geometry));
            }
            foreach(XmlNode child in element.ChildNodes) { var next=child as XmlElement; if(next!=null) { if(next.LocalName=="defs") continue; group.Children.Add(Read(next,fill)); } }
            return group;
        }
        static Transform Transform(string source) {
            var group=new TransformGroup();
            foreach(Match item in Regex.Matches(source,@"(matrix|translate|scale|rotate)\s*\(([^)]+)\)")) {
                var parts=Regex.Split(item.Groups[2].Value.Trim(),@"[\s,]+"); var v=Array.ConvertAll(parts,p=>Number(p,0)); Transform transform;
                switch(item.Groups[1].Value) {
                    case "matrix": if(v.Length!=6) throw new InvalidOperationException("Invalid vector matrix"); transform=new MatrixTransform(v[0],v[1],v[2],v[3],v[4],v[5]); break;
                    case "translate": transform=new TranslateTransform(v[0],v.Length>1?v[1]:0); break;
                    case "scale": transform=new ScaleTransform(v[0],v.Length>1?v[1]:v[0]); break;
                    default: transform=new RotateTransform(v[0],v.Length>2?v[1]:0,v.Length>2?v[2]:0); break;
                }
                group.Children.Insert(0,transform);
            }
            return group;
        }
    }
}
