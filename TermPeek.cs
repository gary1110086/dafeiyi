using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
namespace LightTranslate {
    public class TermPeek:Window {
        public event Action BookRequested;
        bool closing;
        public TermPeek(TermEntry entry,Settings settings) {
            Title="大肥译 · 已收藏术语"; Width=370; Height=420; Topmost=true; ShowInTaskbar=false; ShowActivated=true;
            var root=new Grid(); root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            var head=new DockPanel { Margin=new Thickness(0,0,0,12) }; var close=Ui.Button("×",Close,false); close.Width=28; close.Padding=new Thickness(0); DockPanel.SetDock(close,Dock.Right); head.Children.Add(close);
            var title=new StackPanel(); title.Children.Add(Ui.Text("已收藏术语",10,"#8CD3DF")); var term=Ui.Text(entry.Term,18,"#F4F1E8"); term.MaxHeight=70; term.Margin=new Thickness(0,4,0,0); title.Children.Add(term); head.Children.Add(title); Ui.Row(root,head,0);
            var document=new FlowDocument { FontSize=settings.ReadingFontSize,LineHeight=settings.ReadingFontSize*settings.ReadingLineSpacing,PagePadding=new Thickness(0),Foreground=Ui.Brush("#DEE8F4"),FontFamily=new System.Windows.Media.FontFamily("Microsoft YaHei UI, Segoe UI") };
            if(!string.IsNullOrWhiteSpace(entry.PreferredTranslation)) document.Blocks.Add(ReadingTypography.Card("我的常用译法",entry.PreferredTranslation,320,settings,"note"));
            document.Blocks.Add(ReadingTypography.Card("收藏解释",entry.Explanation,320,settings,"answer"));
            if(!string.IsNullOrWhiteSpace(entry.Note)) document.Blocks.Add(ReadingTypography.Card("我的笔记",entry.Note,320,settings,"note"));
            var preview=new RichTextBox { IsReadOnly=true,IsDocumentEnabled=true,Document=document,BorderThickness=new Thickness(0),Background=System.Windows.Media.Brushes.Transparent,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(0) }; Ui.Row(root,preview,1);
            var footer=new DockPanel { Margin=new Thickness(0,12,0,0) }; var book=Ui.Button("打开术语本",delegate { if(BookRequested!=null) BookRequested(); Close(); },true); DockPanel.SetDock(book,Dock.Right); footer.Children.Add(book); var hint=Ui.Text("本地收藏 · 未请求 API",10,"#AEBFD3"); hint.VerticalAlignment=VerticalAlignment.Center; footer.Children.Add(hint); Ui.Row(root,footer,2); Ui.Frame(this,root,14);
            Closing+=delegate { closing=true; };
            KeyDown+=delegate(object s,KeyEventArgs e) { if(e.Key==Key.Escape) { Close(); e.Handled=true; } };
            Deactivated+=delegate { Dispatcher.BeginInvoke(new Action(delegate { if(!closing) Close(); })); };
        }
    }
}
