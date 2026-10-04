using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Media;
namespace LightTranslate {
    public class TermMatch { public int Start,Length; public TermEntry Entry; }
    public class TermLookup {
        readonly Regex pattern; readonly Dictionary<string,TermEntry> entries=new Dictionary<string,TermEntry>(StringComparer.OrdinalIgnoreCase);
        static bool Latin(char c) { return c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_'; }
        public TermLookup(IList<TermEntry> terms) {
            foreach(var term in terms) if(!string.IsNullOrWhiteSpace(term.Term)&&!entries.ContainsKey(term.Term)) entries.Add(term.Term,term.Copy());
            if(entries.Count==0) return;
            var pieces=entries.Keys.OrderByDescending(t=>t.Length).ThenBy(t=>t,StringComparer.Ordinal).Select(t=>(Latin(t[0])?@"(?<![A-Za-z0-9_])":"")+Regex.Escape(t)+(Latin(t[t.Length-1])?@"(?![A-Za-z0-9_])":""));
            pattern=new Regex(string.Join("|",pieces),RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(70));
        }
        public List<TermMatch> Find(string text) {
            var result=new List<TermMatch>(); if(pattern==null||string.IsNullOrEmpty(text)) return result;
            try { foreach(Match match in pattern.Matches(text)) { result.Add(new TermMatch { Start=match.Index,Length=match.Length,Entry=entries[match.Value].Copy() }); if(result.Count==200) break; } } catch(RegexMatchTimeoutException) { result.Clear(); }
            return result;
        }
        internal void Decorate(FlowDocument document,Action<TermEntry> clicked) { foreach(var block in document.Blocks.ToList()) Block(block,clicked); }
        void Block(Block block,Action<TermEntry> clicked) {
            var paragraph=block as Paragraph; if(paragraph!=null) { if(paragraph.FontFamily.Source!="Consolas") Decorate(paragraph.Inlines,clicked); return; }
            var section=block as Section; if(section!=null) { foreach(var b in section.Blocks.ToList()) Block(b,clicked); return; }
            var list=block as System.Windows.Documents.List; if(list!=null) { foreach(var item in list.ListItems.ToList()) foreach(var b in item.Blocks.ToList()) Block(b,clicked); return; }
            var table=block as Table; if(table!=null) foreach(var group in table.RowGroups.ToList()) foreach(var row in group.Rows.ToList()) foreach(var cell in row.Cells.ToList()) foreach(var b in cell.Blocks.ToList()) Block(b,clicked);
        }
        internal void Decorate(InlineCollection inlines,Action<TermEntry> clicked) {
            foreach(var inline in inlines.ToList()) {
                if(inline is Hyperlink) continue;
                var run=inline as Run;
                if(run!=null) {
                    if(run.FontFamily.Source=="Consolas") continue; var matches=Find(run.Text); if(matches.Count==0) continue;
                    int at=0; var span=new Span();
                    foreach(var m in matches) { if(m.Start>at) span.Inlines.Add(new Run(run.Text.Substring(at,m.Start-at))); var saved=m.Entry; var link=new Hyperlink(new Run(run.Text.Substring(m.Start,m.Length))) { Foreground=Ui.Brush("#B9E5E9"),Cursor=System.Windows.Input.Cursors.Hand,Tag=saved,ToolTip="已收藏 · "+saved.Term+"\n点击查看解释与笔记（本地）" };
                        var decoration=new TextDecoration { Location=TextDecorationLocation.Underline,Pen=new Pen(Ui.Brush("#6396A6"),0.7),PenOffset=2,PenOffsetUnit=TextDecorationUnit.Pixel,PenThicknessUnit=TextDecorationUnit.Pixel }; link.TextDecorations=new TextDecorationCollection { decoration }; link.Click+=delegate(object sender,RoutedEventArgs e) { e.Handled=true; if(clicked!=null) clicked(saved); }; span.Inlines.Add(link); at=m.Start+m.Length; }
                    if(at<run.Text.Length) span.Inlines.Add(new Run(run.Text.Substring(at))); inlines.InsertBefore(run,span); inlines.Remove(run);
                } else { var nested=inline as Span; if(nested!=null) Decorate(nested.Inlines,clicked); }
            }
        }
    }
}
