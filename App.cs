using System;
using System.Windows;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Drawing;
using Forms=System.Windows.Forms;
namespace LightTranslate {
    public static class Program {
        [STAThread] public static int Main(string[] args) {
            if(args.Length>0 && args[0]=="--self-test") return Tests.Run(args.Length>1?args[1]:"self-test.txt");
            var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
            if(args.Length==0) ProductLanguage.Interface=Settings.Load(Settings.DefaultPath).InterfaceLanguage;
            AppController.RequireIsolatedProfile=args.Length>0;
            Ui.Initialize(app);
            if(args.Length>0 && args[0]=="--fixture") { UiTests.Fixture(app,args[1]); return 0; }
            if(args.Length>0 && args[0]=="--ui-test") return UiTests.Run(args[1],app);
            if(args.Length>0 && args[0]=="--runtime-test") return RuntimeTests.Run(args[1],app);
            if(args.Length>0 && args[0]=="--pet-test") return PetUiTests.Run(args[1],app);
            if(args.Length>0 && args[0]=="--render-test") return RenderTests.Run(args[1],app);
            if(args.Length>0 && args[0]=="--companion-test") return CompanionUiTests.Run(args[1],app);
            if(args.Length>0 && args[0]=="--reading-test") return ReadingUiTests.Run(args[1],app);
            if(args.Length>0 && args[0]=="--window-test") return WindowUiTests.Run(args[1],app);
            if(args.Length>0 && args[0]=="--product-test") return ProductUiTests.Run(args[1],app);
            if(args.Length>0 && args[0]=="--web-smoke-test") return ProductUiTests.WebSmoke(args[1],app);
            if(args.Length>0 && args[0]=="--web-connect-test") return ProductUiTests.WebSmoke(args[1],app,true);
            if(args.Length>0 && args[0]=="--web-bridge-test") return WebBridgeTests.Run(args[1],app);
            if(args.Length>0 && args[0]=="--iteration-test") return IterationUiTests.Run(args[1],app);
            if(args.Length>0 && args[0]=="--website-ui-test") return WebsiteUiTests.Run(args[1],app);
            if(args.Length>0 && args[0]=="--vision-connect-test") return VisionLive.Run(args[1],args.Length>2?args[2]:"web",app);
            bool created;
            using(var singleton=new Mutex(true,"Local\\LightTranslate.Desktop.v1",out created)) {
                if(!created) { MessageBox.Show("大肥译已经在运行。请在任务栏右下角托盘中右键肥鱼图标打开设置。","大肥译"); return 0; }
                AppController controller=null;
                app.DispatcherUnhandledException+=delegate(object sender,DispatcherUnhandledExceptionEventArgs e) {
                    e.Handled=true;
                    MessageBox.Show("操作未完成，请重试。\n"+e.Exception.Message,"大肥译",MessageBoxButton.OK,MessageBoxImage.Information);
                };
                try {
                    controller=new AppController(Settings.Load(Settings.DefaultPath),false,SessionCache.DefaultPath);
                    app.Exit+=delegate { if(controller!=null) controller.Dispose(); };
                    app.Run(); return 0;
                } catch(Exception e) { MessageBox.Show("大肥译启动失败："+e.Message,"大肥译"); if(controller!=null) controller.Dispose(); return 1; }
            }
        }
    }
    public class AppController: IDisposable {
        public ChipView Chip=new ChipView(); public PopupView Popup=new PopupView();
        public ResidentOrb Orb=new ResidentOrb();
        public bool ShortcutsReady;
        internal static bool RequireIsolatedProfile;
        internal string SettingsSavePath;
        internal Func<IntPtr> Foreground=Native.GetForegroundWindow;
        internal Func<IntPtr,System.Windows.Point,Task<SelectionResult>> Reader=SelectionService.ReadAsync;
        internal Func<IntPtr,Task<string>> Copier=Native.TryCopySelection;
        internal Func<IntPtr,string> WindowProcessName=Native.ProcessName;
        internal Func<uint> InputActivity=Native.InputActivityStamp;
        internal Func<ScreenFrame> CaptureScreen=ScreenFrame.Capture;
        internal Func<byte[],CancellationToken,Task<OcrDocument>> Recognizer=OcrService.RecognizeAsync;
        internal OcrOverlay OcrWindow;
        CancellationTokenSource ocrRequest; int ocrStartupVersion;
        internal Task ProbeForTest(IntPtr hwnd,System.Windows.Point point) { return Probe(hwnd,point,++selectionVersion); }
        Settings settings; bool testing,disposed,ocrStarting; SettingsView settingsWindow;
        DeepSeekWebView webWindow; internal Action<string> WebPresenter;
        string retryMode="translate",retryQuestion=null,retrySource="";
        internal Func<Settings,string,string,List<Dictionary<string,string>>,Action<string>,CancellationToken,Task> WebStreamer;
        SelectionPolicy policy=new SelectionPolicy(); RequestGeneration generation=new RequestGeneration();
        GestureOrigins gestureOrigins=new GestureOrigins();
        SessionCache cache=new SessionCache();
        TermStore terms; TermBook termBook; TermEditor termEditor;
        TermPeek termPeek; InputActionObserver inputActions;
        readonly ChipLifetime chipLifetime=new ChipLifetime(); IntPtr chipOwner; long chipActionAt;
        readonly PopupFocusLifetime popupLifetime=new PopupFocusLifetime(); long popupActionAt;
        internal IList<CachedResult> RecentResults { get { return cache.Recent; } }
        CancellationTokenSource request; List<Dictionary<string,string>> history=new List<Dictionary<string,string>>();
        ImageRequest selectedImage; string selectionText="";
        string selected { get { return selectionText; } set { selectionText=value; selectedImage=null; } }
        System.Windows.Point anchor=new System.Windows.Point(400,250);
        Forms.NotifyIcon tray; Forms.ToolStripMenuItem buttonItem,autoItem,clipboardItem,companionItem,pauseItem,residentItem;
        Forms.ToolStripMenuItem trayMode,trayService;
        string IdleCaption { get { return settings.Mode=="companion"?"陪伴模式 · 划词已关闭":settings.Enabled?"阅读就绪":"自动识别已暂停"; } }
        ClipboardWatchPolicy clipboardWatch=new ClipboardWatchPolicy();
        DispatcherTimer poll; Stopwatch clock=Stopwatch.StartNew();
        bool mouseDown,shiftDown; System.Windows.Point downPoint,lastUpPoint; long lastUpTime=-1000;
        long pendingAt,chipAt; IntPtr pendingWindow,hotkeyHandle; System.Windows.Point pendingPoint; int selectionVersion,pendingVersion;
        HwndSource hotkeySource; Icon trayIcon;
        public AppController(Settings s,bool isTesting,string historyPath=null,string termsPath=null,string settingsPath=null) {
            if(!isTesting&&RequireIsolatedProfile&&settingsPath==null) throw new InvalidOperationException("Verification requires an isolated settings path before startup.");
            SettingsSavePath=settingsPath??(isTesting?Path.Combine(Path.GetTempPath(),"DaFeiYi-verification",Guid.NewGuid().ToString("N"),"settings.json"):Settings.DefaultPath);
            settings=s; testing=isTesting; clipboardWatch.Reset(Native.GetClipboardSequenceNumber());
            WebPresenter=OpenWeb;
            WebStreamer=StreamWeb;
            cache=new SessionCache(historyPath);
            terms=new TermStore(termsPath??(isTesting||historyPath==null?null:TermStore.DefaultPath));
            Popup.SetAppearance(settings);
            Popup.SetService(settings); Popup.ServiceRequested+=ChangeService;
            RefreshTermLinks(); terms.Changed+=RefreshTermLinks; Popup.TermClicked+=ShowTerm;
            Popup.ReadingChanged+=delegate(int font,double spacing) { settings.ReadingFontSize=font; settings.ReadingLineSpacing=spacing; if(termBook!=null) termBook.SetReadingStyle(settings); SaveQuietly(); };
            Popup.IsVisibleChanged+=delegate { if(Popup.IsVisible) { popupLifetime.Start(Foreground()); popupActionAt=inputActions==null?0:inputActions.Sequence; } };
            Popup.Activated+=delegate { UpdatePopupVisibility(); };
            Popup.Deactivated+=delegate { if(!disposed&&!Application.Current.Dispatcher.HasShutdownStarted) Application.Current.Dispatcher.BeginInvoke(new Action(UpdatePopupVisibility)); };
            Chip.Requested+=delegate(string mode) { Ignore(RequestAsync(mode,null)); };
            Popup.ModeRequested+=delegate(string mode) { Ignore(RequestAsync(mode,null)); };
            Popup.FollowRequested+=delegate(string text) { Ignore(RequestAsync("followup",text)); };
            Popup.DismissRequested+=Dismiss; Popup.SettingsRequested+=OpenSettings;
            Popup.ScreenRequested+=delegate { Ignore(StartOcr()); };
            Popup.RetryRequested+=delegate { Ignore(RequestAsync(retrySource==selected?retryMode:Popup.CurrentMode,retrySource==selected?retryQuestion:null,true)); };
            Popup.WebsiteRecoveryRequested+=delegate { WebPresenter(""); };
            Popup.TriggerModeRequested+=delegate {
                ChangeMode(settings.Mode=="button"?"auto":settings.Mode=="auto"?"clipboard":settings.Mode=="clipboard"?"companion":"button",false);
            };
            Popup.RecentRequested+=RestoreRecent; Popup.SetRecent(cache.Recent); Popup.SetTriggerMode(settings.Mode);
            Popup.ClearHistoryRequested+=delegate { cache.Clear(); Popup.SetRecent(cache.Recent); Popup.SetBusy(request!=null,cache.LastError.Length>0?cache.LastError:"历史记录已清空"); };
            Popup.FavoriteRequested+=CollectTerm; Popup.TermsRequested+=OpenTerms;
            Orb.ScreenRequested+=delegate { Ignore(StartOcr()); };
            Orb.ClipboardRequested+=delegate { Ignore(ClipboardRequest()); };
            Orb.ResultRequested+=OpenResult;
            Orb.HistoryRequested+=delegate { OpenResult(); Popup.ShowRecent(); }; Orb.TermsRequested+=OpenTerms;
            Orb.SettingsRequested+=OpenSettings;
            Orb.DisableRequested+=delegate { SetResident(false); };
            Orb.CompanionRequested+=delegate { ChangeMode(settings.Mode=="companion"?"button":"companion"); };
            Orb.PositionChanged+=RememberOrbPosition;
            Orb.PreferencesChanged+=delegate { settings.PetSize=Orb.PetSize; settings.ReducedMotion=Orb.ReducedMotion; settings.EdgeHide=Orb.EdgeHide; settings.HideIdleCaption=Orb.HideIdleCaption; SaveQuietly(); };
            Orb.InteractionStarted+=delegate { selectionVersion++; pendingAt=0; policy.Clear(); Chip.Hide(); if(request==null) Orb.SetStatus("idle",IdleCaption,0); };
            if(testing) return;
            inputActions=new InputActionObserver(Application.Current.Dispatcher); inputActions.Acted+=delegate(long sequence,int kind,System.Windows.Point point) { if(sequence>chipActionAt) HandleChipAction(kind,point); if(sequence>popupActionAt) HandlePopupAction(kind,point); };
            InstallTray();
            UpdateResident();
            hotkeySource=new HwndSource(new HwndSourceParameters("LightTranslate.Shortcuts") { Width=0,Height=0,WindowStyle=0 }); hotkeyHandle=hotkeySource.Handle; hotkeySource.AddHook(HotkeyHook);
            bool d=Native.RegisterHotKey(hotkeyHandle,1,0x4000|0x0002|0x0001,0x44);
            bool v=Native.RegisterHotKey(hotkeyHandle,2,0x4000|0x0002|0x0001,0x56);
            bool o=Native.RegisterHotKey(hotkeyHandle,3,0x4000|0x0002|0x0001,0x53);
            ShortcutsReady=d&&v&&o;
            poll=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(45) }; poll.Tick+=Poll; poll.Start();
            if(!settings.OnboardingSeen) { settings.OnboardingSeen=true; SaveQuietly(); var welcome=new WelcomeView(OpenSettings,delegate { ChangeMode("companion"); }); welcome.Show(); }
            else if(settings.Mode!="companion"&&((settings.Service=="api"&&settings.ApiKey.Length==0)||settings.LastWarning.Length>0)) { OpenSettings(); Orb.SetStatus("waiting","先在设置中选择连接方式",0); }
            if(!d||!v||!o) tray.ShowBalloonTip(6000,"大肥译快捷键冲突","Ctrl+Alt+D、V 或 S 被其他程序占用；也可从托盘或浮窗使用对应功能。",Forms.ToolTipIcon.Info);
        }
        static void Ignore(Task task) { /* Event tasks catch and surface errors inside their implementation. */ }
        void InstallTray() {
            trayIcon=Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            tray=new Forms.NotifyIcon { Icon=trayIcon,Text="大肥译 · 点击划词按钮",Visible=true };
            var menu=new Forms.ContextMenuStrip();
            menu.Items.Add("打开结果窗口",null,delegate { OpenResult(); });
            var translateClipboard=new Forms.ToolStripMenuItem("翻译剪贴板",null,delegate { Ignore(ClipboardRequest()); }); translateClipboard.ShortcutKeyDisplayString="Ctrl+Alt+V"; menu.Items.Add(translateClipboard);
            var screenshot=new Forms.ToolStripMenuItem("屏幕识字",null,delegate { Ignore(StartOcr()); }); screenshot.ShortcutKeyDisplayString="Ctrl+Alt+S"; menu.Items.Add(screenshot); menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("历史记录 · 最近 30 条",null,delegate { OpenResult(); Popup.ShowRecent(); });
            menu.Items.Add("术语收藏",null,delegate { OpenTerms(); });
            buttonItem=new Forms.ToolStripMenuItem("选中 → 小按钮",null,delegate { ChangeMode("button"); });
            autoItem=new Forms.ToolStripMenuItem("选中 → 自动翻译",null,delegate { ChangeMode("auto"); });
            clipboardItem=new Forms.ToolStripMenuItem("剪贴板 → 自动翻译",null,delegate { ChangeMode("clipboard"); });
            companionItem=new Forms.ToolStripMenuItem("仅陪伴 · 关闭划词",null,delegate { ChangeMode("companion"); });
            menu.Items.Add(new Forms.ToolStripSeparator()); trayMode=new Forms.ToolStripMenuItem("识别模式"); trayMode.DropDownItems.AddRange(new Forms.ToolStripItem[]{buttonItem,autoItem,clipboardItem,companionItem}); menu.Items.Add(trayMode);
            trayService=new Forms.ToolStripMenuItem("连接与模型"); trayService.DropDownItems.Add("DeepSeek API",null,delegate { ChangeService("api",settings.Model,settings.Thinking); }); trayService.DropDownItems.Add("DeepSeek 官网 · 登录账号",null,delegate { ChangeService("web",settings.Model,settings.Thinking); }); trayService.DropDownItems.Add("连接设置…",null,delegate { OpenSettings(); }); menu.Items.Add(trayService);
            pauseItem=new Forms.ToolStripMenuItem("暂停自动识别",null,delegate { settings.Enabled=!settings.Enabled; clipboardWatch.Reset(Native.GetClipboardSequenceNumber()); Dismiss(); SaveQuietly(); UpdateTray(); }); menu.Items.Add(pauseItem);
            residentItem=new Forms.ToolStripMenuItem("显示桌面肥鱼",null,delegate { SetResident(!settings.ResidentOrb); }); menu.Items.Add(residentItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("设置…",null,delegate { OpenSettings(); });
            menu.Items.Add("退出大肥译",null,delegate { Application.Current.Shutdown(); });
            TrayTheme.Apply(menu); tray.ContextMenuStrip=menu; tray.DoubleClick+=delegate { OpenResult(); }; UpdateTray();
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        void UpdateTray() {
            if(tray==null) return;
            buttonItem.Checked=settings.Mode=="button"; autoItem.Checked=settings.Mode=="auto"; clipboardItem.Checked=settings.Mode=="clipboard";
            companionItem.Checked=settings.Mode=="companion";
            trayMode.Text="识别模式 · "+(settings.Mode=="companion"?"仅陪伴":settings.Mode=="clipboard"?"剪贴板":settings.Mode=="auto"?"自动":"点击"); trayService.Text=settings.Service=="web"?"连接 · 官网账号":"连接 · DeepSeek API";
            residentItem.Checked=settings.ResidentOrb;
            pauseItem.Checked=!settings.Enabled; pauseItem.Text=settings.Enabled?"暂停自动识别":"恢复自动识别";
            tray.Text="大肥译 · "+(settings.Mode=="companion"?"仅陪伴 · 划词已关闭":!settings.Enabled?"已暂停":settings.Mode=="clipboard"?"剪贴板自动翻译":settings.Mode=="auto"?"自动翻译":"点击划词按钮");
        }
        void SaveQuietly() { if(testing) return; try { settings.Save(SettingsSavePath); } catch(Exception e) { if(tray!=null) tray.ShowBalloonTip(3000,"设置未保存",e.Message,Forms.ToolTipIcon.Warning); } }
        internal void ChangeService(string service,string model,bool thinking) {
            selectionVersion++; pendingAt=0; CancelRequest(); settings.Service=service=="web"?"web":"api"; settings.Model=model; settings.Thinking=thinking;
            Popup.SetService(settings); SaveQuietly(); UpdateTray();
            Popup.SetBusy(false,"已切换 · 下一次翻译使用 "+ServiceProfile.Label(settings));
        }
        DeepSeekWebView Website() { if(webWindow==null) { webWindow=new DeepSeekWebView(); webWindow.ConnectionChanged+=delegate(WebsiteState state) { if(settingsWindow!=null) settingsWindow.ShowWebsiteState(state); }; webWindow.Closed+=delegate { webWindow=null; }; } return webWindow; }
        Task StreamWeb(Settings snapshot,string input,string mode,List<Dictionary<string,string>> conversation,Action<string> chunk,CancellationToken cancellation) { return Website().StreamAsync(snapshot,input,mode,conversation,chunk,cancellation); }
        void OpenWeb(string draft) {
            CancelRequest(); Website();
            webWindow.Prepare(draft); if(!webWindow.IsVisible) webWindow.Show(); if(webWindow.WindowState==WindowState.Minimized) webWindow.WindowState=WindowState.Normal; webWindow.Activate();
        }
        internal void SetResident(bool enabled) { settings.ResidentOrb=enabled; UpdateResident(); SaveQuietly(); UpdateTray(); }
        void UpdateResident() {
            if(disposed||!settings.ResidentOrb||ocrStarting||OcrWindow!=null) { Orb.Suspend(); return; }
            Orb.ApplyPreferences(settings);
            if(!Orb.IsVisible) Orb.RestorePosition(settings);
            if(settings.Mode=="companion"&&request==null) Orb.SetStatus("idle",IdleCaption,0);
        }
        void RememberOrbPosition() { var point=Orb.SavedPosition; settings.OrbX=(int)point.X; settings.OrbY=(int)point.Y; SaveQuietly(); }
        void RefreshTermLinks() { Popup.SetTerms(terms.Search("")); if(termPeek!=null) termPeek.Close(); }
        internal void ShowTerm(TermEntry entry) {
            if(termPeek!=null) termPeek.Close(); termPeek=new TermPeek(entry,settings);
            termPeek.BookRequested+=delegate { OpenTerms(); if(termBook!=null) termBook.Reveal(entry.Id); };
            termPeek.Closed+=delegate { termPeek=null; }; termPeek.Show(); Native.Place(termPeek,Native.Cursor());
        }
        internal void CollectTerm() {
            if(request!=null) return; if(termEditor!=null) { termEditor.Activate(); return; }
            selectionVersion++; pendingAt=0; Chip.Hide(); policy.Clear();
            string candidate=Popup.SelectedAnswerText; if(candidate.Length==0) candidate=Popup.SourceText;
            if(candidate.Length>200) candidate="";
            TermEntry old=null; foreach(var e in terms.Search("")) if(string.Equals(e.Term,candidate.Trim(),StringComparison.OrdinalIgnoreCase)) { old=e; break; }
            var draft=old==null?new TermEntry { Term=candidate,Explanation=Popup.AnswerText,Context=Popup.SourceText }:old.Copy();
            termEditor=new TermEditor(terms,draft); termEditor.Saved+=delegate(TermEntry e) { Popup.SetBusy(false,"已收藏 · "+e.Term); if(termBook!=null) termBook.Refresh(e.Id); }; termEditor.Closed+=delegate { termEditor=null; }; termEditor.Show(); termEditor.Activate();
        }
        internal void OpenTerms() {
            if(termBook!=null) { termBook.Activate(); return; }
            selectionVersion++; pendingAt=0; Chip.Hide(); policy.Clear();
            termBook=new TermBook(terms,settings); termBook.OpenRequested+=delegate(TermEntry e) {
                selectionVersion++; pendingAt=0; CancelRequest(); selected=e.Context.Length==0?e.Term:e.Context; history.Clear();
                history.Add(new Dictionary<string,string>{{"role","system"},{"content",settings.ExplainPrompt}}); history.Add(new Dictionary<string,string>{{"role","user"},{"content",selected}}); history.Add(new Dictionary<string,string>{{"role","assistant"},{"content",e.Explanation}});
                Popup.SetSource(selected); Popup.SelectMode("explain"); Popup.SetAnswer(e.Explanation+(string.IsNullOrWhiteSpace(e.Note)?"":"\n\n### 我的笔记\n"+e.Note)); Popup.SetBusy(false,"术语收藏 · 本次未请求 API"); ShowPopup(); Native.Place(Popup,Native.Cursor());
            }; termBook.Closed+=delegate { termBook=null; }; termBook.Show(); termBook.Activate();
        }
        void OpenResult() {
            if(OcrWindow!=null||ocrStarting) return;
            if(!Popup.IsVisible && cache.Recent.Count>0) { anchor=Native.Cursor(); RestoreRecent(cache.Recent[0]); return; }
            if(Popup.AnswerText.Length==0) { Popup.SetAnswer("选中文字后点「译 / 解」。\n\n也可以点击常驻浮球，选择屏幕识字或翻译剪贴板。"); Popup.SetBusy(false,"大肥译已就绪"); }
            bool visible=Popup.IsVisible; ShowPopup(); if(!visible) { var bounds=Native.Bounds(Orb); Native.Place(Popup,new System.Windows.Point(bounds.X-420,bounds.Y)); }
        }
        internal void ChangeMode(string mode,bool dismiss=true) {
            settings.Mode=mode; clipboardWatch.Reset(Native.GetClipboardSequenceNumber());
            if(mode=="companion") { settings.ResidentOrb=true; CancelRequest(); }
            if(dismiss) Dismiss(); else { selectionVersion++; pendingAt=0; policy.Clear(); Chip.Hide(); }
            UpdateResident(); SaveQuietly(); UpdateTray(); Popup.SetTriggerMode(settings.Mode);
        }
        void OpenSettings() {
            if(settingsWindow!=null) { settingsWindow.Activate(); return; }
            Dismiss();
            var openedSettings=settings.Copy();
            settingsWindow=new SettingsView(settings,delegate(Settings next) {
                next.OrbX=settings.OrbX; next.OrbY=settings.OrbY;
                if(next.PetSize==openedSettings.PetSize) next.PetSize=settings.PetSize;
                if(next.ReducedMotion==openedSettings.ReducedMotion) next.ReducedMotion=settings.ReducedMotion;
                if(next.EdgeHide==openedSettings.EdgeHide) next.EdgeHide=settings.EdgeHide; if(next.HideIdleCaption==openedSettings.HideIdleCaption) next.HideIdleCaption=settings.HideIdleCaption;
                if(next.ReadingFontSize==openedSettings.ReadingFontSize) next.ReadingFontSize=settings.ReadingFontSize; if(next.ReadingLineSpacing==openedSettings.ReadingLineSpacing) next.ReadingLineSpacing=settings.ReadingLineSpacing;
                next.Save(SettingsSavePath); settings=next; Popup.SetAppearance(settings); Popup.SetService(settings); clipboardWatch.Reset(Native.GetClipboardSequenceNumber()); Dismiss(); UpdateResident(); UpdateTray(); Popup.SetTriggerMode(settings.Mode);
                if(termBook!=null) termBook.SetReadingStyle(settings);
            });
            settingsWindow.WebRequested+=delegate { WebPresenter(""); };
            settingsWindow.WebChecker=delegate { return Website().CheckConnectionAsync(); }; if(webWindow!=null) settingsWindow.ShowWebsiteState(webWindow.Connection);
            settingsWindow.Closed+=delegate { settingsWindow=null; }; settingsWindow.Show(); settingsWindow.Activate();
        }
        IntPtr HotkeyHook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled) {
            if(message==0x0312) { handled=true; if(wParam.ToInt32()==1) Ignore(ShortcutSelection()); else if(wParam.ToInt32()==2) Ignore(ClipboardRequest()); else if(wParam.ToInt32()==3) Ignore(StartOcr()); }
            return IntPtr.Zero;
        }
        void Poll(object sender,EventArgs args) {
            if(disposed) return;
            PollClipboard();
            bool left=Native.Down(1),shift=Native.Down(0x10); var point=Native.Cursor(); var window=Native.GetForegroundWindow();
            bool external=settings.Enabled&&settings.Mode!="companion"&&settings.Mode!="clipboard"&&settingsWindow==null&&OcrWindow==null&&!ocrStarting&&!Orb.Interacting&&!Native.IsOwnWindow(window)&&!Native.IsOwnWindow(Native.RootAt(point));
            bool mouseRelease=gestureOrigins.Mouse(left,window,external),shiftRelease=gestureOrigins.Shift(shift,window,external);
            UpdateChipVisibility();
            UpdatePopupVisibility();
            if(Native.Down(0x1B) && (Chip.IsVisible||Popup.IsVisible)) Dismiss();
            if(!settings.Enabled||settingsWindow!=null||OcrWindow!=null||ocrStarting||Orb.Interacting) { mouseDown=left; shiftDown=shift; pendingAt=0; return; }
            if(settings.Mode=="clipboard"||settings.Mode=="companion") { mouseDown=left; shiftDown=shift; pendingAt=0; return; }
            if(left&&!mouseDown) {
                downPoint=point;
                if(external) { selectionVersion++; pendingAt=0; if(!Popup.Pinned) Dismiss(); }
            }
            if(mouseRelease) {
                bool drag=(point-downPoint).Length>3;
                bool doubleClick=clock.ElapsedMilliseconds-lastUpTime<500 && (point-lastUpPoint).Length<7;
                if(drag||doubleClick||shift) ScheduleProbe(window,point);
                lastUpTime=clock.ElapsedMilliseconds; lastUpPoint=point;
            }
            if(shiftRelease) ScheduleProbe(window,point);
            mouseDown=left; shiftDown=shift;
            if(pendingAt>0&&clock.ElapsedMilliseconds>=pendingAt&&!left) { var hwnd=pendingWindow; var p=pendingPoint; int version=pendingVersion; pendingAt=0; Ignore(Probe(hwnd,p,version)); }
        }
        internal void HandleChipAction(int kind,System.Windows.Point point) { if(!Chip.IsVisible) return; if(kind==2&&Native.Bounds(Chip).Contains(point)) return; HideChip(); }
        internal void HandlePopupAction(int kind,System.Windows.Point point) {
            if(disposed||kind!=2||!Popup.IsVisible||Popup.Pinned) return;
            if(Native.IsOwnWindow(Native.RootAt(point))) return;
            Dismiss();
        }
        internal void UpdatePopupVisibility() {
            if(disposed||!Popup.IsVisible) return; IntPtr foreground=Foreground();
            if(popupLifetime.ShouldHide(foreground,Native.IsOwnWindow(foreground),Popup.Pinned)) Dismiss();
        }
        void ShowPopup() { popupLifetime.Start(Foreground()); popupActionAt=inputActions==null?0:inputActions.Sequence; Popup.Show(); }
        void HideChip() { Chip.Hide(); policy.Clear(); if(request==null) Orb.SetStatus("idle",IdleCaption,0); }
        internal void UpdateChipVisibility() {
            if(!Chip.IsVisible) return; if(Chip.Interacting) { chipLifetime.Start(clock.ElapsedMilliseconds); return; } var point=Native.Cursor(); var bounds=Chip.HoverBounds; bounds.Inflate(8,8);
            bool near=bounds.Contains(point); bool far=!near&&(point-anchor).Length>160;
            if(chipLifetime.ShouldHide(clock.ElapsedMilliseconds,near,far,Foreground()!=chipOwner,false)) HideChip();
        }
        internal void PollClipboard() {
            uint sequence=Native.GetClipboardSequenceNumber();
            bool enabled=!disposed&&settings.Enabled&&settings.Mode=="clipboard"&&settingsWindow==null&&OcrWindow==null&&!ocrStarting&&!Orb.Interacting;
            if(!clipboardWatch.ShouldRead(sequence,clock.ElapsedMilliseconds,enabled,Native.IsOwnWindow(Native.GetClipboardOwner()))) return;
            try {
                string text=Clipboard.ContainsText()?Clipboard.GetText().Trim():"";
                if(sequence!=Native.GetClipboardSequenceNumber()) return;
                var action=clipboardWatch.Accept(text);
                if(action==SelectionAction.TooLong) { ShowNotice("剪贴板文字超过 6000 字符，请复制较短的一段。",Native.Cursor()); return; }
                if(action!=SelectionAction.Translate) return;
                selectionVersion++; pendingAt=0; selected=text; anchor=Native.Cursor(); history.Clear();
                Ignore(RequestAsync("translate",null));
            } catch(System.Runtime.InteropServices.ExternalException) { /* Clipboard may briefly be locked; retry on the next tick. */ }
        }
        void ScheduleProbe(IntPtr hwnd,System.Windows.Point point) { pendingWindow=hwnd; pendingPoint=point; pendingAt=clock.ElapsedMilliseconds+130; pendingVersion=++selectionVersion; }
        async Task Probe(IntPtr hwnd,System.Windows.Point point,int version) {
            uint inputStamp=InputActivity();
            if(request==null) Orb.SetStatus("waiting","正在读取选区",0);
            var result=await Reader(hwnd,point);
            if(disposed||version!=selectionVersion||Foreground()!=hwnd||!settings.Enabled) return;
            if(result.Password) { if(request==null) Orb.SetStatus("idle","阅读就绪",0); return; }
            if(result.Text.Length==0 && CopyFallbackPolicy.Allows(settings,WindowProcessName(hwnd))) {
                if(inputStamp==0 || InputActivity()!=inputStamp || !Native.ModifiersReleased()) return;
                try { result.Text=await Copier(hwnd); } catch { result.Text=""; }
                if(disposed||version!=selectionVersion||Foreground()!=hwnd||!settings.Enabled) return;
            }
            if(result.Text.Length>0) await AcceptSelection(result.Text,point);
            else { policy.Clear(); if(request==null) Orb.SetStatus("idle","阅读就绪",0); }
        }
        internal async Task ShortcutSelection() {
            if(OcrWindow!=null||ocrStarting) return;
            int version=++selectionVersion; pendingAt=0; IntPtr hwnd=Foreground();
            try {
                var point=Native.Cursor();
                if(Native.IsOwnWindow(hwnd)) { if(selected.Length>0) await RequestAsync("translate",null); return; }
                var result=await Reader(hwnd,point);
                if(disposed || version!=selectionVersion || Foreground()!=hwnd || result.Password) return;
                string text=result.Text;
                if(text.Length==0) text=await Copier(hwnd);
                if(disposed || version!=selectionVersion || Foreground()!=hwnd) return;
                if(text.Length==0) { ShowNotice("没有读到选中文字。\n可以手动 Ctrl+C，再按 Ctrl+Alt+V 翻译。",point); return; }
                if(text.Length>6000) { ShowNotice("选中文字超过 6000 字符，请缩小选区。",point); return; }
                clipboardWatch.Reset(Native.GetClipboardSequenceNumber());
                selected=text; anchor=point; history.Clear(); await RequestAsync("translate",null);
            } catch(Exception e) { if(!disposed && version==selectionVersion && Foreground()==hwnd) ShowNotice("取词失败："+e.Message,Native.Cursor()); }
        }
        async Task ClipboardRequest() {
            clipboardWatch.Reset(Native.GetClipboardSequenceNumber());
            selectionVersion++; pendingAt=0;
            CancelOcr();
            try {
                var text=Clipboard.ContainsText()?Clipboard.GetText().Trim():"";
                if(text.Length==0) { ShowNotice("剪贴板没有文字，请先复制需要翻译的内容。",Native.Cursor()); return; }
                if(text.Length>6000) { ShowNotice("剪贴板文字超过 6000 字符，请复制较短的一段。",Native.Cursor()); return; }
                selected=text; anchor=Native.Cursor(); history.Clear(); await RequestAsync("translate",null);
            } catch(Exception) { ShowNotice("剪贴板暂时不可用，请稍后重试。",Native.Cursor()); }
        }
        public async Task AcceptSelection(string text,System.Windows.Point point) {
            if(settings.Mode=="clipboard"||settings.Mode=="companion") return;
            var action=policy.Observe(text,settings.Mode,clock.ElapsedMilliseconds);
            if(action==SelectionAction.Ignore) return;
            if(action==SelectionAction.TooLong) { ShowNotice("选中文字超过 6000 字符，请缩小选区。",point); return; }
            int acceptedVersion=++selectionVersion;
            CancelRequest(); selected=text.Trim(); anchor=point; history.Clear();
            if(action==SelectionAction.Button) { Orb.SetStatus("waiting","选好了，点译或解",0); if(!Popup.Pinned) Popup.Hide(); Chip.Show(); Native.Place(Chip,anchor); chipAt=clock.ElapsedMilliseconds; chipLifetime.Start(chipAt); chipOwner=Foreground(); chipActionAt=inputActions==null?0:inputActions.Sequence; }
            else {
                Orb.SetStatus("waiting","稍等，就开始翻译",0);
                await Task.Delay(settings.AutoDelay);
                if(disposed||acceptedVersion!=selectionVersion) return;
                await RequestAsync("translate",null);
            }
        }
        public async Task RequestAsync(string mode,string question,bool force=false) {
            selectionVersion++; pendingAt=0;
            CancelRequest();
            string input=mode=="followup"?question:selectedImage!=null?selectedImage.Prompt:selected;
            if(string.IsNullOrWhiteSpace(input)) return;
            if(mode=="followup" && (history.Count==0||history[history.Count-1]["role"]!="assistant")) { Popup.SetBusy(false,"先完成一次翻译或解释，再继续追问。"); return; }
            if(mode=="followup" && history.Count>=15) { Popup.SetBusy(false,"本次对话已达 7 轮，请重新翻译或解释开始新对话。"); return; }
            retryMode=mode; retryQuestion=question; retrySource=selected; Popup.SetRecovery(false,false);
            bool shouldPlace=!Popup.IsVisible || (!Popup.Pinned && Popup.SourceText!=selected);
            Chip.Hide(); Popup.SetSource(selected); Popup.SetImage(selectedImage); if(mode!="followup") Popup.SelectMode(mode);
            var snapshot=settings.Copy(); snapshot.Image=selectedImage; if(selectedImage!=null&&snapshot.Service!="web") snapshot.Model=snapshot.VisionModel;
            if(selectedImage!=null) { snapshot.TranslatePrompt=selectedImage.Prompt; snapshot.ExplainPrompt=selectedImage.Prompt; }
            if(mode!="followup"&&settings.UseTermPreferences&&selectedImage==null) { string reference=terms.BuildReference(selected); snapshot.TranslatePrompt+=reference; snapshot.ExplainPrompt+=reference; }
            CachedResult cached;
            if(!force && mode!="followup" && cache.TryGet(snapshot,selected,mode,out cached)) {
                Popup.SetAnswer(cached.Answer); Popup.SetBusy(false,"已复用 · 本次未请求模型"); Popup.SetProvider(cached.Model); Popup.SetRecent(cache.Recent);
                Orb.SetStatus("success","已复用，不用再请求",3500); SetConversation(cached); ShowPopup(); if(shouldPlace) Native.Place(Popup,anchor); return;
            }
            Popup.SetProvider(ServiceProfile.Label(snapshot)); Popup.SetAnswer("正在连接，结果会逐步显示…"); Popup.SetBusy(true,(snapshot.Service=="web"?"官网生成":"正在生成")+" · 关闭浮窗可取消");
            Orb.SetStatus("thinking",mode=="explain"?"准备解释这段内容":"准备翻译这段内容",0);
            ShowPopup(); if(shouldPlace) Native.Place(Popup,anchor);
            int id=generation.Next(); var current=new CancellationTokenSource(); request=current;
            var output=new StringBuilder(); var elapsed=Stopwatch.StartNew(); long lastRender=-100;
            var conversation=mode=="followup"?new List<Dictionary<string,string>>(history):null;
            try {
                Action<string> receive=delegate(string chunk) {
                    Application.Current.Dispatcher.BeginInvoke(new Action(delegate {
                        if(disposed||!generation.IsCurrent(id)) return;
                        if(output.Length==0) Orb.SetStatus("working",mode=="explain"?"正在解释":"正在翻译",0);
                        output.Append(chunk);
                        if(elapsed.ElapsedMilliseconds-lastRender>=90) { Popup.SetAnswer(output.ToString()); lastRender=elapsed.ElapsedMilliseconds; }
                    }));
                };
                if(snapshot.Service=="web") await WebStreamer(snapshot,input,mode,conversation,receive,current.Token);
                else await new ApiClient().StreamAsync(snapshot,input,mode,conversation,receive,current.Token);
                // Flush queued deltas before the completion update; same dispatcher priority preserves ordering.
                await Application.Current.Dispatcher.InvokeAsync(delegate { },DispatcherPriority.Background);
                if(!generation.IsCurrent(id)||disposed) return;
                Popup.SetAnswer(output.ToString()); Popup.SetBusy(false,String.Format("完成 · {0:0.0} 秒",elapsed.Elapsed.TotalSeconds));
                Orb.SetStatus("success","完成，看看结果吧",5000);
                if(mode!="followup") {
                    history.Clear(); history.Add(new Dictionary<string,string>{{"role","system"},{"content",(mode=="explain"?snapshot.ExplainPrompt:snapshot.TranslatePrompt)+"\n继续用中文回答用户对选中文字的追问。"}});
                }
                history.Add(new Dictionary<string,string>{{"role","user"},{"content",input}}); history.Add(new Dictionary<string,string>{{"role","assistant"},{"content",output.ToString()}});
                if(mode!="followup") { cache.Store(snapshot,selected,mode,output.ToString()); Popup.SetRecent(cache.Recent); if(cache.LastError.Length>0) Popup.SetBusy(false,cache.LastError); }
            } catch(OperationCanceledException) { }
            catch(Exception e) {
                if(!generation.IsCurrent(id)||disposed) return;
                string message=e is System.Net.Http.HttpRequestException?"无法连接 API，请检查网络、代理和接口地址。":e.Message;
                Popup.SetAnswer((output.Length>0?output.ToString()+"\n\n":"")+message); Popup.SetBusy(false,"未完成 · 点击翻译 / 解释可重试");
                Popup.SetRecovery(true,snapshot.Service=="web");
                Orb.SetStatus("error","这次没完成，可以重试",0);
            } finally { if(request==current) request=null; current.Dispose(); }
        }
        void SetConversation(CachedResult result) {
            history.Clear(); history.Add(new Dictionary<string,string>{{"role","system"},{"content",result.SystemPrompt+"\n继续用中文回答用户对选中文字的追问。"}});
            history.Add(new Dictionary<string,string>{{"role","user"},{"content",result.Source}}); history.Add(new Dictionary<string,string>{{"role","assistant"},{"content",result.Answer}});
        }
        internal void RestoreRecent(CachedResult result) {
            selectionVersion++; pendingAt=0; CancelRequest(); selected=result.Source; SetConversation(result); Chip.Hide();
            Popup.SetSource(result.Source); Popup.SelectMode(result.Mode); Popup.SetProvider(result.Model); Popup.SetAnswer(result.Answer); Popup.SetBusy(false,"历史记录 · 本次未请求模型");
            Orb.SetStatus("success","已打开最近结果",3500);
            bool visible=Popup.IsVisible; ShowPopup(); if(!visible) Native.Place(Popup,anchor);
        }
        void CancelRequest() { generation.Cancel(); if(request!=null) { request.Cancel(); request=null; } Orb.SetStatus("idle",IdleCaption,0); }
        void CancelOcr() { ocrStarting=false; ocrStartupVersion=0; if(OcrWindow!=null) OcrWindow.Close(); if(ocrRequest!=null) { ocrRequest.Cancel(); ocrRequest.Dispose(); ocrRequest=null; } UpdateResident(); }
        internal async Task StartOcr() {
            bool restore=Popup.IsVisible; int version=++selectionVersion; pendingAt=0; CancelOcr(); CancelRequest(); Chip.Hide(); Popup.Hide();
            ocrStarting=true; ocrStartupVersion=version; Orb.Suspend();
            Orb.SetStatus("working_search","正在识别屏幕文字",0);
            OcrOverlay view=null; CancellationTokenSource current=null;
            try {
                await Task.Delay(150); if(disposed||version!=selectionVersion) return;
                var frame=CaptureScreen(); current=new CancellationTokenSource(); ocrRequest=current;
                view=new OcrOverlay(frame,null,settings.Mode=="auto"); OcrWindow=view; ocrStarting=false; ocrStartupVersion=0;
                view.Submitted+=delegate(string text,string mode) { Ignore(SubmitOcr(text,mode)); };
                view.ImageSubmitted+=delegate(ImageRequest image) { Ignore(SubmitImage(image)); };
                view.Closed+=delegate {
                    current.Cancel(); if(OcrWindow==view) OcrWindow=null; if(ocrRequest==current) ocrRequest=null; current.Dispose();
                    if(!view.WasSubmitted) Orb.SetStatus("idle","阅读就绪",0);
                    UpdateResident();
                    if(!view.WasSubmitted && restore && !disposed && version==selectionVersion) { Popup.SetBusy(false,"已退出屏幕识字"); ShowPopup(); }
                };
                view.Show(); var doc=await Recognizer(frame.Png,current.Token);
                if(!disposed && OcrWindow==view && version==selectionVersion) { view.SetDocument(doc); Orb.SetStatus("waiting","拖选要理解的文字",0); }
            } catch(OperationCanceledException) { }
            catch(Exception e) {
                if(!disposed && version==selectionVersion) { if(view!=null && OcrWindow==view) view.SetError("识字失败："+e.Message); else ShowNotice("无法截取屏幕："+e.Message,Native.Cursor()); Orb.SetStatus("error","屏幕识字没有完成",0); }
            }
            finally { if(ocrStartupVersion==version) { ocrStarting=false; ocrStartupVersion=0; UpdateResident(); } }
        }
        async Task SubmitOcr(string text,string mode) {
            selectionVersion++; pendingAt=0; CancelRequest(); selected=text.Trim(); anchor=Native.Cursor(); history.Clear(); policy.Clear(); await RequestAsync(mode,null);
        }
        internal async Task SubmitImage(ImageRequest image) {
            selectionVersion++; pendingAt=0; CancelRequest(); selected=image.Label; selectedImage=image; anchor=Native.Cursor(); history.Clear(); policy.Clear(); await RequestAsync(image.Action=="translate"?"translate":"explain",null);
        }
        void ShowNotice(string text,System.Windows.Point point) { CancelRequest(); selected=""; history.Clear(); Chip.Hide(); anchor=point; Popup.SetSource("大肥译"); Popup.SetAnswer(text); Popup.SetBusy(false,"可在右上角打开设置"); ShowPopup(); Native.Place(Popup,point); }
        public void Dismiss() { selectionVersion++; pendingAt=0; CancelOcr(); CancelRequest(); Chip.Hide(); Popup.Hide(); policy.Clear(); }
        public void SetConversationForTest(string original,string answer) { selected=original; history.Clear(); history.Add(new Dictionary<string,string>{{"role","system"},{"content","test"}}); history.Add(new Dictionary<string,string>{{"role","user"},{"content",original}}); history.Add(new Dictionary<string,string>{{"role","assistant"},{"content",answer}}); }
        public void Dispose() {
            if(disposed) return; disposed=true; selectionVersion++; CancelOcr(); CancelRequest();
            if(poll!=null) poll.Stop(); if(tray!=null) { tray.Visible=false; tray.Dispose(); } if(trayIcon!=null) trayIcon.Dispose();
            if(inputActions!=null) inputActions.Dispose(); terms.Changed-=RefreshTermLinks;
            if(hotkeySource!=null) { Native.UnregisterHotKey(hotkeyHandle,1); Native.UnregisterHotKey(hotkeyHandle,2); Native.UnregisterHotKey(hotkeyHandle,3); hotkeySource.Dispose(); }
            Chip.Hide(); Popup.Hide(); Orb.Suspend(); Orb.Close(); if(settingsWindow!=null) settingsWindow.Close();
            if(webWindow!=null) webWindow.Shutdown();
            if(termBook!=null) termBook.Close(); if(termEditor!=null) termEditor.Close();
            if(termPeek!=null) termPeek.Close();
        }
    }
}
