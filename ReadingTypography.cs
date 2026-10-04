using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace LightTranslate {
    internal static class ReadingTypography {
        internal static FlowDocument Render(string raw,double width,Settings settings) { var document=MarkdownView.Render(raw,width); Apply(document,settings,width); return document; }
        internal static void Apply(FlowDocument document,Settings settings,double width) {
            double font=Math.Max(12,Math.Min(20,settings.ReadingFontSize)),spacing=Math.Max(1.4,Math.Min(2.0,settings.ReadingLineSpacing)); document.FontSize=font; document.LineHeight=font*spacing;
            foreach(var block in document.Blocks) Scale(block,font/14.0,spacing/(23.0/14.0),width);
        }
        static void Scale(TextElement element,double ratio,double spacing,double width) {
            var font=element.ReadLocalValue(TextElement.FontSizeProperty); if(font!=DependencyProperty.UnsetValue) element.FontSize=(double)font*ratio;
            var block=element as Block; if(block!=null) { var line=block.ReadLocalValue(Block.LineHeightProperty); if(line!=DependencyProperty.UnsetValue&&!double.IsNaN((double)line)) block.LineHeight=(double)line*ratio*spacing; var margin=block.Margin; block.Margin=new Thickness(margin.Left,margin.Top*ratio,margin.Right,margin.Bottom*ratio); }
            var paragraph=element as Paragraph; if(paragraph!=null) { foreach(var i in paragraph.Inlines) Scale(i,ratio,spacing,width); }
            var span=element as Span; if(span!=null) foreach(var i in span.Inlines) Scale(i,ratio,spacing,width);
            var section=element as Section; if(section!=null) foreach(var b in section.Blocks) Scale(b,ratio,spacing,width);
            var list=element as System.Windows.Documents.List; if(list!=null) foreach(var item in list.ListItems) foreach(var b in item.Blocks) Scale(b,ratio,spacing,width-22);
            var table=element as Table; if(table!=null) foreach(var group in table.RowGroups) foreach(var row in group.Rows) foreach(var cell in row.Cells) foreach(var b in cell.Blocks) Scale(b,ratio,spacing,width/Math.Max(1,table.Columns.Count));
            var ui=element as InlineUIContainer; if(ui!=null) { var image=ui.Child as Image; if(image!=null&&Equals(image.Tag,"latex")) { double scale=Math.Min(ratio,width/Math.Max(1,image.Width)); image.Width*=scale; image.Height*=scale; } }
            var display=element as BlockUIContainer; if(display!=null) { var scroll=display.Child as ScrollViewer; var image=scroll==null?null:scroll.Content as Image; if(image!=null&&Equals(image.Tag,"latex")) { image.Width*=ratio; image.Height*=ratio; } }
        }
        internal static Section Card(string label,string raw,double width,Settings settings,string kind) {
            var section=new Section { Margin=new Thickness(0,0,0,14),Padding=new Thickness(12,10,12,4),Background=Ui.Brush(kind=="note"?"#213C55":"#101E32"),BorderBrush=Ui.Brush(kind=="note"?"#6396A6":"#344A63"),BorderThickness=new Thickness(kind=="note"?2:1,0,0,0) };
            section.Blocks.Add(new Paragraph(new Run(label)) { FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=Ui.Brush("#8CD3DF"),Margin=new Thickness(0,0,0,8),LineHeight=18 });
            var parsed=Render(raw,width-24,settings); while(parsed.Blocks.Count>0) { var block=parsed.Blocks.FirstBlock; parsed.Blocks.Remove(block); section.Blocks.Add(block); } return section;
        }
    }
}
