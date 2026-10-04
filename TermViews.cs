using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace LightTranslate {
    public class TermEditor:Window {
        readonly TextBox term,explanation,context,note,preferred; readonly TextBlock feedback;
        readonly TermStore store; readonly string id;
        public event Action<TermEntry> Saved;
        public TermEditor(TermStore collection,TermEntry entry) {
            store=collection; id=entry.Id; Title="大肥译 · 收藏术语"; Width=490; Height=620; MinWidth=420; MinHeight=420; Topmost=true; ShowInTaskbar=false; WindowStartupLocation=WindowStartupLocation.CenterScreen;
            var root=new Grid(); root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            var head=new DockPanel { Margin=new Thickness(0,0,0,16) }; var close=Ui.Button("×",Close,false); DockPanel.SetDock(close,Dock.Right); head.Children.Add(close); head.Children.Add(Ui.Text(string.IsNullOrEmpty(id)?"收藏一个术语":"编辑术语",20,"#F4F1E8")); Ui.Row(root,head,0);
            var form=new StackPanel(); term=Box(entry.Term,false); term.MaxLength=200; explanation=Box(entry.Explanation,true); explanation.MinHeight=150; explanation.MaxHeight=220;
            context=Box(entry.Context,true); context.MaxLength=6000; context.MinHeight=70; note=Box(entry.Note,true); note.MaxLength=6000; note.MinHeight=80;
            form.Children.Add(Ui.Labelled("术语",term,"可以收藏一个词，也可以收藏短句，最多 200 字。"));
            preferred=Box(entry.PreferredTranslation,false); preferred.MaxLength=300; preferred.Tag="例如：点阵 / 自旋轨道力矩（可选）"; form.Children.Add(Ui.Labelled("我的常用译法",preferred,"本段匹配时作为翻译参考；解释和个人笔记不会附加到请求。"));
            form.Children.Add(Ui.Labelled("解释 / 译文",explanation,"保留 Markdown 与数学公式，在术语本中自动渲染。"));
            form.Children.Add(Ui.Labelled("我的笔记",note,"例如：和 basis 的区别；老师使用的含义。"));
            form.Children.Add(new Expander { Header="原文上下文",Content=context,Margin=new Thickness(0,0,0,10) });
            Ui.Row(root,new ScrollViewer { Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled },1);
            var footer=new Grid { Margin=new Thickness(0,14,0,0) }; footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto }); feedback=Ui.Text("仅保存在本机 · Windows 加密",11,"#AEBFD3"); feedback.VerticalAlignment=VerticalAlignment.Center; feedback.MaxWidth=290; footer.Children.Add(feedback);
            var save=Ui.Button("保存收藏",Save,true); Grid.SetColumn(save,1); footer.Children.Add(save); Ui.Row(root,footer,2); Ui.Frame(this,root,18);
            Loaded+=delegate { term.Focus(); if(string.IsNullOrWhiteSpace(term.Text)) feedback.Text="先为这段内容填写一个术语或短标题"; };
            KeyDown+=delegate(object s,KeyEventArgs e) { if(e.Key==Key.Escape) Close(); if(e.Key==Key.S&&(Keyboard.Modifiers&ModifierKeys.Control)!=0) { Save(); e.Handled=true; } };
        }
        static TextBox Box(string text,bool multiline) { return new TextBox { Text=text??"",AcceptsReturn=multiline,TextWrapping=multiline?TextWrapping.Wrap:TextWrapping.NoWrap,VerticalScrollBarVisibility=multiline?ScrollBarVisibility.Auto:ScrollBarVisibility.Hidden,Padding=new Thickness(10,8,10,8),FontSize=13 }; }
        internal void Save() { try { var saved=store.SaveTermWithPreference(id,term.Text,explanation.Text,context.Text,note.Text,preferred.Text); if(Saved!=null) Saved(saved); Close(); } catch(Exception e) { feedback.Text=e.Message; feedback.Foreground=Ui.Brush("#FFB5AE"); } }
    }
    public class TermBook:Window {
        readonly TermStore store; readonly ListBox list; readonly TextBox search; readonly TextBlock count,title,meta,feedback; readonly RichTextBox preview; readonly Button edit,copy,remove,open;
        TermEditor editor; string deleteId="";
        Settings readingSettings;
        public event Action<TermEntry> OpenRequested;
        internal int VisibleCount { get { return list.Items.Count; } }
        internal TermEntry Selection { get { return list.SelectedItem as TermEntry; } }
        public TermBook(TermStore collection,Settings style=null) {
            readingSettings=(style??new Settings()).Copy();
            store=collection; Title="大肥译 · 术语收藏"; Width=770; Height=590; MinWidth=650; MinHeight=440; Topmost=true; ShowInTaskbar=false; WindowStartupLocation=WindowStartupLocation.CenterScreen;
            var root=new Grid(); root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            var head=new DockPanel { Margin=new Thickness(0,0,0,14) }; var close=Ui.Button("×",Close,false); DockPanel.SetDock(close,Dock.Right); head.Children.Add(close); var brand=new StackPanel { Orientation=Orientation.Horizontal }; brand.Children.Add(WhaleTheme.Portrait(40)); var heading=new StackPanel(); heading.Children.Add(Ui.Text("术语收藏",21,"#F4F1E8")); count=Ui.Text("",11,"#AEBFD3"); heading.Children.Add(count); brand.Children.Add(heading); head.Children.Add(brand); Ui.Row(root,head,0);
            var searchRow=new Grid { Margin=new Thickness(0,0,0,16) }; searchRow.ColumnDefinitions.Add(new ColumnDefinition()); searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto }); search=new TextBox { Tag="搜索术语、解释或笔记…",Padding=new Thickness(11,8,11,8) }; searchRow.Children.Add(search); var add=Ui.Button("＋ 新术语",delegate { Edit(new TermEntry()); },true); add.Margin=new Thickness(9,0,0,0); Grid.SetColumn(add,1); searchRow.Children.Add(add); Ui.Row(root,searchRow,1);
            var body=new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(200) }); body.ColumnDefinitions.Add(new ColumnDefinition());
            list=new ListBox { Background=Ui.Brush("#101E32"),Foreground=Ui.Brush("#DEE8F4"),BorderBrush=Ui.Brush("#344A63"),BorderThickness=new Thickness(1),Padding=new Thickness(5) }; list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled);
            list.ItemTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel MaxWidth='158' Margin='6,9'><TextBlock Text='{Binding Term}' TextWrapping='Wrap' FontSize='13' FontWeight='SemiBold' Foreground='#DEE8F4'/><TextBlock Text='{Binding Preview}' TextWrapping='Wrap' MaxHeight='36' LineHeight='17' TextTrimming='CharacterEllipsis' FontSize='10' Foreground='#AEBFD3' Margin='0,5,0,0'/></StackPanel></DataTemplate>"); list.ClearValue(ItemsControl.DisplayMemberPathProperty);
            list.ItemContainerStyle=(Style)System.Windows.Markup.XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ListBoxItem'><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ListBoxItem'><Border x:Name='Body' Background='Transparent' CornerRadius='8' Margin='0,2'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='Body' Property='Background' Value='#294760'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Body' Property='Background' Value='#213C55'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
            list.BorderThickness=new Thickness(0); list.Background=Brushes.Transparent; body.Children.Add(new Border { Background=Ui.Brush("#101E32"),BorderBrush=Ui.Brush("#344A63"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(11),Padding=new Thickness(3),Child=list });
            var detail=new Grid { Margin=new Thickness(20,0,0,0) }; detail.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); detail.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); detail.RowDefinitions.Add(new RowDefinition()); detail.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            title=Ui.Text("收藏以后，理解就留在这里。",19,"#F4F1E8"); title.MaxHeight=75; Ui.Row(detail,title,0); meta=Ui.Text("",10,"#AEBFD3"); meta.Margin=new Thickness(0,7,0,12); Ui.Row(detail,meta,1);
            preview=new RichTextBox { IsReadOnly=true,IsDocumentEnabled=true,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Padding=new Thickness(0),VerticalScrollBarVisibility=ScrollBarVisibility.Auto }; Ui.Row(detail,preview,2);
            var actions=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0) }; edit=Ui.Button("编辑",delegate { if(Selection!=null) Edit(Selection); },true); copy=Ui.Button("复制",Copy,false); open=Ui.Button("结果窗",delegate { if(Selection!=null&&OpenRequested!=null) OpenRequested(Selection); },false); remove=Ui.Button("删除",Delete,false); remove.Foreground=Ui.Brush("#FFB5AE"); foreach(var b in new[]{edit,copy,open,remove}) { b.Padding=new Thickness(10,6,10,6); b.Margin=new Thickness(0,0,6,0); actions.Children.Add(b); } Ui.Row(detail,actions,3); Grid.SetColumn(detail,1); body.Children.Add(detail); Ui.Row(root,body,2);
            feedback=Ui.Text("收藏独立保存 · 解释与笔记留在本机",11,"#AEBFD3"); feedback.Margin=new Thickness(0,15,0,0); Ui.Row(root,feedback,3); Ui.Frame(this,root,20);
            search.TextChanged+=delegate { Refresh(null); }; list.SelectionChanged+=delegate { deleteId=""; remove.Content="删除"; Render(); }; SizeChanged+=delegate { if(preview!=null) Render(); }; Closed+=delegate { if(editor!=null) editor.Close(); }; KeyDown+=delegate(object s,KeyEventArgs e) { if(e.Key==Key.Escape) Close(); if(e.Key==Key.F&&(Keyboard.Modifiers&ModifierKeys.Control)!=0) { search.Focus(); e.Handled=true; } }; Refresh(null);
        }
        internal void Refresh(string id) { string selected=id??(Selection==null?"":Selection.Id); var items=store.Search(search.Text); list.ItemsSource=items; count.Text=store.Count+" 个术语 · "+items.Count+" 个匹配"; list.SelectedItem=items.FirstOrDefault(e=>e.Id==selected)??items.FirstOrDefault(); if(store.LastError.Length>0) feedback.Text=store.LastError; Render(); }
        internal void SetReadingStyle(Settings style) { readingSettings=style.Copy(); Render(); }
        internal void Reveal(string id) { search.Clear(); Refresh(id); }
        void Render() {
            var e=Selection; foreach(var b in new[]{edit,copy,remove,open}) b.IsEnabled=e!=null;
            bool noMatch=e==null&&!string.IsNullOrWhiteSpace(search.Text); title.Text=e==null?(noMatch?"没有匹配的术语":"收藏以后，理解就留在这里。"):e.Term;
            DateTime time; meta.Text=e==null?"从译文窗口点击书签，收藏第一次理解。":DateTime.TryParse(e.UpdatedUtc,out time)?"更新于 "+time.ToLocalTime().ToString("yyyy-MM-dd HH:mm"):"";
            string text=e==null?"## 留下真正想记住的词\n\n收藏解释，补上自己的理解，下次遇到就能找到。\n\n支持搜索术语、解释、上下文与笔记。":e.Explanation+(string.IsNullOrWhiteSpace(e.Note)?"":"\n\n---\n### 我的笔记\n"+e.Note)+(string.IsNullOrWhiteSpace(e.Context)?"":"\n\n---\n### 原文上下文\n"+e.Context);
            if(noMatch) { meta.Text="尝试缩短搜索词，或清空搜索查看全部收藏。"; text="搜索会检查术语、解释、原文上下文和个人笔记。\n\n也可以点击右上角 **＋ 新术语**，记下新的理解。"; }
            double width=Math.Max(240,Width-280); var document=ReadingTypography.Render(text,width,readingSettings);
            if(e!=null) {
                document.Blocks.Clear();
                if(!string.IsNullOrWhiteSpace(e.PreferredTranslation)) document.Blocks.Add(ReadingTypography.Card("我的常用译法",e.PreferredTranslation,width,readingSettings,"note"));
                document.Blocks.Add(ReadingTypography.Card("收藏解释",e.Explanation,width,readingSettings,"answer"));
                if(!string.IsNullOrWhiteSpace(e.Note)) document.Blocks.Add(ReadingTypography.Card("我的笔记",e.Note,width,readingSettings,"note"));
                if(!string.IsNullOrWhiteSpace(e.Context)) document.Blocks.Add(ReadingTypography.Card("原文上下文",e.Context,width,readingSettings,"context"));
            }
            preview.Document=document;
        }
        void Edit(TermEntry e) { if(editor!=null) { editor.Activate(); return; } editor=new TermEditor(store,e.Copy()); editor.Owner=this; editor.Saved+=delegate(TermEntry saved) { search.Clear(); Refresh(saved.Id); feedback.Text="已保存 · "+saved.Term; }; editor.Closed+=delegate { editor=null; }; editor.Show(); editor.Activate(); }
        void Copy() { if(Selection==null) return; try { Clipboard.SetText(Selection.Term+"\n\n"+Selection.Explanation+(string.IsNullOrWhiteSpace(Selection.Note)?"":"\n\n我的笔记：\n"+Selection.Note)); feedback.Text="已复制术语与解释"; } catch { feedback.Text="剪贴板忙，请稍后重试"; } }
        void Delete() { if(Selection==null) return; if(deleteId!=Selection.Id) { deleteId=Selection.Id; remove.Content="确认删除"; feedback.Text="再次点击确认删除；选择其他术语可取消。"; return; } try { store.Delete(deleteId); Refresh(null); feedback.Text="已删除收藏"; } catch(Exception e) { feedback.Text=e.Message; } }
    }
}
