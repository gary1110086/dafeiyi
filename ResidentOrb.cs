using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Diagnostics;
using Forms=System.Windows.Forms;

namespace LightTranslate {
    public class ResidentOrb:Window {
        public event Action ScreenRequested,ClipboardRequested,ResultRequested,SettingsRequested,DisableRequested,PositionChanged,InteractionStarted,PreferencesChanged;
        public event Action CompanionRequested,HistoryRequested,TermsRequested;
        readonly MenuItem companion=new MenuItem { Header=ProductLanguage.T("仅陪伴 · 关闭划词"),IsCheckable=true };
        readonly ContextMenu menu=new ContextMenu();
        readonly Border ball;
        readonly Image portrait=new Image { Stretch=Stretch.Uniform,IsHitTestVisible=false };
        readonly TextBlock fallback=Ui.Text(ProductLanguage.T("译"),32,"#5864D8");
        readonly TextBlock caption=Ui.Text(ProductLanguage.T("阅读就绪"),10,"#737F95");
        readonly Border pill;
        readonly Canvas edgeCanvas=new Canvas { IsHitTestVisible=false,ClipToBounds=true,HorizontalAlignment=HorizontalAlignment.Left };
        readonly Image edgeImage=new Image { IsHitTestVisible=false,Stretch=Stretch.Fill,RenderTransformOrigin=new Point(0.5,0.5) };
        readonly Image edgeIdleImage=new Image { IsHitTestVisible=false,Stretch=Stretch.Uniform };
        readonly EdgeReveal edge=new EdgeReveal();
        string dockSide=""; System.Drawing.Rectangle dockWork; double dockY,lastEdge=-1;
        public bool EdgeHide { get; private set; }
        public bool HideIdleCaption { get; private set; }
        internal string DockSide { get { return dockSide; } }
        internal double EdgeProgress { get { return edge.Progress; } }
        public Point SavedPosition { get { var bounds=Native.Bounds(this); return dockSide.Length==0?new Point(bounds.X,bounds.Y):new Point(dockSide=="left"?dockWork.Left:dockWork.Right-PetSize*VisualTreeHelper.GetDpi(this).DpiScaleX,dockY); } }
        readonly PetAnimation animation=new PetAnimation();
        readonly DispatcherTimer animationTimer=new DispatcherTimer(DispatcherPriority.Background);
        readonly Stopwatch clock=Stopwatch.StartNew();
        long lastTick,statusDeadline,captionDeadline,nextMicro=30000,chainDeadline;
        string statusText="阅读就绪"; int chain=-1; bool previewing;
        int shownIndex=-1; string shownClip="";
        public string CurrentAnimation { get { return animation.ActiveClip; } }
        public string CurrentState { get { return animation.BaseClip; } }
        public int PetSize { get; private set; }
        public bool ReducedMotion { get { return animation.ReducedMotion; } }
        internal Point DragPoint { get { return ball.PointToScreen(new Point(ball.ActualWidth*0.53,ball.ActualHeight*0.67)); } }
        internal string Caption { get { return caption.Text; } }
        internal Point TouchPoint(string part) { return ball.PointToScreen(new Point(ball.ActualWidth*(part=="tail"?0.78:0.53),ball.ActualHeight*(part=="head"?0.38:0.74))); }
        internal bool AnimationRunning { get { return animationTimer.IsEnabled; } }
        internal bool VisibleFallback { get { return ball.Child==fallback; } }
        Point origin; Rect initialBounds; bool pressed,dragged;
        public bool MenuOpen { get { return menu.IsOpen; } }
        internal bool Interacting { get { return pressed||menu.IsOpen; } }
        internal ContextMenu Menu { get { return menu; } }
        public ResidentOrb() {
            Title=ProductLanguage.T("大肥译 · 大肥鱼娘"); Width=176; Height=177; Topmost=true; ShowInTaskbar=false; ShowActivated=false;
            Focusable=false;
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; AllowsTransparency=true; Background=Brushes.Transparent;
            ball=new Border { Background=Brushes.Transparent,Cursor=Cursors.Hand,Child=portrait };
            fallback.HorizontalAlignment=HorizontalAlignment.Center; fallback.VerticalAlignment=VerticalAlignment.Center;
            fallback.ToolTip=ProductLanguage.T("角色素材暂不可用，右键仍可使用翻译功能");
            var root=new StackPanel(); root.Children.Add(ball);
            pill=new Border { Background=Ui.Brush("#F4F6FA"),BorderBrush=Ui.Brush("#E5E9F2"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(9,4,9,4),HorizontalAlignment=HorizontalAlignment.Center,MaxWidth=170,Child=caption };
            root.ClipToBounds=true; edgeCanvas.Children.Add(edgeImage); edgeCanvas.Children.Add(edgeIdleImage);
            caption.TextWrapping=TextWrapping.NoWrap; caption.TextTrimming=TextTrimming.CharacterEllipsis; root.Children.Add(pill);
            Content=root; ball.ToolTip=ProductLanguage.T("大肥译 · 头部摸摸，身体打开菜单，尾巴互动\n右键展开功能 · 拖动放置");
            pill.MouseLeftButtonUp+=delegate { OpenMenu(); }; pill.MouseRightButtonUp+=delegate { OpenMenu(); };
            pill.MouseLeftButtonDown+=delegate { if(InteractionStarted!=null) InteractionStarted(); }; pill.MouseRightButtonDown+=delegate { if(InteractionStarted!=null) InteractionStarted(); };
            SourceInitialized+=delegate { Native.MakeNonActivating(this); };
            Add("屏幕识字",delegate { if(ScreenRequested!=null) ScreenRequested(); },"Ctrl+Alt+S","screen");
            Add("翻译剪贴板",delegate { if(ClipboardRequested!=null) ClipboardRequested(); },"Ctrl+Alt+V","clipboard");
            Add("打开结果窗口",delegate { if(ResultRequested!=null) ResultRequested(); },null,"result");
            Add("历史记录 · 最近 30 条",delegate { if(HistoryRequested!=null) HistoryRequested(); },null,"history");
            Add("术语收藏",delegate { if(TermsRequested!=null) TermsRequested(); },null,"bookmark");
            menu.Items.Add(new Separator());
            Add("设置",delegate { if(SettingsRequested!=null) SettingsRequested(); },null,"settings");
            companion.Icon=MenuIcons.Create("companion"); companion.Click+=delegate { if(CompanionRequested!=null) CompanionRequested(); }; menu.Items.Add(companion);
            menu.Items.Add(new Separator());
            var play=new MenuItem { Header=ProductLanguage.T("和肥鱼玩"),Icon=MenuIcons.Create("heart") }; var pat=new MenuItem { Header=ProductLanguage.T("摸摸头"),Icon=MenuIcons.Create("heart") }; pat.Click+=delegate { PlayInteraction("head_pat"); }; play.Items.Add(pat); var feed=new MenuItem { Header=ProductLanguage.T("喂一口 token · 本地互动"),Icon=MenuIcons.Create("food") }; feed.Click+=delegate { PlayInteraction("eat_token"); }; play.Items.Add(feed); menu.Items.Add(play);
            var previews=new MenuItem { Header=ProductLanguage.T("动作预览"),Icon=MenuIcons.Create("actions") };
            string[] names={"idle","waiting","thinking","working","working_search","working_command","success","error","dragging","dragging_release","dragging_dizzy","dragging_protest","head_pat","poke","tail","eat_token"};
            string[] labels={"待机","等待","思考","书写","查找","执行","完成","遇到问题","抱起","落地","晕乎乎","抗议","摸摸头","戳戳","摆尾","吃 token"};
            for(int i=0;i<names.Length;i++) { string action=names[i],label=labels[i]; var item=new MenuItem { Header=ProductLanguage.T(label) }; item.Click+=delegate { Preview(action,label); }; previews.Items.Add(item); } menu.Items.Add(previews);
            menu.Items.Add(new Separator());
            var display=new MenuItem { Header=ProductLanguage.T("显示与收起"),Icon=MenuIcons.Create("edge") };
            var sizes=new MenuItem { Header=ProductLanguage.T("大小"),Icon=MenuIcons.Create("size") }; foreach(int size in new[]{144,176,208}) { int selected=size; var item=new MenuItem { Header=size==144?"小巧":size==176?"标准":"大一点",IsCheckable=true }; menu.Opened+=delegate { item.IsChecked=PetSize==selected; }; item.Click+=delegate { Undock(); ApplySize(selected); Native.Clamp(this); TryDock(); if(PositionChanged!=null) PositionChanged(); if(PreferencesChanged!=null) PreferencesChanged(); }; sizes.Items.Add(item); } display.Items.Add(sizes);
            var reduce=new MenuItem { Header=ProductLanguage.T("减少动态"),IsCheckable=true,Icon=MenuIcons.Create("motion") }; reduce.Click+=delegate { ApplyMotion(reduce.IsChecked); if(PreferencesChanged!=null) PreferencesChanged(); }; menu.Opened+=delegate { reduce.IsChecked=ReducedMotion; }; display.Items.Add(reduce);
            var edgeOption=new MenuItem { Header=ProductLanguage.T("贴边探头"),IsCheckable=true,Icon=MenuIcons.Create("edge") }; edgeOption.Click+=delegate { EdgeHide=edgeOption.IsChecked; if(!EdgeHide) Undock(); else TryDock(); if(PreferencesChanged!=null) PreferencesChanged(); }; display.Items.Add(edgeOption);
            var captionOption=new MenuItem { Header=ProductLanguage.T("空闲时隐藏状态文字"),IsCheckable=true,Icon=MenuIcons.Create("label") }; captionOption.Click+=delegate { HideIdleCaption=captionOption.IsChecked; if(PreferencesChanged!=null) PreferencesChanged(); }; display.Items.Add(captionOption);
            menu.Opened+=delegate { edgeOption.IsChecked=EdgeHide; captionOption.IsChecked=HideIdleCaption; }; menu.Items.Add(display);
            Add("收起桌面鱼娘",delegate { if(DisableRequested!=null) DisableRequested(); },null,"hide");
            menu.PlacementTarget=ball; menu.Placement=System.Windows.Controls.Primitives.PlacementMode.Left;
            ball.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) {
                if(InteractionStarted!=null) InteractionStarted();
                if(menu.IsOpen) menu.IsOpen=false;
                Undock();
                origin=Native.Cursor(); initialBounds=Native.Bounds(this); pressed=true; dragged=false; ball.CaptureMouse(); e.Handled=true;
            };
            ball.MouseMove+=delegate(object sender,MouseEventArgs e) {
                if(!pressed) return; Point delta=Native.Cursor()-new Vector(origin.X,origin.Y);
                if(!dragged && (Native.Cursor()-origin).Length<4) return;
                if(!dragged) { chain=-1; previewing=false; animation.Play("dragging",int.MaxValue); RenderFrame(); }
                dragged=true; Native.MoveWithoutActivation(this,initialBounds.X+delta.X,initialBounds.Y+delta.Y); e.Handled=true;
            };
            ball.MouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e) {
                if(!pressed) return; pressed=false; ball.ReleaseMouseCapture();
                if(dragged) { Native.Clamp(this); TryDock(); if(PositionChanged!=null) PositionChanged(); StartRelease(); }
                else { var position=e.GetPosition(ball); if(position.Y<ball.ActualHeight*0.48) PlayInteraction("head_pat"); else if(position.X>ball.ActualWidth*0.72) PlayInteraction("tail"); else { PlayInteraction("poke"); OpenMenu(); } TryDock(); }
                e.Handled=true;
            };
            ball.LostMouseCapture+=delegate { if(pressed&&dragged) { Native.Clamp(this); TryDock(); if(PositionChanged!=null) PositionChanged(); StartRelease(); } pressed=false; };
            ball.MouseRightButtonDown+=delegate { if(InteractionStarted!=null) InteractionStarted(); };
            ball.MouseRightButtonUp+=delegate(object sender,MouseButtonEventArgs e) { OpenMenu(); e.Handled=true; };
            KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Escape) { menu.IsOpen=false; e.Handled=true; } };
            animationTimer.Interval=TimeSpan.FromMilliseconds(42); animationTimer.Tick+=delegate { Animate(); };
            IsVisibleChanged+=delegate { if(IsVisible) { lastTick=clock.ElapsedMilliseconds; Animate(); animationTimer.Start(); } else animationTimer.Stop(); };
            Closed+=delegate { animationTimer.Stop(); menu.IsOpen=false; };
            ApplySize(176); RenderFrame();
        }
        public void SetStatus(string state,string text,int durationMs) {
            animation.SetBase(state); statusText=ProductLanguage.T(text); statusDeadline=durationMs>0?clock.ElapsedMilliseconds+durationMs:0;
            if(!previewing&&captionDeadline==0) caption.Text=text; caption.ToolTip=text; RenderFrame();
        }
        public void PlayInteraction(string action) {
            chain=-1; previewing=false; animation.Play(action,0);
            caption.Text=action=="head_pat"?"摸摸头，继续陪你读":action=="tail"?"尾巴也想伸个懒腰":action=="eat_token"?"补充一点阅读能量":"需要读哪一段？";
            captionDeadline=clock.ElapsedMilliseconds+2200; RenderFrame();
        }
        internal void Preview(string action,string label) { PetClip clip; if(!PetAssets.Shared.Clips.TryGetValue(action,out clip)) { caption.Text="角色素材缺失，仍可划词"; return; } chain=-1; previewing=true; animation.Play(action,clip.loop?3500:0); caption.Text="动作预览 · "+label; captionDeadline=0; RenderFrame(); }
        internal void ApplyPreferences(Settings settings) { companion.IsChecked=settings.Mode=="companion"; EdgeHide=settings.EdgeHide; HideIdleCaption=settings.HideIdleCaption; if(!EdgeHide) Undock(); if(PetSize!=settings.PetSize) { Undock(); ApplySize(settings.PetSize); } if(ReducedMotion!=settings.ReducedMotion) ApplyMotion(settings.ReducedMotion); }
        internal void SetReducedMotionForTest(bool reduced) { ApplyMotion(reduced); }
        void ApplySize(int size) { PetSize=Math.Max(144,Math.Min(208,size)); Width=PetSize; ball.Width=PetSize; ball.Height=PetSize*344.0/412.0; Height=ball.Height+30; }
        void ApplyMotion(bool reduced) { animation.ReducedMotion=reduced; animationTimer.Interval=TimeSpan.FromMilliseconds(reduced?250:42); if(reduced) { chain=-1; animation.ClearOverlay(); previewing=false; } shownIndex=-1; RenderFrame(); }
        void StartRelease() { if(ReducedMotion) { animation.ClearOverlay(); RenderFrame(); return; } chain=0; animation.Play("dragging_release",0); chainDeadline=clock.ElapsedMilliseconds+2058; RenderFrame(); }
        void Animate() {
            long now=clock.ElapsedMilliseconds; int elapsed=(int)Math.Min(1000,Math.Max(0,now-lastTick)); lastTick=now;
            animation.Tick(elapsed);
            if(chain>=0&&now>=chainDeadline) {
                if(chain==0) { chain=1; animation.Play("dragging_dizzy",840); chainDeadline=now+840; }
                else if(chain==1) { chain=2; animation.Play("dragging_protest",0); chainDeadline=now+4032; }
                else { chain=-1; animation.ClearOverlay(); }
            }
            if(statusDeadline>0&&now>=statusDeadline) { statusDeadline=0; animation.SetBase("idle"); statusText=companion.IsChecked?"陪伴模式 · 划词已关闭":"阅读就绪"; }
            if(previewing && !animation.HasOverlay) { previewing=false; caption.Text=statusText; }
            if(captionDeadline>0&&now>=captionDeadline) { captionDeadline=0; if(!previewing) caption.Text=statusText; }
            if(captionDeadline==0&&!previewing) caption.Text=statusText;
            if(!ReducedMotion && animation.BaseClip=="idle" && animation.ActiveClip=="idle" && now>=nextMicro && !Interacting && (dockSide.Length==0||edge.Progress==1)) { animation.Play("eat_token",0); nextMicro=now+45000; }
            RenderFrame();
            AnimateEdge(elapsed);
            pill.Visibility=dockSide.Length>0&&edge.Progress<1?Visibility.Collapsed:HideIdleCaption&&animation.BaseClip=="idle"&&!IsMouseOver&&!Interacting&&captionDeadline==0&&!previewing?Visibility.Hidden:Visibility.Visible;
        }
        void RenderFrame() {
            if(shownIndex==animation.Index&&shownClip==animation.ActiveClip) return;
            var frame=PetAssets.Shared.Image(animation.ActiveClip,animation.Index);
            if(frame!=null) { portrait.Source=frame; if(dockSide.Length==0||edge.Progress==1) ball.Child=portrait; } else if(dockSide.Length==0||edge.Progress==1) ball.Child=fallback;
            shownIndex=animation.Index; shownClip=animation.ActiveClip;
        }
        void Add(string text,Action action,string shortcut,string icon) {
            var item=new MenuItem { Header=ProductLanguage.T(text),Icon=MenuIcons.Create(icon),InputGestureText=shortcut??"",Padding=new Thickness(10,7,10,7) }; item.Click+=delegate { action(); }; menu.Items.Add(item);
        }
        internal void OpenMenu() { if(InteractionStarted!=null) InteractionStarted(); menu.IsOpen=true; }
        internal void Suspend() { menu.IsOpen=false; Hide(); }
        internal void RestorePosition(Settings settings) {
            ApplyPreferences(settings);
            Show(); var bounds=Native.Bounds(this);
            if(settings.OrbX==int.MinValue || settings.OrbY==int.MinValue) {
                var work=Forms.Screen.FromPoint(new System.Drawing.Point((int)Native.Cursor().X,(int)Native.Cursor().Y)).WorkingArea;
                Native.MoveWithoutActivation(this,work.Right-bounds.Width-24,work.Top+work.Height*0.45);
            } else Native.MoveWithoutActivation(this,settings.OrbX,settings.OrbY);
            Native.Clamp(this);
            UpdateLayout();
            TryDock();
        }
        internal void TryDock() {
            if(!EdgeHide||!IsVisible) return;
            var b=Native.Bounds(this); var work=Forms.Screen.FromPoint(new System.Drawing.Point((int)(b.X+b.Width/2),(int)(b.Y+b.Height/2))).WorkingArea;
            if(b.X-work.Left<=20) dockSide="left"; else if(work.Right-b.Right<=20) dockSide="right"; else { dockSide=""; return; }
            dockWork=work; dockY=b.Y; edge.Open(); lastEdge=-1;
        }
        void Undock() {
            if(dockSide.Length==0) return; var pos=SavedPosition; dockSide=""; Width=PetSize; UpdateLayout(); Native.MoveWithoutActivation(this,pos.X,pos.Y); ball.Child=portrait; shownIndex=-1; lastEdge=-1; RenderFrame();
        }
        void AnimateEdge(int elapsed) {
            if(dockSide.Length==0) return;
            var screen=Forms.Screen.FromPoint(new System.Drawing.Point((int)SavedPosition.X,(int)dockY)).WorkingArea;
            if(screen!=dockWork) { Undock(); Native.Clamp(this); TryDock(); if(dockSide.Length==0) return; }
            var bounds=Native.Bounds(this); var point=Native.Cursor(); var hit=bounds; hit.Inflate(12,12);
            bool busy=animation.BaseClip!="idle"||animation.HasOverlay||chain>=0||previewing;
            edge.Tick(elapsed,hit.Contains(point),Interacting||busy,ReducedMotion);
            double progress=edge.Progress;
            if(progress==lastEdge) { if(progress==1&&ball.Child!=portrait) { ball.Child=portrait; shownIndex=-1; RenderFrame(); } return; }
            lastEdge=progress;
            double eased=progress*progress*(3-2*progress); Width=PetSize*(0.33+0.67*eased); UpdateLayout();
            bounds=Native.Bounds(this); Native.MoveWithoutActivation(this,dockSide=="left"?dockWork.Left:dockWork.Right-bounds.Width,dockY);
            if(progress<1) {
                var frame=EdgeArt.Frame((int)Math.Round(progress*7));
                if(frame==null) { ball.Child=portrait; return; }
                edgeImage.Source=frame; double size=PetSize*0.68; edgeImage.Width=edgeImage.Height=size;
                double left=-size*0.25+(PetSize*0.25+size*0.25)*eased;
                Canvas.SetLeft(edgeImage,dockSide=="left"?left:Width-left-size); Canvas.SetTop(edgeImage,PetSize*0.08);
                edgeImage.RenderTransform=new ScaleTransform(dockSide=="left"?1:-1,1); edgeCanvas.Width=Width; edgeCanvas.Height=ball.Height; ball.Child=edgeCanvas;
                double fade=Math.Max(0,Math.Min(1,(progress-0.8)/0.2)); edgeImage.Opacity=1-fade; edgeIdleImage.Opacity=fade; edgeIdleImage.Source=portrait.Source; edgeIdleImage.Width=PetSize; edgeIdleImage.Height=ball.Height; Canvas.SetLeft(edgeIdleImage,dockSide=="left"?0:Width-PetSize); Canvas.SetTop(edgeIdleImage,0);
            } else { ball.Child=portrait; shownIndex=-1; RenderFrame(); }
        }
    }
}
