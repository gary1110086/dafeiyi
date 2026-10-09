using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Markup;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;
using System.Globalization;
namespace LightTranslate {
    internal static class Ui {
        public static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(WhaleTheme.Color(hex)); }
        public static void Initialize(Application app) {
            WhaleTheme.Install(app);
            app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(WhaleTheme.Xaml(@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='Window'><Setter Property='FontFamily' Value='Microsoft YaHei UI, Segoe UI'/><Setter Property='Foreground' Value='#F4F1E8'/><Setter Property='FontSize' Value='13'/></Style>
<Style TargetType='Button'><Setter Property='Background' Value='#F1F3F8'/><Setter Property='Foreground' Value='#4D5870'/><Setter Property='Padding' Value='13,8'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FontSize' Value='12'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='Body' Background='{TemplateBinding Background}' CornerRadius='9' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Body' Property='Opacity' Value='0.78'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Body' Property='Opacity' Value='0.58'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='Body' Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='TextBox'><Setter Property='Foreground' Value='#F4F1E8'/><Setter Property='CaretBrush' Value='#8CD3DF'/><Setter Property='SelectionBrush' Value='#406982'/><Setter Property='SelectionTextBrush' Value='#FFFFFF'/><Setter Property='SelectionOpacity' Value='0.85'/><Setter Property='Background' Value='#F7F8FB'/><Setter Property='BorderBrush' Value='#E1E6EF'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='11,9'/><Setter Property='FontSize' Value='13'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9'><Grid><ScrollViewer x:Name='PART_ContentHost' Margin='0'/><TextBlock x:Name='Placeholder' Text='{TemplateBinding Tag}' Foreground='#9AA4B6' Margin='{TemplateBinding Padding}' IsHitTestVisible='False' Visibility='Collapsed'/></Grid></Border><ControlTemplate.Triggers><MultiTrigger><MultiTrigger.Conditions><Condition Property='Text' Value=''/><Condition Property='IsKeyboardFocused' Value='False'/></MultiTrigger.Conditions><Setter TargetName='Placeholder' Property='Visibility' Value='Visible'/></MultiTrigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ScrollBar'><Setter Property='Width' Value='7'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='{TemplateBinding Orientation}'><Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' IsTabStop='False'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='#D5DBE5' CornerRadius='3' Margin='1,0'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' IsTabStop='False'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style></ResourceDictionary>")));
        }
        public static TextBlock Text(string text,double size,string color) { return new TextBlock { Text=text,FontSize=size,Foreground=Brush(color),TextWrapping=TextWrapping.Wrap }; }
        public static Button Button(string text,Action action,bool primary) {
            var b=new Button { Content=ProductLanguage.T(text),Background=Brush("#F1F3F8"),Foreground=Brush("#4D5870") }; if(primary) { b.Background=Brush("#5864D8"); b.Foreground=Brush("#101C30"); }
            b.Click+=delegate { action(); }; return b;
        }
        public static Border Frame(Window window,UIElement child,int padding) {
            window.WindowStyle=WindowStyle.None; window.ResizeMode=ResizeMode.NoResize; window.AllowsTransparency=true; window.Background=Brushes.Transparent;
            var border=new Border { Background=Brush("#FEFEFF"),BorderBrush=Brush("#E5E9F2"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(20),Margin=new Thickness(12),Padding=new Thickness(padding),Child=child,
                Effect=new DropShadowEffect { Color=Color.FromRgb(35,43,74),BlurRadius=22,ShadowDepth=4,Opacity=0.15 } };
            AttachDrag(window,border); window.Content=border;
            border.LayoutUpdated+=delegate { CompactScrollbars(border); }; return border;
        }
        static void CompactScrollbars(DependencyObject root) {
            var bar=root as ScrollBar;
            if(bar!=null && bar.Orientation==Orientation.Vertical && (bar.MinWidth!=0 || bar.MaxWidth!=7)) {
                bar.MinWidth=0; bar.MaxWidth=7; bar.Width=7; bar.HorizontalAlignment=HorizontalAlignment.Right;
            }
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) CompactScrollbars(VisualTreeHelper.GetChild(root,i));
        }
        static DependencyObject Parent(DependencyObject element) {
            return element is Visual || element is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        internal static bool Interactive(DependencyObject hit,DependencyObject stop) {
            for(var element=hit;element!=null && element!=stop;element=Parent(element)) {
                if(element is ButtonBase || element is TextBoxBase || element is PasswordBox || element is ComboBox || element is Slider || element is ScrollBar || element is ScrollViewer || element is Hyperlink) return true;
            }
            return false;
        }
        static void AttachDrag(Window window,Border border) {
            border.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) {
                if(Interactive(e.OriginalSource as DependencyObject,border)) return;
                if(e.LeftButton!=MouseButtonState.Pressed) return;
                try { window.DragMove(); Native.Clamp(window); e.Handled=true; } catch(InvalidOperationException) { }
            };
        }
        public static void Row(Grid grid,UIElement element,int row) { Grid.SetRow(element,row); grid.Children.Add(element); }
        public static FrameworkElement Labelled(string title,UIElement field,string hint) {
            var stack=new StackPanel { Margin=new Thickness(0,0,0,14) }; stack.Children.Add(new TextBlock { Text=ProductLanguage.T(title),Foreground=Brush("#34405A"),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,7) }); stack.Children.Add(field);
            if(!string.IsNullOrEmpty(hint)) { var note=Text(ProductLanguage.T(hint),11,"#8690A3"); note.Margin=new Thickness(0,6,0,0); stack.Children.Add(note); } return stack;
        }
    }
    public class PopupView: Window {
        public string AnswerText="";
        public bool Pinned; public string CurrentMode="translate";
        string sourceValue="";
        public string SourceText { get { return sourceValue; } }
        public int HistoryCount { get { return recentItems.Count; } }
        internal ContextMenu HistoryMenu { get; private set; }
        public bool IsExpanded; bool customSize;
        ReadingBackdrop readingBackground;
        internal string BackgroundTheme { get { return readingBackground.Theme; } }
        Settings readingSettings=new Settings(); TermLookup lookup=new TermLookup(new List<TermEntry>());
        internal int ReadingFontSize { get { return readingSettings.ReadingFontSize; } }
        internal double ReadingLineSpacing { get { return readingSettings.ReadingLineSpacing; } }
        internal double ReadingShade { get { return readingBackground.FocusOpacity; } }
        public void SetAppearance(Settings settings) { readingSettings=settings.Copy(); readingBackground.Apply(settings); RenderSource(); RenderAnswer(answer.VerticalOffset); }
        public event Action<int,double> ReadingChanged;
        public event Action<TermEntry> TermClicked;
        public void SetTerms(IList<TermEntry> entries) { lookup=new TermLookup(entries); RenderSource(); RenderAnswer(answer.VerticalOffset); }
        public event Action<string> ModeRequested; public event Action<string> FollowRequested;
        public event Action DismissRequested,SettingsRequested,RetryRequested,TriggerModeRequested,ScreenRequested;
        public event Action WebsiteRecoveryRequested;
        StackPanel recovery; Button recoverWebsite;
        internal bool RecoveryVisible { get { return recovery!=null&&recovery.Visibility==Visibility.Visible; } }
        public event Action<string,string,bool> ServiceRequested;
        Settings serviceSettings=new Settings(); Button serviceButton;
        public event Action<CachedResult> RecentRequested;
        public event Action ClearHistoryRequested,FavoriteRequested,TermsRequested;
        public string SelectedAnswerText { get { return answer.Selection.Text.Trim(); } }
        TextBlock source,status,provider,termHint; RichTextBox answer; TextBox follow; Button translate,explain,pin,send,copy,expand,recent,trigger,favorite;
        Image sourcePreview; ScrollViewer sourceScroll; bool sourceExpanded;
        List<CachedResult> recentItems=new List<CachedResult>(); bool busy;
        public void ToggleExpanded() { customSize=false; IsExpanded=!IsExpanded; Width=IsExpanded?500:400; Height=IsExpanded?590:440; expand.Content=IsExpanded?"↙":"↗"; expand.ToolTip=IsExpanded?"收回紧凑窗口":"展开阅读 · Ctrl+E"; if(!busy) FitContent(); }
        public PopupView() {
            Title=ProductLanguage.T("大肥译 · 选中即理解"); Width=400; Height=440; ShowInTaskbar=false; Topmost=true; ShowActivated=false;
            var grid=new Grid(); for(int i=0;i<7;i++) grid.RowDefinitions.Add(new RowDefinition { Height=i==3?new GridLength(1,GridUnitType.Star):GridLength.Auto });
            var head=new Grid { Background=Brushes.Transparent,Margin=new Thickness(0,0,0,12) }; head.ColumnDefinitions.Add(new ColumnDefinition()); head.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var brand=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
            brand.Children.Add(WhaleTheme.Portrait(36));
            provider=Ui.Text(ProductLanguage.T("大肥译"),14,"#263047"); provider.Margin=new Thickness(0,0,0,0); provider.VerticalAlignment=VerticalAlignment.Center;
            serviceButton=Ui.Button("",ShowServiceMenu,false); serviceButton.Content=provider; serviceButton.Padding=new Thickness(4,4,4,4); serviceButton.Background=Brushes.Transparent; brand.Children.Add(serviceButton); head.Children.Add(brand);
            var tools=new StackPanel { Orientation=Orientation.Horizontal };
            pin=Ui.Button("",TogglePin,false); pin.ToolTip=ProductLanguage.T("固定浮窗 · Ctrl+P"); UpdatePin();
            expand=Ui.Button("↗",ToggleExpanded,false); expand.ToolTip=ProductLanguage.T("展开阅读 · Ctrl+E");
            var settings=Ui.Button("⚙",delegate { if(SettingsRequested!=null) SettingsRequested(); },false); settings.ToolTip=ProductLanguage.T("设置");
            var screen=Ui.Button("▣",delegate { if(ScreenRequested!=null) ScreenRequested(); },false); screen.ToolTip=ProductLanguage.T("屏幕识字 / 截图翻译 · Ctrl+Alt+S");
            var typography=Ui.Button("Aa",delegate { },false); typography.ToolTip=ProductLanguage.T("阅读排版 · 字号与行距"); typography.Click+=delegate { ShowReadingMenu(typography); };
            var close=Ui.Button("×",delegate { if(DismissRequested!=null) DismissRequested(); },false); close.ToolTip=ProductLanguage.T("关闭 · Esc");
            foreach(var b in new[]{pin,expand,typography,screen,settings,close}) { b.Width=27; b.Height=27; b.Padding=new Thickness(0); b.FontSize=b==typography?12:16; b.Margin=new Thickness(2,0,0,0); b.Background=Brushes.Transparent; tools.Children.Add(b); }
            Grid.SetColumn(tools,1); head.Children.Add(tools);
            Ui.Row(grid,head,0);
            source=Ui.Text(ProductLanguage.T("选中一段文字，开始理解。"),11,"#737F95");
            sourceScroll=new ScrollViewer { MaxHeight=42,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=source };
            var sourceStack=new StackPanel(); var sourceHead=new DockPanel { Margin=new Thickness(0,0,0,4) }; var sourceToggle=Ui.Button("⌄",delegate { sourceExpanded=!sourceExpanded; sourceScroll.MaxHeight=sourceExpanded?120:42; },false); sourceToggle.ToolTip=ProductLanguage.T("展开 / 收起原文"); sourceToggle.Padding=new Thickness(3,0,3,0); sourceToggle.Background=Brushes.Transparent; sourceToggle.Height=16; DockPanel.SetDock(sourceToggle,Dock.Right); sourceHead.Children.Add(sourceToggle);
            termHint=Ui.Text(ProductLanguage.T("选中原文"),10,"#8CD3DF"); termHint.VerticalAlignment=VerticalAlignment.Center; sourceHead.Children.Add(termHint); sourceStack.Children.Add(sourceHead); sourceStack.Children.Add(sourceScroll); sourcePreview=new Image { Height=74,Stretch=Stretch.Uniform,Visibility=Visibility.Collapsed,Margin=new Thickness(0,6,0,0) }; sourceStack.Children.Add(sourcePreview);
            var sourceBox=new Border { Background=Ui.Brush("#F4F6FA"),CornerRadius=new CornerRadius(9),Padding=new Thickness(10,7,10,8),Margin=new Thickness(0,0,0,10),Child=sourceStack }; Ui.Row(grid,sourceBox,1);
            var modes=new Grid { Margin=new Thickness(0,0,0,10) }; modes.ColumnDefinitions.Add(new ColumnDefinition()); modes.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var tabs=new StackPanel { Orientation=Orientation.Horizontal };
            translate=Ui.Button("翻译",delegate { if(ModeRequested!=null) ModeRequested("translate"); },true); explain=Ui.Button("解释",delegate { if(ModeRequested!=null) ModeRequested("explain"); },false);
            translate.MinWidth=58; explain.MinWidth=58; translate.Padding=explain.Padding=new Thickness(10,6,10,6); explain.Margin=new Thickness(5,0,0,0); tabs.Children.Add(translate); tabs.Children.Add(explain); modes.Children.Add(tabs);
            copy=Ui.Button("复制",delegate { CopyText(false); },false); copy.ToolTip=ProductLanguage.T("复制译文；右键可复制原文 / 双语"); copy.Background=Brushes.Transparent; copy.Padding=new Thickness(8,5,8,5);
            var copyMenu=new ContextMenu(); var copyAnswer=new MenuItem { Header=ProductLanguage.T("复制译文") }; copyAnswer.Click+=delegate { CopyText(false); }; copyMenu.Items.Add(copyAnswer);
            var copySource=new MenuItem { Header=ProductLanguage.T("复制原文") }; copySource.Click+=delegate { CopySource(); }; copyMenu.Items.Add(copySource);
            var copyBoth=new MenuItem { Header=ProductLanguage.T("复制双语 · Ctrl+Shift+C") }; copyBoth.Click+=delegate { CopyText(true); }; copyMenu.Items.Add(copyBoth);
            copyMenu.Items.Add(new Separator()); var retry=new MenuItem { Header=ProductLanguage.T("重新生成 · Ctrl+R") }; retry.Click+=delegate { if(RetryRequested!=null) RetryRequested(); }; copyMenu.Items.Add(retry); copy.ContextMenu=copyMenu;
            recent=Ui.Button("历史",ShowRecent,false); recent.ToolTip=ProductLanguage.T("最近 30 条历史记录 · 关闭后仍保留"); recent.Background=Brushes.Transparent; recent.Padding=new Thickness(8,5,8,5);
            favorite=Ui.Button("",delegate { if(FavoriteRequested!=null) FavoriteRequested(); },false); favorite.Content=MenuIcons.Create("bookmark"); favorite.ToolTip=ProductLanguage.T("收藏术语 · Ctrl+S；右键打开术语本"); favorite.Padding=new Thickness(7,5,7,5); favorite.Background=Brushes.Transparent;
            var favoriteMenu=new ContextMenu(); var terms=new MenuItem { Header=ProductLanguage.T("打开术语收藏"),Icon=MenuIcons.Create("bookmark") }; terms.Click+=delegate { if(TermsRequested!=null) TermsRequested(); }; favoriteMenu.Items.Add(terms); favorite.ContextMenu=favoriteMenu;
            var resultTools=new StackPanel { Orientation=Orientation.Horizontal }; resultTools.Children.Add(favorite); resultTools.Children.Add(recent); resultTools.Children.Add(copy); Grid.SetColumn(resultTools,1); modes.Children.Add(resultTools); Ui.Row(grid,modes,2);
            answer=new RichTextBox { IsReadOnly=true,IsDocumentEnabled=true,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Padding=new Thickness(0),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new FontFamily("Microsoft YaHei UI, Segoe UI"),FontSize=14,Foreground=Ui.Brush("#303B50") }; Ui.Row(grid,answer,3);
            var statusRow=new Grid { Margin=new Thickness(0,8,0,9) }; statusRow.ColumnDefinitions.Add(new ColumnDefinition()); statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            status=Ui.Text(ProductLanguage.T("由你的 DeepSeek API 提供"),10,"#939CAE"); status.VerticalAlignment=VerticalAlignment.Center;
            var statusContent=new StackPanel(); statusContent.Children.Add(status); recovery=new StackPanel { Orientation=Orientation.Horizontal,Visibility=Visibility.Collapsed,Margin=new Thickness(0,7,0,0) };
            recoverWebsite=Ui.Button("检查官网 / 登录",delegate { if(WebsiteRecoveryRequested!=null) WebsiteRecoveryRequested(); },false); recoverWebsite.FontSize=11; recovery.Children.Add(recoverWebsite);
            var retryRequest=Ui.Button("重试这次请求",delegate { if(RetryRequested!=null) RetryRequested(); },true); retryRequest.FontSize=11; retryRequest.Margin=new Thickness(7,0,0,0); recovery.Children.Add(retryRequest); statusContent.Children.Add(recovery); statusRow.Children.Add(statusContent);
            trigger=Ui.Button("点击模式",delegate { if(TriggerModeRequested!=null) TriggerModeRequested(); },false); trigger.Padding=new Thickness(7,3,7,3); trigger.FontSize=10; trigger.ToolTip=ProductLanguage.T("点击切换：小按钮 / 自动翻译 / 剪贴板 / 仅陪伴"); Grid.SetColumn(trigger,1); statusRow.Children.Add(trigger); Ui.Row(grid,statusRow,4);
            var followGrid=new Grid(); followGrid.ColumnDefinitions.Add(new ColumnDefinition()); followGrid.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            follow=new TextBox { ToolTip=ProductLanguage.T("继续追问当前内容，Enter 发送"),MinHeight=33,Padding=new Thickness(9,7,9,7),Text="",Tag=ProductLanguage.T("问问这段…  Enter 发送") }; followGrid.Children.Add(follow);
            send=Ui.Button("↑",SubmitFollow,true); send.Width=34; send.Padding=new Thickness(0); send.FontSize=17; send.ToolTip=ProductLanguage.T("追问 · Enter"); send.Margin=new Thickness(7,0,0,0); Grid.SetColumn(send,1); followGrid.Children.Add(send); Ui.Row(grid,followGrid,5);
            follow.KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift)) { SubmitFollow(); e.Handled=true; } };
            KeyDown+=delegate(object sender,KeyEventArgs e) {
                if(e.Key==Key.Escape && DismissRequested!=null) DismissRequested();
                if((Keyboard.Modifiers&ModifierKeys.Control)==0) return;
                if(e.Key==Key.P) { TogglePin(); e.Handled=true; }
                if(e.Key==Key.E) { ToggleExpanded(); e.Handled=true; }
                if(e.Key==Key.R && RetryRequested!=null) { RetryRequested(); e.Handled=true; }
                if(e.Key==Key.S&&!busy&&FavoriteRequested!=null) { FavoriteRequested(); e.Handled=true; }
                if(e.Key==Key.C && (Keyboard.Modifiers&ModifierKeys.Shift)!=0) { CopyText(true); e.Handled=true; }
                if(e.Key==Key.D1 && ModeRequested!=null) { ModeRequested("translate"); e.Handled=true; }
                if(e.Key==Key.D2 && ModeRequested!=null) { ModeRequested("explain"); e.Handled=true; }
            };
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e) { if(!Application.Current.Dispatcher.HasShutdownStarted) { e.Cancel=true; if(DismissRequested!=null) DismissRequested(); else Hide(); } };
            readingBackground=ReadingBackdrop.Wrap(grid); Ui.Frame(this,readingBackground,0); SizeChanged+=delegate(object sender,SizeChangedEventArgs e) { if(e.WidthChanged&&answer!=null&&AnswerText.Length>0) RenderAnswer(answer.VerticalOffset); if(IsVisible) Native.Clamp(this); }; SetAnswer(ProductLanguage.Interface=="en"?"Ready.\nSelect text, then click the button to translate or explain.":"准备好了。\n选中文字后点击小按钮，即可翻译或解释。");
            PopupResize.Attach(this,delegate { customSize=true; });
        }
        void TogglePin() { Pinned=!Pinned; UpdatePin(); }
        void UpdatePin() {
            pin.Content=new System.Windows.Shapes.Path { Data=System.Windows.Media.Geometry.Parse("M 3,2 L 11,2 L 10,6 L 13,10 L 1,10 L 4,6 Z M 7,10 L 7,15"),Stroke=Ui.Brush(Pinned?"#5864D8":"#7D879B"),StrokeThickness=1.2,Fill=Pinned?Ui.Brush("#E9ECFF"):Brushes.Transparent,Width=14,Height=16,Stretch=Stretch.Uniform };
            pin.ToolTip=Pinned?"取消固定 · Ctrl+P":"固定浮窗 · Ctrl+P";
        }
        public void CopyText(bool bilingual) {
            if(busy || string.IsNullOrWhiteSpace(AnswerText)) return;
            try { Clipboard.SetText(bilingual?sourceValue+"\n\n"+AnswerText:AnswerText); status.Text=bilingual?"已复制双语":"已复制译文"; } catch { status.Text="剪贴板忙，请稍后再试"; }
        }
        void CopySource() { try { Clipboard.SetText(sourceValue); status.Text="已复制原文"; } catch { status.Text="剪贴板忙，请稍后再试"; } }
        public void SetRecent(IList<CachedResult> entries) { recentItems=new List<CachedResult>(entries); }
        public void ShowRecent() {
            var menu=new ContextMenu { MaxHeight=430,MaxWidth=440 }; HistoryMenu=menu;
            menu.Template=(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ContextMenu'><Border Background='#17263E' BorderBrush='#46617E' BorderThickness='1' CornerRadius='13' Padding='5'><ScrollViewer MaxHeight='410' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled'><StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Cycle'/></ScrollViewer></Border></ControlTemplate>");
            menu.Items.Add(new MenuItem { Header=recentItems.Count==0?"还没有历史记录":"最近 "+recentItems.Count+" 条 · 点选即可重看",IsEnabled=false });
            foreach(var result in recentItems) {
                var saved=result; string text=saved.Source.Replace('\n',' ').Replace('\r',' '); if(text.Length>28) text=text.Substring(0,28)+"…";
                DateTime time; string stamp=DateTime.TryParse(saved.CreatedUtc,null,DateTimeStyles.RoundtripKind,out time)?time.ToLocalTime().ToString("MM-dd HH:mm")+"  ":"";
                var item=new MenuItem { Header=new TextBlock { Text=stamp+(saved.Mode=="explain"?"解释  ":"翻译  ")+text,MaxWidth=355,TextTrimming=TextTrimming.CharacterEllipsis },ToolTip=new TextBlock { Text=saved.Source,MaxWidth=350,MaxHeight=240,TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis } }; item.Click+=delegate { if(RecentRequested!=null) RecentRequested(saved); }; menu.Items.Add(item);
            }
            menu.Items.Add(new Separator()); var clear=new MenuItem { Header=ProductLanguage.T("清空历史记录"),IsEnabled=recentItems.Count>0 };
            clear.Click+=delegate { if(ClearHistoryRequested!=null) ClearHistoryRequested(); }; menu.Items.Add(clear);
            menu.PlacementTarget=recent; menu.Placement=PlacementMode.Bottom; menu.IsOpen=true;
        }
        public void SetTriggerMode(string mode) { trigger.Content=mode=="companion"?"陪伴模式":mode=="clipboard"?"剪贴板模式":mode=="auto"?"自动模式":"点击模式"; trigger.Content=ProductLanguage.T(Convert.ToString(trigger.Content)); trigger.Foreground=Ui.Brush(mode!="button"?"#5864D8":"#7F8A9F"); }
        void FitContent() {
            if(IsExpanded || customSize || busy) return;
            var formatted=new FormattedText(AnswerText,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(answer.FontFamily,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),ReadingFontSize,Brushes.Black,VisualTreeHelper.GetDpi(this).PixelsPerDip);
            formatted.MaxTextWidth=Math.Max(250,Width-62); formatted.LineHeight=ReadingFontSize*ReadingLineSpacing;
            Height=Math.Max(330,Math.Min(440,formatted.Height+230));
        }
        void SubmitFollow() { if(send.IsEnabled && !string.IsNullOrWhiteSpace(follow.Text) && FollowRequested!=null) { string q=follow.Text.Trim(); follow.Clear(); FollowRequested(q); } }
        internal void SetImage(ImageRequest image) { sourcePreview.Visibility=image==null?Visibility.Collapsed:Visibility.Visible; sourcePreview.Source=null; if(image!=null) using(var stream=new System.IO.MemoryStream(image.Png)) { var b=new System.Windows.Media.Imaging.BitmapImage(); b.BeginInit(); b.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; b.StreamSource=stream; b.EndInit(); sourcePreview.Source=b; } }
        public void SetSource(string text) { SetImage(null); if(sourceValue!=text) { sourceExpanded=false; sourceScroll.MaxHeight=42; } sourceValue=text??""; RenderSource(); }
        void RenderSource() { source.Text=sourceValue; source.FontSize=Math.Max(11,ReadingFontSize-2); lookup.Decorate(source.Inlines,OnTerm); int matches=lookup.Find(sourceValue).Count; termHint.Text=ProductLanguage.T(matches>0?"选中原文 · 下划线可看收藏":"选中原文"); }
        void OnTerm(TermEntry entry) { if(TermClicked!=null) TermClicked(entry); }
        internal void SetReadingStyle(int font,double spacing) {
            readingSettings.ReadingFontSize=Math.Max(12,Math.Min(20,font)); readingSettings.ReadingLineSpacing=Math.Max(1.4,Math.Min(2,spacing)); RenderSource(); RenderAnswer(answer.VerticalOffset); FitContent(); if(ReadingChanged!=null) ReadingChanged(ReadingFontSize,ReadingLineSpacing);
        }
        void ShowReadingMenu(Button anchor) {
            var menu=new ContextMenu(); menu.Items.Add(new MenuItem { Header=ProductLanguage.T("阅读排版 · 自动保存"),IsEnabled=false }); var fonts=new MenuItem { Header=ProductLanguage.T("字号") };
            foreach(int value in new[]{12,14,16,18,20}) { int size=value; var item=new MenuItem { Header=size+" px",IsCheckable=true,IsChecked=size==ReadingFontSize }; item.Click+=delegate { SetReadingStyle(size,ReadingLineSpacing); }; fonts.Items.Add(item); } menu.Items.Add(fonts);
            var lines=new MenuItem { Header=ProductLanguage.T("行距") }; double[] values={1.45,23.0/14.0,1.9}; string[] labels={"紧凑","舒适","宽松"}; for(int i=0;i<values.Length;i++) { double space=values[i]; var item=new MenuItem { Header=labels[i],IsCheckable=true,IsChecked=Math.Abs(space-ReadingLineSpacing)<0.06 }; item.Click+=delegate { SetReadingStyle(ReadingFontSize,space); }; lines.Items.Add(item); } menu.Items.Add(lines);
            menu.Items.Add(new Separator()); var reset=new MenuItem { Header=ProductLanguage.T("恢复默认排版") }; reset.Click+=delegate { SetReadingStyle(14,23.0/14.0); }; menu.Items.Add(reset); menu.PlacementTarget=anchor; menu.Placement=PlacementMode.Bottom; menu.IsOpen=true;
        }
        public void SetProvider(string text) { provider.ToolTip=text; }
        public void SetService(Settings settings) { serviceSettings=settings.Copy(); provider.Text=settings.Service=="web"?ProductLanguage.T("官网账号 ⌄"):settings.Model=="deepseek-flash"?"Flash ⌄":settings.Model=="deepseek-v4-pro"?"Pro ⌄":"DeepSeek ⌄"; serviceButton.ToolTip=ServiceProfile.Label(settings)+" · 点击切换"; }
        void ShowServiceMenu() {
            var menu=new ContextMenu();
            var note=new MenuItem { Header=ProductLanguage.T("下一次请求使用"),IsEnabled=false }; menu.Items.Add(note);
            menu.Items.Add(new MenuItem { Header=ProductLanguage.T("API 模型 · 使用 API Key"),IsEnabled=false });
            foreach(string name in new[]{"deepseek-flash","deepseek-v4-pro",serviceSettings.Model}) {
                bool duplicate=false; foreach(object item in menu.Items) { var m=item as MenuItem; if(m!=null&&Convert.ToString(m.Tag)==name) duplicate=true; } if(duplicate) continue;
                string modelName=name; var choice=new MenuItem { Header=name=="deepseek-flash"?"Flash · 日常阅读":name=="deepseek-v4-pro"?"Pro · 复杂解释":name,Tag=name,IsCheckable=true,IsChecked=serviceSettings.Service=="api"&&serviceSettings.Model==name };
                choice.Click+=delegate { if(ServiceRequested!=null) ServiceRequested("api",modelName,serviceSettings.Thinking); }; menu.Items.Add(choice);
            }
            menu.Items.Add(new Separator());
            foreach(bool thinking in new[]{false,true}) { bool value=thinking; var item=new MenuItem { Header=thinking?"深度思考":"标准 · 快速回答",IsCheckable=true,IsChecked=serviceSettings.Thinking==thinking,IsEnabled=serviceSettings.Service=="web"||ServiceProfile.SupportsThinking(serviceSettings.Model) }; item.Click+=delegate { if(ServiceRequested!=null) ServiceRequested(serviceSettings.Service,serviceSettings.Model,value); }; menu.Items.Add(item); }
            menu.Items.Add(new Separator()); var web=new MenuItem { Header=ProductLanguage.T("DeepSeek 官网 · 登录账号"),IsCheckable=true,IsChecked=serviceSettings.Service=="web" }; web.Click+=delegate { if(ServiceRequested!=null) ServiceRequested("web",serviceSettings.Model,serviceSettings.Thinking); }; menu.Items.Add(web);
            var settingsItem=new MenuItem { Header=ProductLanguage.T("管理连接…"),Icon=MenuIcons.Create("settings") }; settingsItem.Click+=delegate { if(SettingsRequested!=null) SettingsRequested(); }; menu.Items.Add(settingsItem);
            menu.PlacementTarget=serviceButton; menu.IsOpen=true;
        }
        public void SetBusy(bool isBusy,string message) { busy=isBusy; status.Text=ProductLanguage.T(message); send.IsEnabled=!busy; copy.IsEnabled=!busy; favorite.IsEnabled=!busy; if(busy && !IsExpanded) Height=Math.Max(360,Height); if(!busy) FitContent(); }
        internal void SetRecovery(bool visible,bool website) { recovery.Visibility=visible?Visibility.Visible:Visibility.Collapsed; recoverWebsite.Visibility=website?Visibility.Visible:Visibility.Collapsed; }
        public void SelectMode(string mode) {
            CurrentMode=mode; bool t=mode=="translate";
            translate.Background=Ui.Brush(t?"#5864D8":"#F1F3F8"); translate.Foreground=Ui.Brush(t?"#FFFFFF":"#4D5870");
            explain.Background=Ui.Brush(!t?"#5864D8":"#F1F3F8"); explain.Foreground=Ui.Brush(!t?"#FFFFFF":"#4D5870");
        }
        public void SetAnswer(string text) {
            string next=text??""; double offset=busy&&AnswerText.Length>0&&next.StartsWith(AnswerText,StringComparison.Ordinal)?answer.VerticalOffset:0;
            AnswerText=next;
            RenderAnswer(offset);
            if(!busy) FitContent();
        }
        void RenderAnswer(double offset) {
            answer.Document=ReadingTypography.Render(AnswerText,Width-70,readingSettings); lookup.Decorate(answer.Document,OnTerm); readingBackground.SetReadingLoad(AnswerText,IsExpanded);
            answer.UpdateLayout(); answer.ScrollToVerticalOffset(offset);
        }
    }
    public class ChipView: Window {
        public event Action<string> Requested;
        Border surface,grip; bool pressed; Point origin; Rect initial;
        internal bool Interacting { get { return pressed; } }
        internal Point DragPoint { get { return grip.PointToScreen(new Point(grip.ActualWidth/2,grip.ActualHeight/2)); } }
        internal Rect HoverBounds { get { var point=surface.PointToScreen(new Point()); var dpi=VisualTreeHelper.GetDpi(this); return new Rect(point.X,point.Y,surface.ActualWidth*dpi.DpiScaleX,surface.ActualHeight*dpi.DpiScaleY); } }
        public ChipView() {
            Width=166; Height=62; ShowInTaskbar=false; Topmost=true; ShowActivated=false; Title=ProductLanguage.T("大肥译 · 划词");
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; AllowsTransparency=true; Background=Brushes.Transparent;
            SourceInitialized+=delegate { Native.MakeNonActivating(this); };
            var stack=new StackPanel { Orientation=Orientation.Horizontal };
            grip=new Border { Width=26,Height=34,Cursor=Cursors.SizeAll,Background=Brushes.Transparent,ToolTip=ProductLanguage.T("按住这里移动 · Esc 收起"),Child=new TextBlock { Text="⠿",FontSize=18,Foreground=Ui.Brush("#9DAFC7"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false } }; stack.Children.Add(grip);
            var t=Ui.Button("翻译",delegate { if(Requested!=null) Requested("translate"); },true); t.ToolTip=ProductLanguage.T("翻译选中文字");
            var explanationButton=Ui.Button("解释",delegate { if(Requested!=null) Requested("explain"); },false); explanationButton.ToolTip=ProductLanguage.T("解释选中文字"); explanationButton.Margin=new Thickness(3,0,0,0);
            foreach(var b in new[]{t,explanationButton}) { b.Width=48; b.Height=32; b.Padding=new Thickness(0); b.FontSize=12; b.VerticalAlignment=VerticalAlignment.Center; stack.Children.Add(b); }
            surface=new Border { Background=Ui.Brush("#17263E"),BorderBrush=Ui.Brush("#42607D"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(13),Margin=new Thickness(10),Padding=new Thickness(4,3,5,3),Child=stack,Effect=new DropShadowEffect { BlurRadius=12,ShadowDepth=2,Opacity=0.2 } }; Content=surface;
            grip.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) { origin=Native.Cursor(); initial=Native.Bounds(this); pressed=true; grip.CaptureMouse(); e.Handled=true; };
            grip.MouseMove+=delegate(object sender,MouseEventArgs e) { if(!pressed) return; var delta=Native.Cursor()-origin; Native.MoveWithoutActivation(this,initial.X+delta.X,initial.Y+delta.Y); e.Handled=true; };
            grip.MouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e) { pressed=false; grip.ReleaseMouseCapture(); Native.Clamp(this); e.Handled=true; };
            grip.LostMouseCapture+=delegate { pressed=false; }; IsVisibleChanged+=delegate { if(!IsVisible&&pressed) { pressed=false; grip.ReleaseMouseCapture(); } };
        }
    }
    public class SettingsView: Window {
        readonly Dictionary<string,ScrollViewer> sections=new Dictionary<string,ScrollViewer>(); readonly Dictionary<string,Button> navigation=new Dictionary<string,Button>();
        RadioButton apiMode,webMode; CheckBox thinking; StackPanel apiPage; Border webInfo; TextBlock sectionTitle,sectionHint; bool connectionBusy; Button testConnection,loadModels;
        public event Action WebRequested;
        internal Action BrowserOpener=WebsiteBrowser.Open;
        internal Func<Task<WebsiteState>> WebChecker;
        TextBlock connectionSummary,websiteStatus;
        internal string CurrentSection { get; private set; }
        internal void SelectSection(string name) { if(!sections.ContainsKey(name)) return; CurrentSection=name; foreach(var p in sections) p.Value.Visibility=p.Key==name?Visibility.Visible:Visibility.Collapsed; foreach(var b in navigation) { b.Value.Background=Ui.Brush(b.Key==name?"#294760":"#17263E"); b.Value.Foreground=Ui.Brush(b.Key==name?"#8CD3DF":"#AEBFD3"); } sectionTitle.Text=ProductLanguage.T(name); sectionHint.Text=name=="使用指南"?"从第一次划词，到顺手阅读。":name=="连接与模型"?"选择官网账号或 API，随时切换。":name=="划词与快捷键"?"决定什么时候出现、什么时候翻译。":name=="阅读外观"?"让内容清楚，让肥鱼柔和地陪你。":name=="更新与关于"?"更新程序，保留你的阅读积累。":"大小、动作和贴边探头，都在这里。"; sectionHint.Text=ProductLanguage.T(sectionHint.Text); UpdateConnection(); }
        void OpenDemo() { var demo=new WelcomeView(delegate { SelectSection("连接与模型"); Activate(); },delegate { companionMode.IsChecked=true; SelectSection("划词与快捷键"); Activate(); }); demo.Owner=this; demo.Show(); }
        ComboBox interfaceLanguage,targetLanguage,backgroundChoice,visionModel; Slider backgroundStrength; ReadingBackdrop backgroundPreview; string customBackground;
        PasswordBox key; TextBox url,translation,explanation,copyApps; CheckBox copyEnabled,resident,reduced,edgeHide,hideCaption,termPreferences; ComboBox model,petSize,readingFont,readingSpacing; RadioButton buttonMode,autoMode,clipboardMode,companionMode; Slider delay; TextBlock feedback; Settings original; Action<Settings> save; CancellationTokenSource network=new CancellationTokenSource();
        public bool HasRequiredFields { get { return key!=null&&url!=null&&model!=null&&buttonMode!=null&&autoMode!=null; } }
        public SettingsView(Settings settings,Action<Settings> onSave) {
            original=settings.Copy(); save=onSave; Title=ProductLanguage.T("大肥译设置"); Width=730; Height=700; WindowStartupLocation=WindowStartupLocation.CenterScreen;
            var grid=new Grid(); grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            var heading=new Grid { Background=Brushes.Transparent,Margin=new Thickness(0,0,0,16) }; heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var name=new StackPanel { Orientation=Orientation.Horizontal }; name.Children.Add(WhaleTheme.Portrait(42)); var brand=new StackPanel(); brand.Children.Add(Ui.Text(ProductLanguage.T("大肥译"),22,"#263047")); var sub=Ui.Text(ProductLanguage.T("读懂一点，陪你久一点"),11,"#929CAE"); sub.Margin=new Thickness(0,3,0,0); brand.Children.Add(sub); name.Children.Add(brand); heading.Children.Add(name);
            var close=Ui.Button("×",Close,false); close.FontSize=19; close.Background=Brushes.Transparent; close.VerticalAlignment=VerticalAlignment.Top; Grid.SetColumn(close,1); heading.Children.Add(close); Ui.Row(grid,heading,0);
            var readingPage=new StackPanel(); var triggerPage=new StackPanel(); var petPage=new StackPanel(); apiPage=new StackPanel(); var servicePage=new StackPanel(); var form=readingPage;
            customBackground=original.BackgroundPath;
            backgroundChoice=new ComboBox { MinWidth=200,SelectedIndex=original.BackgroundTheme=="angry"?1:original.BackgroundTheme=="custom"?2:original.BackgroundTheme=="none"?3:0 };
            backgroundChoice.Items.Add(ProductLanguage.T("正在思考 · 原来的肥鱼")); backgroundChoice.Items.Add(ProductLanguage.T("气鼓鼓 · 举手肥鱼")); backgroundChoice.Items.Add(ProductLanguage.T("自选图片")); backgroundChoice.Items.Add(ProductLanguage.T("纯色 · 专注阅读"));
            backgroundStrength=new Slider { Minimum=30,Maximum=100,Value=original.BackgroundStrength,Width=200,TickFrequency=5,IsSnapToTickEnabled=true };
            var previewText=Ui.Text(ProductLanguage.T("先看懂，\n再往下读。"),16,"#303B50"); previewText.HorizontalAlignment=HorizontalAlignment.Left; previewText.VerticalAlignment=VerticalAlignment.Center; previewText.FontWeight=FontWeights.SemiBold;
            var previewContent=new Grid(); previewContent.Children.Add(previewText);
            backgroundPreview=new ReadingBackdrop(previewContent) { Height=145,Margin=new Thickness(0,9,0,8),Background=Ui.Brush("#FEFEFF") };
            var appearance=new StackPanel(); var choiceRow=new StackPanel { Orientation=Orientation.Horizontal }; choiceRow.Children.Add(backgroundChoice);
            var chooseImage=Ui.Button("选择图片…",ChooseBackground,false); chooseImage.Margin=new Thickness(8,0,0,0); choiceRow.Children.Add(chooseImage); appearance.Children.Add(choiceRow); appearance.Children.Add(backgroundPreview);
            var strengthRow=new StackPanel { Orientation=Orientation.Horizontal }; strengthRow.Children.Add(Ui.Text(ProductLanguage.T("背景浓度"),11,"#929CAE")); backgroundStrength.Margin=new Thickness(14,0,0,0); strengthRow.Children.Add(backgroundStrength); appearance.Children.Add(strengthRow);
            backgroundChoice.SelectionChanged+=delegate { UpdateBackgroundPreview(); }; backgroundStrength.ValueChanged+=delegate { UpdateBackgroundPreview(); }; UpdateBackgroundPreview();
            form.Children.Add(new Expander { Header=ProductLanguage.T("阅读背景 · 点击展开更换"),Content=appearance,Margin=new Thickness(0,0,0,15),IsExpanded=false });
            var readingOptions=new StackPanel(); var readingRow=new StackPanel { Orientation=Orientation.Horizontal };
            readingFont=new ComboBox { Width=110,SelectedIndex=Math.Max(0,Math.Min(4,(original.ReadingFontSize-12)/2)),Margin=new Thickness(0,0,12,0) }; foreach(int size in new[]{12,14,16,18,20}) readingFont.Items.Add(size+" px");
            readingSpacing=new ComboBox { Width=120,SelectedIndex=original.ReadingLineSpacing<1.55?0:original.ReadingLineSpacing>1.8?2:1 }; readingSpacing.Items.Add(ProductLanguage.T("紧凑行距")); readingSpacing.Items.Add(ProductLanguage.T("舒适行距")); readingSpacing.Items.Add(ProductLanguage.T("宽松行距")); readingRow.Children.Add(readingFont); readingRow.Children.Add(readingSpacing); readingOptions.Children.Add(Ui.Labelled("字号与行距",readingRow,"结果窗口 Aa 也能直接调整；长文自动加强文字遮罩。"));
            termPreferences=new CheckBox { Content=ProductLanguage.T("翻译时参考我保存的常用译法"),IsChecked=original.UseTermPreferences,Margin=new Thickness(0,0,0,8) }; readingOptions.Children.Add(termPreferences); readingOptions.Children.Add(Ui.Text(ProductLanguage.T("只附加本段匹配的术语与常用译法；收藏解释和个人笔记保持本地。"),11,"#AEBFD3")); form.Children.Add(new Expander { Header=ProductLanguage.T("阅读排版与术语助手"),Content=readingOptions,Margin=new Thickness(0,0,0,15) });
            form=triggerPage;
            var modeGrid=new Grid(); modeGrid.ColumnDefinitions.Add(new ColumnDefinition()); modeGrid.ColumnDefinitions.Add(new ColumnDefinition());
            buttonMode=new RadioButton { GroupName="trigger",IsChecked=original.Mode=="button",Content=new StackPanel { Children={Ui.Text(ProductLanguage.T("点击小按钮"),14,"#34405A"),Ui.Text(ProductLanguage.T("选中后出现「译 / 解」，点击才请求"),11,"#929CAE")} },Margin=new Thickness(0,4,10,9) };
            autoMode=new RadioButton { GroupName="trigger",IsChecked=original.Mode=="auto",Content=new StackPanel { Children={Ui.Text(ProductLanguage.T("自动显示结果"),14,"#34405A"),Ui.Text(ProductLanguage.T("选中后稍作停顿，自动开始翻译"),11,"#929CAE")} },Margin=new Thickness(0,4,0,9) }; Grid.SetColumn(autoMode,1); modeGrid.Children.Add(buttonMode); modeGrid.Children.Add(autoMode);
            clipboardMode=new RadioButton { GroupName="trigger",IsChecked=original.Mode=="clipboard",Content=new StackPanel { Children={Ui.Text(ProductLanguage.T("剪贴板自动翻译"),14,"#34405A"),Ui.Text(ProductLanguage.T("复制新文字后自动显示译文；忽略本工具复制的内容"),11,"#929CAE")} },Margin=new Thickness(0,4,0,9) };
            var modeChoices=new StackPanel(); modeChoices.Children.Add(modeGrid); modeChoices.Children.Add(clipboardMode); companionMode=new RadioButton { GroupName="trigger",IsChecked=original.Mode=="companion",Content=ProductLanguage.T("仅陪伴 · 关闭划词与剪贴板自动翻译"),Margin=new Thickness(0,4,0,9) }; modeChoices.Children.Add(companionMode);
            form.Children.Add(Ui.Labelled("触发方式",modeChoices,"点击才翻译，或选择自动模式；官网和 API 都在肥鱼浮窗显示回答。"));
            form=petPage;
            resident=new CheckBox { Content=ProductLanguage.T("桌面常驻大肥鱼娘"),IsChecked=original.ResidentOrb,Margin=new Thickness(0,4,0,2) };
            form.Children.Add(Ui.Labelled("桌面伴侣",resident,"单击任何部位或右键打开模式与功能；按住拖动放置。互动动作也可从菜单中选择。"));
            petSize=new ComboBox { SelectedIndex=original.PetSize<160?0:original.PetSize>192?2:1,Width=140,HorizontalAlignment=HorizontalAlignment.Left }; petSize.Items.Add(ProductLanguage.T("小巧")); petSize.Items.Add(ProductLanguage.T("标准")); petSize.Items.Add(ProductLanguage.T("大一点"));
            reduced=new CheckBox { Content=ProductLanguage.T("减少动态"),IsChecked=original.ReducedMotion,Margin=new Thickness(16,0,0,0),VerticalAlignment=VerticalAlignment.Center };
            var petOptions=new StackPanel { Orientation=Orientation.Horizontal }; petOptions.Children.Add(petSize); petOptions.Children.Add(reduced); form.Children.Add(Ui.Labelled("大小与动态",petOptions,"减少动态会保留状态表情，并停止连续动画和空闲小动作。"));
            edgeHide=new CheckBox { Content=ProductLanguage.T("贴边探头：拖到左右屏幕边缘后自动收起，鼠标靠近出来"),IsChecked=original.EdgeHide,Margin=new Thickness(0,0,0,7) };
            hideCaption=new CheckBox { Content=ProductLanguage.T("空闲时隐藏状态文字，鼠标靠近或工作时再显示"),IsChecked=original.HideIdleCaption,Margin=new Thickness(0,0,0,14) }; form.Children.Add(edgeHide); form.Children.Add(hideCaption);
            form=triggerPage;
            copyEnabled=new CheckBox { Content=ProductLanguage.T("对指定软件启用复制取词"),IsChecked=original.CopyFallbackEnabled,Margin=new Thickness(0,4,0,10) };
            copyApps=new TextBox { Text=original.CopyApplications,Tag=ProductLanguage.T("Obsidian, Acrobat, msedge"),ToolTip=ProductLanguage.T("填写进程名称，逗号分隔，可带 .exe") };
            var compatibility=new StackPanel(); compatibility.Children.Add(copyEnabled); compatibility.Children.Add(Ui.Labelled("软件进程名称",copyApps,"直接读不到选区时尝试 Ctrl+C，会更新剪贴板。只对这里列出的软件启用。"));
            compatibility.Children.Add(Ui.Text(ProductLanguage.T("屏幕识字：Ctrl+Alt+S，可拖选图片中的文字，Shift+拖动框选。识别原文可修改；仅把选中文字交给所选模型。\n本机 OCR 语言：")+OcrService.LanguageSummary(),11,"#8690A3"));
            form.Children.Add(new Expander { Header=ProductLanguage.T("取词兼容与屏幕识字"),Content=compatibility,Margin=new Thickness(0,0,0,15) });
            delay=new Slider { Minimum=200,Maximum=1500,Value=original.AutoDelay,TickFrequency=50,IsSnapToTickEnabled=true,Width=230,HorizontalAlignment=HorizontalAlignment.Left };
            var delayRow=new StackPanel { Orientation=Orientation.Horizontal }; var ms=Ui.Text(original.AutoDelay+" ms",11,"#8690A3"); ms.Margin=new Thickness(13,0,0,0); delay.ValueChanged+=delegate { ms.Text=((int)delay.Value)+" ms"; }; delayRow.Children.Add(delay); delayRow.Children.Add(ms);
            form.Children.Add(Ui.Labelled("自动模式等待时间",delayRow,null));
            connectionSummary=Ui.Text(ProductLanguage.T(""),12,"#AEBFD3"); servicePage.Children.Add(new Border { Background=Ui.Brush("#20364D"),CornerRadius=new CornerRadius(12),Padding=new Thickness(14),Margin=new Thickness(0,0,0,14),Child=connectionSummary });
            apiMode=new RadioButton { GroupName="service",Content=ProductLanguage.T("API · 使用你的 API Key"),IsChecked=original.Service!="web",Margin=new Thickness(0,9,0,12) }; webMode=new RadioButton { GroupName="service",Content=ProductLanguage.T("官网账号 · 回答仍显示在肥鱼浮窗"),IsChecked=original.Service=="web",Margin=new Thickness(0,0,0,16) }; servicePage.Children.Add(apiMode); servicePage.Children.Add(webMode);
            form=apiPage;
            key=new PasswordBox { Password=original.ApiKey,Padding=new Thickness(11,9,11,9),FontSize=13,Background=Ui.Brush("#F7F8FB"),BorderBrush=Ui.Brush("#E1E6EF"),BorderThickness=new Thickness(1) };
            form.Children.Add(Ui.Labelled("API Key",key,"凭证由 Windows 加密，只保存到这台电脑的当前用户账户。"));
            url=new TextBox { Text=original.BaseUrl }; form.Children.Add(Ui.Labelled("接口地址",url,"官方：https://api.deepseek.com，也支持兼容服务的 /v1 地址。"));
            model=new ComboBox { IsEditable=true,Text=original.Model,Padding=new Thickness(9,7,9,7),MinHeight=36 };
            model.Items.Add(ProductLanguage.T("deepseek-flash")); model.Items.Add(ProductLanguage.T("deepseek-v4-pro")); model.Items.Add(ProductLanguage.T("deepseek-chat"));
            var modelGrid=new Grid(); modelGrid.ColumnDefinitions.Add(new ColumnDefinition()); modelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto }); modelGrid.Children.Add(model);
            loadModels=Ui.Button("读取模型",async delegate { await LoadModels(); },false); loadModels.Margin=new Thickness(9,0,0,0); Grid.SetColumn(loadModels,1); modelGrid.Children.Add(loadModels); form.Children.Add(Ui.Labelled("模型",modelGrid,"Flash 适合日常阅读；Pro 可用于复杂解释。也可填写兼容服务模型。"));
            thinking=new CheckBox { Content=ProductLanguage.T("深度思考 · 适合复杂问题，等待可能更久"),IsChecked=original.Thinking,Margin=new Thickness(0,0,0,14) }; form.Children.Add(thinking); model.SelectionChanged+=delegate { thinking.IsEnabled=ServiceProfile.SupportsThinking(model.Text); }; thinking.IsEnabled=ServiceProfile.SupportsThinking(model.Text);
            visionModel=new ComboBox { IsEditable=true,Text=original.VisionModel,MinHeight=36 }; visionModel.Items.Add(ProductLanguage.T("deepseek-flash")); form.Children.Add(Ui.Labelled("图片理解模型",visionModel,"框图与公式使用此模型。DeepSeek 官方 Flash 支持图片；Pro 不支持。图片请求使用当前 API Key 并按个人账号计费。"));
            servicePage.Children.Add(apiPage);
            var webContent=new StackPanel(); webContent.Children.Add(Ui.Text(ProductLanguage.T("浮窗翻译 · 应用内登录"),16,"#8CD3DF")); var webDescription=Ui.Text(ProductLanguage.T("在应用内官网登录并返回浮窗，即可划词翻译。账号过期或需要验证时，再打开登录窗口。"),12,"#AEBFD3"); webDescription.Margin=new Thickness(0,10,0,15); webContent.Children.Add(webDescription);
            var webThinking=new CheckBox { Content=ProductLanguage.T("深度思考 · 适合复杂解释"),IsChecked=original.Thinking,Foreground=Ui.Brush("#DEE8F4"),Margin=new Thickness(0,0,0,14) }; webThinking.Checked+=delegate { thinking.IsChecked=true; }; webThinking.Unchecked+=delegate { thinking.IsChecked=false; }; thinking.Checked+=delegate { webThinking.IsChecked=true; }; thinking.Unchecked+=delegate { webThinking.IsChecked=false; }; webContent.Children.Add(webThinking);
            websiteStatus=Ui.Text(ProductLanguage.T("尚未检查 · 使用应用内的官网窗口登录，外部浏览器登录不会同步到这里。"),11,"#AEBFD3"); websiteStatus.Margin=new Thickness(0,0,0,12); webContent.Children.Add(websiteStatus);
            var login=Ui.Button("使用官网账号 / 登录",delegate { var connection=original.Copy(); connection.Service="web"; connection.Thinking=thinking.IsChecked==true; try { save(connection); original=connection; if(WebRequested!=null) WebRequested(); } catch(Exception e) { feedback.Text=e.Message; } },true); login.HorizontalAlignment=HorizontalAlignment.Left; webContent.Children.Add(login);
            var flow=Ui.Text(ProductLanguage.T("浮窗翻译：在上面的应用内窗口登录 → 检查连接 → 完成登录并返回。关闭账号窗口后登录会保留。"),11,"#AEBFD3"); flow.Margin=new Thickness(0,10,0,12); webContent.Children.Add(flow);
            var outside=new StackPanel(); outside.Children.Add(Ui.Text(ProductLanguage.T("浏览器辅助 · 独立使用"),12,"#DEE8F4"));
            var browserButton=Ui.Button("在 Chrome / 浏览器打开官网",delegate { try { BrowserOpener(); feedback.Text=ProductLanguage.T("已请求打开浏览器；浮窗翻译仍需在应用内登录。"); } catch(Exception) { feedback.Text=ProductLanguage.T("浏览器未能打开，可手动访问 https://chat.deepseek.com/"); } },false); browserButton.HorizontalAlignment=HorizontalAlignment.Left; browserButton.Margin=new Thickness(0,8,0,8); outside.Children.Add(browserButton);
            outside.Children.Add(Ui.Text(ProductLanguage.T("优先打开 Chrome，未安装时使用默认浏览器。这里的登录不会同步到大肥译，适合检查官网是否能正常打开或在浏览器中直接聊天。"),11,"#AEBFD3"));
            webContent.Children.Add(new Border { BorderBrush=Ui.Brush("#344A63"),BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(0,12,0,0),Child=outside });
            webInfo=new Border { Background=Ui.Brush("#101E32"),CornerRadius=new CornerRadius(12),Padding=new Thickness(16),Child=webContent }; servicePage.Children.Add(webInfo);
            apiMode.Checked+=delegate { UpdateConnection(); }; webMode.Checked+=delegate { UpdateConnection(); }; UpdateConnection();
            form=readingPage;
            interfaceLanguage=new ComboBox { SelectedIndex=original.InterfaceLanguage=="en"?1:0 }; interfaceLanguage.Items.Add(ProductLanguage.T("简体中文")); interfaceLanguage.Items.Add(ProductLanguage.T("English"));
            targetLanguage=new ComboBox { SelectedIndex=original.TargetLanguage=="en"?1:original.TargetLanguage=="ja"?2:0 }; targetLanguage.Items.Add(ProductLanguage.T("简体中文")); targetLanguage.Items.Add(ProductLanguage.T("English")); targetLanguage.Items.Add(ProductLanguage.T("日本語"));
            var languageOptions=new StackPanel(); languageOptions.Children.Add(Ui.Labelled("界面语言",interfaceLanguage,"保存后重启应用生效；回答语言独立设置。")); languageOptions.Children.Add(Ui.Labelled("翻译与回答语言",targetLanguage,"官网和 API 都使用这个语言；术语与公式保留。"));
            var demo=Ui.Button("体验划词示例",OpenDemo,false); languageOptions.Children.Add(demo);
            form.Children.Add(new Expander { Header=ProductLanguage.T("语言与首次体验"),Content=languageOptions,IsExpanded=true,Margin=new Thickness(0,0,0,15) });
            translation=new TextBox { Text=original.TranslatePrompt,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=88,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
            explanation=new TextBox { Text=original.ExplainPrompt,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=100,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
            var promptStack=new StackPanel(); promptStack.Children.Add(Ui.Labelled("翻译提示词",translation,null)); promptStack.Children.Add(Ui.Labelled("解释提示词",explanation,null));
            var advanced=new Expander { Header=ProductLanguage.T("定制翻译与解释风格"),Content=promptStack,Margin=new Thickness(0,0,0,15) }; form.Children.Add(advanced);
            var guide=new Border { Background=Ui.Brush("#F4F6FA"),CornerRadius=new CornerRadius(10),Padding=new Thickness(12),Child=Ui.Text(ProductLanguage.T("Ctrl + Alt + D  读取选区；不支持时尝试复制\nCtrl + Alt + V  翻译你已复制的文字\nCtrl + Alt + S  屏幕识字 / 截图翻译\nEsc  收起浮窗    ·    小按钮左侧拖柄可移动"),11,"#7C879B") }; triggerPage.Children.Add(guide);
            var body=new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(144) }); body.ColumnDefinitions.Add(new ColumnDefinition());
            var nav=new StackPanel { Margin=new Thickness(0,0,16,0) }; body.Children.Add(nav); var pageFrame=new Grid { Margin=new Thickness(4,0,0,0) }; pageFrame.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); pageFrame.RowDefinitions.Add(new RowDefinition()); Grid.SetColumn(pageFrame,1); body.Children.Add(pageFrame);
            var pageHeading=new StackPanel { Margin=new Thickness(0,0,0,20) }; sectionTitle=Ui.Text(ProductLanguage.T(""),18,"#F4F1E8"); sectionTitle.FontWeight=FontWeights.SemiBold; sectionHint=Ui.Text(ProductLanguage.T(""),11,"#AEBFD3"); sectionHint.Margin=new Thickness(0,6,0,0); pageHeading.Children.Add(sectionTitle); pageHeading.Children.Add(sectionHint); Ui.Row(pageFrame,pageHeading,0);
            AddSection(pageFrame,"阅读外观",readingPage); AddSection(pageFrame,"划词与快捷键",triggerPage); AddSection(pageFrame,"桌面肥鱼",petPage); AddSection(pageFrame,"连接与模型",servicePage);
            var updates=new UpdatePanel(delegate { save(Draft()); }); AddSection(pageFrame,"更新与关于",updates);
            AddSection(pageFrame,"使用指南",new GettingStartedPanel(OpenDemo,SelectSection));
            foreach(string title in new[]{"连接与模型","划词与快捷键","阅读外观","桌面肥鱼","使用指南","更新与关于"}) { string section=title; var button=Ui.Button(title,delegate { SelectSection(section); },false); button.HorizontalContentAlignment=HorizontalAlignment.Left; button.Padding=new Thickness(12,12,12,12); button.Margin=new Thickness(0,0,0,6); navigation[title]=button; nav.Children.Add(button); }
            var navNote=Ui.Text(ProductLanguage.T("你的设置保存在本机"),10,"#9DAFC7"); navNote.Margin=new Thickness(8,18,0,0); nav.Children.Add(navNote); Ui.Row(grid,body,1);
            var footer=new StackPanel { Margin=new Thickness(0,15,0,0) };
            feedback=Ui.Text(original.LastWarning,11,"#8893A6"); feedback.Margin=new Thickness(0,0,0,10); footer.Children.Add(feedback);
            var actions=new Grid(); actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            testConnection=Ui.Button("测试 API 连接",async delegate { await TestConnection(); },false); testConnection.HorizontalAlignment=HorizontalAlignment.Left; actions.Children.Add(testConnection); UpdateConnection();
            var accept=Ui.Button("保存并开始使用",Save,true); Grid.SetColumn(accept,1); actions.Children.Add(accept); footer.Children.Add(actions); Ui.Row(grid,footer,2);
            Ui.Frame(this,grid,23);
            SelectSection("连接与模型");
            Loaded+=delegate { double available=SystemParameters.WorkArea.Height-35; if(Height>available) Height=available; };
            Closed+=delegate { updates.Cancel(); network.Cancel(); network.Dispose(); };
            KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Escape) Close(); };
        }
        void AddSection(Grid frame,string title,StackPanel content) { var scroll=new ScrollViewer { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(0,0,9,0),Visibility=Visibility.Collapsed }; sections[title]=scroll; Ui.Row(frame,scroll,1); }
        internal void ShowWebsiteState(WebsiteState value) { if(websiteStatus!=null) { websiteStatus.Text=ProductLanguage.T(value.Message); websiteStatus.Foreground=Ui.Brush(value.Ready?"#8CD3DF":"#AEBFD3"); } }
        void UpdateConnection() { bool web=webMode!=null&&webMode.IsChecked==true; if(apiPage!=null) apiPage.Visibility=web?Visibility.Collapsed:Visibility.Visible; if(webInfo!=null) webInfo.Visibility=web?Visibility.Visible:Visibility.Collapsed; if(testConnection!=null) { testConnection.Visibility=CurrentSection=="连接与模型"?Visibility.Visible:Visibility.Collapsed; testConnection.Content=ProductLanguage.T(web?"检查官网连接":"测试 API 连接"); } if(connectionSummary!=null&&ProductLanguage.Interface=="en") { connectionSummary.Text=(web?"Connection: DeepSeek website · no API key":"Connection: DeepSeek API · your key")+"\nMode: "+ProductLanguage.T(original.Mode=="companion"?"陪伴模式":original.Mode=="auto"?"自动模式":original.Mode=="clipboard"?"剪贴板模式":"点击模式")+(web?"":" · API usage is billed to your account."); return; } if(connectionSummary!=null) connectionSummary.Text=(web?"连接：DeepSeek 官网账号 · 无需 API Key":"连接：DeepSeek API · 使用自己的 Key")+"\n当前模式："+(original.Mode=="companion"?"仅陪伴":original.Mode=="auto"?"自动翻译":original.Mode=="clipboard"?"剪贴板翻译":"点击小按钮")+(web?"":" · API 费用按个人账号计费。"); }
        Settings Draft() {
            var s=original.Copy(); s.Service=webMode.IsChecked==true?"web":"api"; s.Thinking=thinking.IsChecked==true; s.ApiKey=key.Password.Trim(); s.BaseUrl=url.Text.Trim(); s.Model=model.Text.Trim(); s.Mode=companionMode.IsChecked==true?"companion":clipboardMode.IsChecked==true?"clipboard":autoMode.IsChecked==true?"auto":"button"; s.AutoDelay=(int)delay.Value;
            s.InterfaceLanguage=interfaceLanguage.SelectedIndex==1?"en":"zh-CN"; s.TargetLanguage=targetLanguage.SelectedIndex==1?"en":targetLanguage.SelectedIndex==2?"ja":"zh-CN";
            s.VisionModel=visionModel.Text.Trim(); if(s.VisionModel.Length==0) s.VisionModel="deepseek-flash";
            SetBackgroundDraft(s);
            if(s.BackgroundTheme=="custom"&&string.IsNullOrEmpty(s.BackgroundPath)) throw new ArgumentException("请先选择一张背景图片。");
            s.CopyFallbackEnabled=copyEnabled.IsChecked==true; s.CopyApplications=copyApps.Text.Trim();
            s.ResidentOrb=resident.IsChecked==true||s.Mode=="companion";
            s.PetSize=petSize.SelectedIndex==0?144:petSize.SelectedIndex==2?208:176; s.ReducedMotion=reduced.IsChecked==true; s.EdgeHide=edgeHide.IsChecked==true; s.HideIdleCaption=hideCaption.IsChecked==true;
            int originalFontIndex=Math.Max(0,Math.Min(4,(original.ReadingFontSize-12)/2)); int originalSpaceIndex=original.ReadingLineSpacing<1.55?0:original.ReadingLineSpacing>1.8?2:1;
            s.ReadingFontSize=readingFont.SelectedIndex==originalFontIndex?original.ReadingFontSize:12+readingFont.SelectedIndex*2; s.ReadingLineSpacing=readingSpacing.SelectedIndex==originalSpaceIndex?original.ReadingLineSpacing:readingSpacing.SelectedIndex==0?1.45:readingSpacing.SelectedIndex==2?1.9:23.0/14.0; s.UseTermPreferences=termPreferences.IsChecked==true;
            s.TranslatePrompt=translation.Text.Trim(); s.ExplainPrompt=explanation.Text.Trim(); ApiClient.Endpoint(s.BaseUrl);
            if(s.Model.Length==0) throw new ArgumentException("请填写模型名称。");
            if(s.TranslatePrompt.Length==0||s.ExplainPrompt.Length==0) throw new ArgumentException("翻译和解释提示词不能为空。");
            return s;
        }
        void SetBackgroundDraft(Settings s) { s.BackgroundTheme=backgroundChoice.SelectedIndex==1?"angry":backgroundChoice.SelectedIndex==2?"custom":backgroundChoice.SelectedIndex==3?"none":"thinking"; s.BackgroundPath=customBackground; s.BackgroundStrength=(int)backgroundStrength.Value; }
        void UpdateBackgroundPreview() { var s=original.Copy(); SetBackgroundDraft(s); backgroundPreview.Apply(s); }
        void ChooseBackground() {
            var dialog=new Microsoft.Win32.OpenFileDialog { Title=ProductLanguage.T("选择阅读背景"),Filter="图片|*.png;*.jpg;*.jpeg;*.webp;*.bmp",CheckFileExists=true };
            if(dialog.ShowDialog(this)!=true) return;
            try { customBackground=ReadingBackdrop.Import(dialog.FileName); backgroundChoice.SelectedIndex=2; UpdateBackgroundPreview(); }
            catch(Exception e) { feedback.Text="图片未能导入："+e.Message; }
        }
        void Save() { try { var s=Draft(); save(s); Close(); } catch(Exception e) { feedback.Text=e.Message; feedback.Foreground=Ui.Brush("#C16262"); } }
        async Task LoadModels() {
            if(connectionBusy) return; connectionBusy=true; loadModels.IsEnabled=false; testConnection.IsEnabled=false;
            try {
                var s=Draft(); if(s.ApiKey.Length==0) throw new ArgumentException("先填写 API Key，再读取模型。"); feedback.Text="正在读取可用模型…";
                var list=await new ApiClient().ModelsAsync(s,network.Token); string selected=model.Text; model.Items.Clear(); foreach(var item in list) model.Items.Add(item); model.Text=selected;
                feedback.Text="读取到 "+list.Length+" 个模型；选择后保存。";
            } catch(OperationCanceledException) { } catch(Exception e) { feedback.Text="读取失败："+e.Message; } finally { connectionBusy=false; loadModels.IsEnabled=true; testConnection.IsEnabled=true; }
        }
        async Task TestConnection() {
            if(connectionBusy) return; connectionBusy=true; loadModels.IsEnabled=false; testConnection.IsEnabled=false;
            try {
                if(webMode.IsChecked==true) { feedback.Text="正在检查官网页面，不发送问题…"; var state=WebChecker==null?new WebsiteState("unknown","请打开官网登录，然后检查连接。"):await WebChecker(); ShowWebsiteState(state); feedback.Text=state.Message; return; }
                var s=Draft(); if(s.ApiKey.Length==0) throw new ArgumentException("先填写 API Key。"); feedback.Text="正在检查 API 认证和模型列表…";
                var list=await new ApiClient().ModelsAsync(s,network.Token);
                feedback.Text=Array.IndexOf(list,s.Model)>=0?"连接成功，所选模型可用。保存后即可翻译。":"连接成功，但模型列表未包含当前名称，请核对后保存。";
            } catch(OperationCanceledException) { } catch(Exception e) { feedback.Text="连接失败："+e.Message; } finally { connectionBusy=false; loadModels.IsEnabled=true; testConnection.IsEnabled=true; }
        }
    }
}
