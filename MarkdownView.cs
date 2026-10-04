using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using M=Markdig.Syntax;
using MI=Markdig.Syntax.Inlines;
using MT=Markdig.Extensions.Tables;
using MM=Markdig.Extensions.Mathematics;
using WpfMath;
using WpfMath.Parsers;
using WpfMath.Rendering;
using XamlMath;

namespace LightTranslate {
    internal static class MarkdownView {
        static readonly MarkdownPipeline pipeline=BuildPipeline();
        static MarkdownPipeline BuildPipeline() {
            var builder=new MarkdownPipelineBuilder().DisableHtml().UsePipeTables().UseEmphasisExtras().UseMathematics().UseAutoLinks();
            builder.InlineParsers.Insert(0,new DelimitedMathParser()); return builder.Build();
        }
        static readonly Dictionary<string,DrawingImage> mathCache=new Dictionary<string,DrawingImage>();
        static readonly LinkedList<string> recentMath=new LinkedList<string>();
        internal static int MathCacheCount { get { return mathCache.Count; } }
        internal static FlowDocument Render(string raw,double width) {
            var document=new FlowDocument { PagePadding=new Thickness(0),FontSize=14,FontFamily=new FontFamily("Microsoft YaHei UI, Segoe UI"),Foreground=Ui.Brush("#303B50"),LineHeight=23 };
            try { AddBlocks(document.Blocks,Markdown.Parse(raw??"",pipeline),Math.Max(200,width)); }
            catch { document.Blocks.Clear(); document.Blocks.Add(new Paragraph(new Run(raw??""))); }
            return document;
        }
        static Paragraph Para() { return new Paragraph { Margin=new Thickness(0,0,0,9) }; }
        static void AddBlocks(BlockCollection target,M.ContainerBlock blocks,double width) {
            foreach(var block in blocks) {
                var math=block as MM.MathBlock;
                if(math!=null) { AddDisplayMath(target,math.Lines.ToString(),width); continue; }
                var table=block as MT.Table;
                if(table!=null) { AddTable(target,table,width); continue; }
                var list=block as M.ListBlock;
                if(list!=null) {
                    int start; if(!int.TryParse(list.OrderedStart,out start)||start<1) start=1;
                    var view=new System.Windows.Documents.List { MarkerStyle=list.IsOrdered?TextMarkerStyle.Decimal:TextMarkerStyle.Disc,StartIndex=start,Margin=new Thickness(0,2,0,10),Padding=new Thickness(20,0,0,0) };
                    foreach(var child in list) { var item=new ListItem(); AddBlocks(item.Blocks,(M.ContainerBlock)child,width-22); view.ListItems.Add(item); } target.Add(view); continue;
                }
                var quote=block as M.QuoteBlock;
                if(quote!=null) { var section=new Section { BorderBrush=Ui.Brush("#5864D8"),BorderThickness=new Thickness(3,0,0,0),Padding=new Thickness(10,6,8,0),Margin=new Thickness(0,2,0,12),Background=Ui.Brush("#F4F6FA") }; AddBlocks(section.Blocks,quote,width-24); target.Add(section); continue; }
                var code=block as M.CodeBlock;
                if(code!=null) { var paragraph=Para(); paragraph.FontFamily=new FontFamily("Consolas"); paragraph.FontSize=12; paragraph.LineHeight=19; paragraph.Background=Ui.Brush("#F4F6FA"); paragraph.Padding=new Thickness(9,8,9,8); paragraph.Inlines.Add(new Run(code.Lines.ToString())); target.Add(paragraph); continue; }
                var leaf=block as M.LeafBlock;
                if(leaf!=null&&leaf.Inline!=null) {
                    var paragraph=Para(); var heading=block as M.HeadingBlock;
                    if(heading!=null) { paragraph.FontWeight=FontWeights.SemiBold; paragraph.FontSize=heading.Level<=2?18:16; paragraph.LineHeight=paragraph.FontSize+9; paragraph.Margin=new Thickness(0,9,0,10); }
                    for(var inline=leaf.Inline.FirstChild;inline!=null;inline=inline.NextSibling) {
                        var display=inline as MM.MathInline;
                        if(display!=null&&display.DelimiterCount==2) {
                            if(paragraph.Inlines.Count>0) target.Add(paragraph);
                            AddDisplayMath(target,display.Content.ToString(),width,display.Delimiter=='\\'?@"\[":"$$",display.Delimiter=='\\'?@"\]":"$$"); paragraph=Para();
                        } else AddInlines(paragraph.Inlines,leaf.Inline,width,inline);
                    }
                    if(paragraph.Inlines.Count>0) target.Add(paragraph); continue;
                }
                if(block is M.ThematicBreakBlock) { target.Add(new Paragraph { BorderBrush=Ui.Brush("#E5E9F2"),BorderThickness=new Thickness(0,0,0,1),Margin=new Thickness(0,6,0,12),FontSize=1,LineHeight=1 }); continue; }
                var container=block as M.ContainerBlock; if(container!=null) AddBlocks(target,container,width);
            }
        }
        static void AddTable(BlockCollection target,MT.Table source,double width) {
            var table=new Table { CellSpacing=0,Margin=new Thickness(0,4,0,14),FontSize=12,LineHeight=20 };
            int count=source.ColumnDefinitions.Count;
            for(int i=0;i<count;i++) table.Columns.Add(new TableColumn { Width=new GridLength(1,GridUnitType.Star) });
            var group=new TableRowGroup(); table.RowGroups.Add(group); int rowIndex=0;
            foreach(MT.TableRow row in source) {
                var output=new TableRow { Background=Ui.Brush(rowIndex==0?"#F1F3F8":rowIndex%2==0?"#F4F6FA":"#FEFEFF") };
                if(row.IsHeader) output.FontWeight=FontWeights.SemiBold;
                foreach(MT.TableCell cell in row) {
                    var view=new TableCell { BorderBrush=Ui.Brush("#E5E9F2"),BorderThickness=new Thickness(0,0,1,1),Padding=new Thickness(7,6,7,6),ColumnSpan=cell.ColumnSpan,RowSpan=cell.RowSpan };
                    AddBlocks(view.Blocks,cell,Math.Max(60,(width/count)-16)); foreach(var item in view.Blocks) item.Margin=new Thickness(0);
                    int index=cell.ColumnIndex; if(index>=0&&index<count) {
                        var alignment=source.ColumnDefinitions[index].Alignment;
                        if(alignment.HasValue) view.TextAlignment=alignment.Value==MT.TableColumnAlign.Center?TextAlignment.Center:alignment.Value==MT.TableColumnAlign.Right?TextAlignment.Right:TextAlignment.Left;
                    }
                    output.Cells.Add(view);
                }
                group.Rows.Add(output); rowIndex++;
            }
            target.Add(table);
        }
        static void AddInlines(InlineCollection target,MI.ContainerInline container,double width,MI.Inline only=null) {
            for(var inline=only??container.FirstChild;inline!=null;inline=only==null?inline.NextSibling:null) {
                var math=inline as MM.MathInline; if(math!=null) { AddInlineMath(target,math.Content.ToString(),width,math.Delimiter=='\\'?@"\(":"$",math.Delimiter=='\\'?@"\)":"$"); continue; }
                var literal=inline as MI.LiteralInline; if(literal!=null) { target.Add(new Run(literal.Content.ToString())); continue; }
                var code=inline as MI.CodeInline; if(code!=null) { target.Add(new Run(code.Content) { FontFamily=new FontFamily("Consolas"),FontSize=12,Background=Ui.Brush("#F4F6FA") }); continue; }
                var emphasis=inline as MI.EmphasisInline;
                if(emphasis!=null) {
                    var span=new Span(); if(emphasis.DelimiterChar=='~') span.TextDecorations=TextDecorations.Strikethrough; else if(emphasis.DelimiterCount>=2) span.FontWeight=FontWeights.SemiBold; else span.FontStyle=FontStyles.Italic;
                    AddInlines(span.Inlines,emphasis,width); target.Add(span); continue;
                }
                if(inline is MI.LineBreakInline) { target.Add(new LineBreak()); continue; }
                var link=inline as MI.LinkInline;
                if(link!=null) {
                    Uri uri; bool safe=!link.IsImage&&Uri.TryCreate(link.Url,UriKind.Absolute,out uri)&&(uri.Scheme=="https"||uri.Scheme=="http");
                    if(safe) {
                        var hyper=new Hyperlink { NavigateUri=new Uri(link.Url),Foreground=Ui.Brush("#5864D8"),ToolTip=link.Url };
                        AddInlines(hyper.Inlines,link,width); hyper.RequestNavigate+=delegate(object sender,System.Windows.Navigation.RequestNavigateEventArgs e) { e.Handled=true; try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute=true }); } catch { } }; target.Add(hyper);
                    } else { if(link.IsImage) target.Add(new Run("图片：")); AddInlines(target,link,width); } continue;
                }
                var auto=inline as MI.AutolinkInline; if(auto!=null) { target.Add(new Run(auto.Url)); continue; }
                var nested=inline as MI.ContainerInline; if(nested!=null) AddInlines(target,nested,width);
            }
        }
        static DrawingImage Formula(string latex,bool display) {
            if(string.IsNullOrWhiteSpace(latex)||latex.Length>2500) throw new InvalidOperationException("Formula too long");
            string key=(display?"D":"I")+latex; DrawingImage image;
            if(mathCache.TryGetValue(key,out image)) { recentMath.Remove(key); recentMath.AddFirst(key); return image; }
            try {
                var formula=WpfTeXFormulaParser.Instance.Parse(latex);
                var environment=WpfTeXEnvironment.Create(display?TexStyle.Display:TexStyle.Text,18,"Cambria",Ui.Brush("#303B50"),Brushes.Transparent);
                var geometry=formula.RenderToGeometry(environment,18); var bounds=geometry.Bounds;
                if(bounds.IsEmpty||bounds.Width>20000||bounds.Height>5000) throw new InvalidOperationException("Formula dimensions invalid");
                var drawing=new DrawingGroup(); drawing.Children.Add(new GeometryDrawing(Ui.Brush("#303B50"),null,geometry)); drawing.Transform=new TranslateTransform(-bounds.X,-bounds.Y);
                image=new DrawingImage(drawing); image.Freeze();
            } catch { image=ExtendedMath.Render(latex,display); }
            mathCache[key]=image; recentMath.AddFirst(key);
            while(recentMath.Count>64) { string oldest=recentMath.Last.Value; recentMath.RemoveLast(); mathCache.Remove(oldest); } return image;
        }
        static Image MathImage(string latex,bool display,double width) {
            var source=Formula(latex,display); var image=new Image { Source=source,Tag="latex",Stretch=Stretch.Uniform,Width=source.Width,Height=source.Height,ToolTip=latex,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,2,2,2) };
            if(!display&&source.Width>width) { image.Width=width; image.Height=source.Height*width/source.Width; }
            System.Windows.Automation.AutomationProperties.SetName(image,latex); return image;
        }
        static void AddInlineMath(InlineCollection target,string latex,double width,string open,string close) {
            try { target.Add(new InlineUIContainer(MathImage(latex,false,width)) { BaselineAlignment=BaselineAlignment.Center }); }
            catch { target.Add(new Run(open+latex+close) { FontFamily=new FontFamily("Consolas"),ToolTip="公式尚未完整或包含暂不支持的语法，保留原文" }); }
        }
        static void AddDisplayMath(BlockCollection target,string latex,double width,string open="$$",string close="$$") {
            try {
                var image=MathImage(latex,true,width); var scroll=new ScrollViewer { Content=image,MaxWidth=width,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,5,0,12) };
                var style=new Style(typeof(System.Windows.Controls.Primitives.ScrollBar),(Style)Application.Current.FindResource(typeof(System.Windows.Controls.Primitives.ScrollBar)));
                style.Setters.Add(new Setter(FrameworkElement.WidthProperty,double.NaN)); style.Setters.Add(new Setter(FrameworkElement.HeightProperty,7.0)); scroll.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar),style);
                target.Add(new BlockUIContainer(scroll));
            } catch { var paragraph=Para(); paragraph.FontFamily=new FontFamily("Consolas"); paragraph.FontSize=12; paragraph.Inlines.Add(new Run(open+"\n"+latex+"\n"+close) { ToolTip="公式尚未完整或包含暂不支持的语法，保留原文" }); target.Add(paragraph); }
        }
    }
}
