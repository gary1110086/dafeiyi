using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace LightTranslate {
    internal static class RenderTests {
        static int passed,failed; static StringBuilder report=new StringBuilder();
        static void Assert(bool value,string message) { if(!value) throw new Exception(message); }
        static void Check(string name,Action action) { try { action(); passed++; report.AppendLine("PASS "+name); } catch(Exception e) { failed++; report.AppendLine("FAIL "+name+": "+e.Message); } }
        static IEnumerable<DependencyObject> Nodes(DependencyObject root) {
            yield return root;
            foreach(object child in LogicalTreeHelper.GetChildren(root)) { var node=child as DependencyObject; if(node!=null) foreach(var next in Nodes(node)) yield return next; }
        }
        static RichTextBox Box(PopupView popup) { return Nodes(popup).OfType<RichTextBox>().First(); }
        static int FormulaCount(PopupView popup) { return Nodes(Box(popup).Document).OfType<Image>().Count(x=>x.Tag as string=="latex"); }
        const string TableText="## lattice 的三个含义\n\n| 用法 | 含义 | 例子 |\n|---|---|---|\n| 严格用法 | 几何点集（Bravais lattice） | 平移对称性 |\n| 通俗用法 | 晶格、原子排列 | 金刚石结构 |\n| 材料用法 | 周期性排列 | lattice constant |";
        public static int Run(string folder,Application app) {
            Directory.CreateDirectory(folder); app.Dispatcher.BeginInvoke(new Action(async delegate {
                var popup=new PopupView(); popup.Show(); await Task.Delay(60);
                popup.SetSource("Understanding the crystal lattice");
                popup.SetAnswer("### 晶格，是描述重复排列的骨架\n\n可以先把它想成一组有规律的点：每个点代表一个重复位置，具体放什么原子由**基元**决定。\n\n晶格向量 $\\vec{R}=n_1\\vec{a}_1+n_2\\vec{a}_2+n_3\\vec{a}_3$。\n\n> 晶体结构 = 晶格 + 基元");
                popup.SetBusy(false,"本地排版预览 · 未调用 API"); await Task.Delay(80);
                UiTests.Capture(popup,Path.Combine(folder,"插画紧凑阅读.png"));
                Check("lattice Markdown is a native three-column four-row table",delegate {
                    popup.SetAnswer(TableText); var table=Nodes(Box(popup).Document).OfType<Table>().FirstOrDefault(); Assert(table!=null,"pipe table remained plain text"); Assert(table.Columns.Count==3&&table.RowGroups[0].Rows.Count==4,"table cells were lost"); Assert(!new TextRange(Box(popup).Document.ContentStart,Box(popup).Document.ContentEnd).Text.Contains("|---"),"separator is visible");
                });
                Check("nested and ordered lists, quote and emphasis retain structure",delegate {
                    popup.SetAnswer("3. **原子**\n   - *基元*\n4. 第二步\n\n> 引用解释\n\n~~旧结论~~"); var nodes=Nodes(Box(popup).Document).ToArray(); Assert(nodes.OfType<System.Windows.Documents.List>().Count()>=2,"nested list flattened"); Assert(nodes.OfType<Span>().Any(x=>x.FontStyle==FontStyles.Italic),"italic lost"); Assert(nodes.OfType<Span>().Any(x=>x.TextDecorations!=null&&x.TextDecorations.Count>0),"strikethrough lost");
                });
                Check("inline dollar and parenthesis LaTex both render vector formulas",delegate {
                    popup.SetAnswer(@"能量 $E=mc^2$，还有 \(\frac{a}{b}+\alpha_i\)。"); Assert(FormulaCount(popup)==2,"inline math left as syntax"); Assert(Nodes(Box(popup).Document).OfType<Image>().All(x=>x.Source is DrawingImage),"math uses blurred bitmap");
                });
                Check("spaced parenthesis formulas and aligned equations render",delegate {
                    popup.SetAnswer(@"公式 \( \frac{1}{2} + \beta \)"+"\n\n\\[\n"+@"\begin{aligned}a&=b+c\\d&=e\end{aligned}"+"\n\\]"); Assert(FormulaCount(popup)==2,"common delimiter spacing or aligned equations stayed raw");
                });
                Check("display dollar and bracket formulas render fractions integrals and matrices",delegate {
                    popup.SetAnswer("$$\n\\int_0^1 x^2\\,dx=\\frac{1}{3}\n$$\n\n\\[\\begin{pmatrix}a&b\\\\c&d\\end{pmatrix}\\]"); Assert(FormulaCount(popup)==2,"display math was not rendered");
                });
                Check("physics vectors derivatives sums and piecewise formulas render",delegate {
                    foreach(string formula in new[]{@"\boldsymbol{\tau}=\vec{m}\times(\vec{m}\times\vec{\sigma})",@"\frac{d\mathbf{m}}{dt}=-\gamma\mathbf{m}\times\mathbf{H}_{\mathrm{eff}}+\alpha\mathbf{m}\times\frac{d\mathbf{m}}{dt}",@"f(x)=\begin{cases}x^2&x\geq0\\-x&x<0\end{cases}"}) { try { ExtendedMath.Render(formula,true); } catch(Exception e) { throw new Exception(formula+" => "+e.ToString()); } }
                    popup.SetAnswer(@"$\boldsymbol{\tau}=\vec{m}\times(\vec{m}\times\vec{\sigma})$"+"\n\n$$\n"+@"\frac{d\mathbf{m}}{dt}=-\gamma\mathbf{m}\times\mathbf{H}_{\mathrm{eff}}+\alpha\mathbf{m}\times\frac{d\mathbf{m}}{dt}"+"\n$$\n\n$$\n"+@"f(x)=\begin{cases}x^2&x\geq0\\-x&x<0\end{cases}"+"\n$$"); Assert(FormulaCount(popup)==3,"common physics math fell back to syntax"); UiTests.Capture(popup,Path.Combine(folder,"物理公式与分段函数.png"));
                });
                Check("code fences and inline code preserve Markdown and TeX literally",delegate {
                    string text="```tex\n# **literal** \\(\\frac{1}{2}\\) $x$\n```\n\n`\\(x\\)`"; popup.SetAnswer(text); var visible=new TextRange(Box(popup).Document.ContentStart,Box(popup).Document.ContentEnd).Text; Assert(visible.Contains("# **literal**")&&visible.Contains(@"\frac{1}{2}"),"code interpreted as Markdown"); Assert(FormulaCount(popup)==0,"code interpreted as formula");
                });
                Check("indented code and invalid closing fences remain literal",delegate {
                    foreach(string text in new[]{"    \\(x\\)\n    \\[x\\]","```tex\n```not-a-closing-fence\n\\(x\\)\n\\[x\\]\n```"}) {
                        popup.SetAnswer(text); string visible=new TextRange(Box(popup).Document.ContentStart,Box(popup).Document.ContentEnd).Text;
                        Assert(visible.Contains(@"\(x\)")&&visible.Contains(@"\[x\]")&&FormulaCount(popup)==0,"literal code was rewritten or rendered");
                    }
                });
                Check("display math remains inside its quote or list item",delegate {
                    popup.SetAnswer(@"> \[\frac{1}{2}\]"+"\n\n"+@"1. \[x^2\]"); var document=Box(popup).Document;
                    Assert(FormulaCount(popup)==2,"nested display formulas vanished"); Assert(Nodes(document).OfType<Section>().Any(x=>Nodes(x).OfType<Image>().Any()),"quote math escaped quote"); Assert(Nodes(document).OfType<ListItem>().Any(x=>Nodes(x).OfType<Image>().Any()),"list math escaped item");
                });
                Check("escaped pipes and code pipes do not create extra table cells",delegate {
                    popup.SetAnswer("| 项目 | 内容 |\n|---|---|\n| a\\|b | `c\\|d` |"); var table=Nodes(Box(popup).Document).OfType<Table>().FirstOrDefault(); Assert(table!=null&&table.Columns.Count==2&&table.RowGroups[0].Rows[1].Cells.Count==2,"escaped pipe changed columns");
                });
                Check("partial streamed Markdown and malformed formula remain readable",delegate {
                    string text=TableText+"\n\n公式 \\(\\frac{1}{2}\\)，未知 $\\unknowncommand{x}$。"; for(int i=1;i<=text.Length;i+=7) popup.SetAnswer(text.Substring(0,i)); popup.SetAnswer(text); Assert(popup.AnswerText==text,"raw answer lost"); Assert(FormulaCount(popup)==1,"valid formula lost beside malformed one"); Assert(new TextRange(Box(popup).Document.ContentStart,Box(popup).Document.ContentEnd).Text.Contains("unknowncommand"),"invalid math vanished");
                });
                Check("raw copy retains source Markdown and all LaTex delimiters",delegate {
                    string text=@"**解释** \(\frac{1}{2}\)"; popup.SetSource("source"); popup.SetAnswer(text); Assert(popup.AnswerText==text,"rendering changed raw answer");
                });
                Check("remote image and raw HTML do not become active content",delegate {
                    popup.SetAnswer("![远程图片](https://example.com/private.png)\n\n<script>alert(1)</script>\n\n[危险](javascript:alert(1))"); Assert(!Nodes(Box(popup).Document).OfType<Image>().Any(),"external image loaded"); Assert(!Nodes(Box(popup).Document).OfType<Hyperlink>().Any(x=>x.NavigateUri!=null&&x.NavigateUri.Scheme=="javascript"),"unsafe link active");
                });
                Check("formula reuse is bounded and a new answer replaces old content",delegate {
                    for(int i=0;i<75;i++) popup.SetAnswer("$x_"+i+"^2$"); var type=typeof(PopupView).Assembly.GetType("LightTranslate.MarkdownView"); Assert(type!=null,"real renderer missing"); var property=type.GetProperty("MathCacheCount",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic); Assert(property!=null&&(int)property.GetValue(null,null)<=64,"formula cache unbounded"); popup.SetAnswer("新的段落"); Assert(FormulaCount(popup)==0,"previous answer retained formulas");
                });
                string longText=string.Join("\n\n",Enumerable.Repeat("滚动阅读中的段落内容，不应该因新文字到来而跳回开头。",55)); popup.SetAnswer(longText); popup.SetBusy(true,"流式验证"); await Task.Delay(90); Box(popup).ScrollToVerticalOffset(180); await Task.Delay(60); double previousOffset=Box(popup).VerticalOffset; popup.SetAnswer(longText+"\n\n新增的流式内容"); await Task.Delay(90);
                Check("stream updates preserve the reader scroll position",delegate { Assert(previousOffset>100&&Box(popup).VerticalOffset>=previousOffset-1,"stream reset reading position"); }); popup.SetBusy(false,"完成");
                popup.SetAnswer("$$\nE=mc^2\n$$"); double oldWidth=Nodes(Box(popup).Document).OfType<ScrollViewer>().First().MaxWidth; popup.ToggleExpanded(); await Task.Delay(60);
                Check("expanded reading recomputes formula viewport width",delegate { Assert(Nodes(Box(popup).Document).OfType<ScrollViewer>().First().MaxWidth>oldWidth+50,"formula stayed constrained to compact width"); }); popup.ToggleExpanded();
                popup.ToggleExpanded(); popup.SetSource("lattice · Markdown 表格与 LaTeX 公式"); popup.SetAnswer(TableText+"\n\n### 数学排版\n\n能量为 $E=mc^2$，晶格向量为 $\\vec{R}=n_1\\vec{a}_1+n_2\\vec{a}_2+n_3\\vec{a}_3$。\n\n$$\n\\int_0^1 x^2\\,dx=\\frac{1}{3}\n$$\n\n> 完整内容支持选择、滚动与复制。\n\n```c\nenergy = mass * c * c;\n```"); popup.SetBusy(false,"Markdown 与公式 · 本地渲染演示"); await Task.Delay(100);
                UiTests.Capture(popup,Path.Combine(folder,"Markdown与公式.png"));
                Check("actual popup renders Markdown and formulas within reading layout",delegate { Assert(FormulaCount(popup)>=3,"render proof missing math"); Assert(popup.ActualWidth<=510,"layout expanded outside window"); });
                popup.SetAppearance(new Settings { BackgroundTheme="angry" }); await Task.Delay(100);
                UiTests.Capture(popup,Path.Combine(folder,"举手肥鱼-表格与公式.png"));
                Check("new WebP backdrop switches without replacing the answer document",delegate { Assert(popup.BackgroundTheme=="angry"&&FormulaCount(popup)>=3,"background change lost reading content"); Assert(ReadingBackdrop.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","Reading","angry-maid.webp")).PixelWidth==720,"WebP artwork missing"); });
                popup.SetSource("Read a little, understand a little."); popup.SetAnswer("### 今天也要读懂一点\n\n把难懂的句子交给大肥译，先翻译，再慢慢理解其中的概念。\n\n**阅读时的小帮手**：选中、复制、翻译，也可以接着追问。\n\n晶格向量 $\\vec{R}=n_1\\vec{a}_1+n_2\\vec{a}_2+n_3\\vec{a}_3$。"); await Task.Delay(100);
                UiTests.Capture(popup,Path.Combine(folder,"举手肥鱼-展开阅读.png")); popup.ToggleExpanded(); await Task.Delay(100); UiTests.Capture(popup,Path.Combine(folder,"举手肥鱼-紧凑阅读.png"));
                popup.SetAppearance(new Settings { BackgroundTheme="none" }); Check("plain reading mode preserves the same source and answer",delegate { Assert(popup.BackgroundTheme=="none"&&popup.AnswerText.Contains("今天"),"plain mode changed content"); });
                Check("custom WebP imports as a local PNG and remains usable without the source",delegate {
                    string original=Path.Combine(folder,"temporary-import.webp"); File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","Reading","angry-maid.webp"),original,true);
                    string imported=ReadingBackdrop.Import(original,Path.Combine(folder,"import-test")); File.Delete(original);
                    Assert(File.Exists(imported)&&ReadingBackdrop.Load(imported).PixelWidth==720,"custom image depended on original or failed decoding");
                    string answer=popup.AnswerText; popup.SetAppearance(new Settings { BackgroundTheme="custom",BackgroundPath=imported,BackgroundStrength=70 }); Assert(popup.AnswerText==answer&&popup.BackgroundTheme=="custom","custom background replaced the answer");
                });
                popup.SetAppearance(new Settings { BackgroundTheme="angry" });
                var follow=Nodes(popup).OfType<TextBox>().First(); follow.Text="这段公式是什么意思？ Explain lattice"; follow.Focus(); await Task.Delay(80);
                UiTests.Capture(popup,Path.Combine(folder,"输入文字可读性.png")); follow.Select(0,9); await Task.Delay(60); UiTests.Capture(popup,Path.Combine(folder,"输入文字选中状态.png"));
                popup.Hide(); report.AppendLine(String.Format("RESULT {0} passed, {1} failed",passed,failed)); File.WriteAllText(Path.Combine(folder,"render-test.txt"),report.ToString(),new UTF8Encoding(true)); app.Shutdown(failed==0?0:1);
            })); return app.Run();
        }
    }
}
