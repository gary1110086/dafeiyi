using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
namespace LightTranslate {
    public class OcrOverlay: Window {
        public string SelectedText { get { return preview.Text.Trim(); } }
        public event Action<string,string> Submitted;
        internal event Action<ImageRequest> ImageSubmitted;
        internal bool ImageMode;
        Rect imageArea=Rect.Empty; Image imagePreview; TextBlock previewLabel; Rectangle regionBox; Button translateButton;
        public bool WasSubmitted;
        public bool Ready { get { return document!=null; } }
        ScreenFrame frame; OcrDocument document; bool automatic,dragging,rectangle; Point start;
        Canvas surface; TextBox preview; TextBlock hint; Border card; List<Rectangle> boxes=new List<Rectangle>();
        public OcrOverlay(ScreenFrame capture,OcrDocument doc,bool auto) {
            frame=capture; automatic=auto; Title=ProductLanguage.T("大肥译 · 屏幕识字"); Width=capture.Bounds.Width; Height=capture.Bounds.Height;
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; ShowInTaskbar=false; Topmost=true; Background=Brushes.Black;
            var root=new Grid(); root.Children.Add(new Image { Source=frame.Image,Stretch=Stretch.Fill }); root.Children.Add(new Border { Background=new SolidColorBrush(Color.FromArgb(16,15,23,42)),IsHitTestVisible=false });
            surface=new Canvas { Background=Brushes.Transparent,Cursor=Cursors.IBeam,ClipToBounds=true }; root.Children.Add(surface);
            var header=new Grid(); header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var label=new StackPanel(); label.Children.Add(Ui.Text(ProductLanguage.T("屏幕识字"),14,"#263047")); hint=Ui.Text(ProductLanguage.T("正在本机识字…  Esc 退出"),11,"#7D879B"); hint.Margin=new Thickness(0,4,0,0); label.Children.Add(hint); header.Children.Add(label);
            var kinds=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0) }; kinds.Children.Add(Ui.Button("文字识别",delegate { SetImageMode(false); },false)); var imageMode=Ui.Button("框图 / 公式",delegate { SetImageMode(true); },true); imageMode.Margin=new Thickness(8,0,0,0); kinds.Children.Add(imageMode); label.Children.Add(kinds);
            var close=Ui.Button("×",Close,false); close.Background=Brushes.Transparent; close.FontSize=20; close.Padding=new Thickness(8,0,8,0); Grid.SetColumn(close,1); header.Children.Add(close);
            var toolbar=new Border { Child=header,Background=Ui.Brush("#FCFDFF"),BorderBrush=Ui.Brush("#DCE3F1"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(16,10,10,10),Margin=new Thickness(18),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,MaxWidth=590 };
            root.Children.Add(toolbar);
            var content=new StackPanel(); previewLabel=Ui.Text(ProductLanguage.T("选中的原文 · 可以直接修正"),11,"#7D879B"); content.Children.Add(previewLabel);
            imagePreview=new Image { Height=125,Stretch=Stretch.Uniform,Margin=new Thickness(0,8,0,4),Visibility=Visibility.Collapsed }; content.Children.Add(imagePreview);
            preview=new TextBox { TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,Height=80,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,8,0,10),Padding=new Thickness(9,7,9,7) }; content.Children.Add(preview);
            var actions=new WrapPanel { HorizontalAlignment=HorizontalAlignment.Right };
            var reset=Ui.Button("重新选择",delegate { ClearSelection(); },false); reset.Background=Brushes.Transparent; actions.Children.Add(reset);
            var explain=Ui.Button("解释",delegate { Submit("explain"); },false); explain.Margin=new Thickness(6,0,0,0); actions.Children.Add(explain);
            var formula=Ui.Button("解释公式",delegate { SubmitImage("formula"); },false); formula.Margin=new Thickness(6,0,0,0); formula.Visibility=Visibility.Collapsed; actions.Children.Add(formula);
            var chart=Ui.Button("分析图表",delegate { SubmitImage("chart"); },false); chart.Margin=new Thickness(6,0,0,0); chart.Visibility=Visibility.Collapsed; actions.Children.Add(chart);
            translateButton=Ui.Button("翻译",delegate { Submit("translate"); },true); translateButton.Margin=new Thickness(6,0,0,0); actions.Children.Add(translateButton); content.Children.Add(actions);
            var imageNotice=Ui.Text(ProductLanguage.T("仅发送框选区域给当前连接。官网失败不会自动切换 API；API 图片理解使用 Flash。"),10,"#7D879B"); imageNotice.Margin=new Thickness(0,7,0,0); imageNotice.Visibility=Visibility.Collapsed; content.Children.Add(imageNotice);
            imagePreview.IsVisibleChanged+=delegate { formula.Visibility=chart.Visibility=imageNotice.Visibility=ImageMode?Visibility.Visible:Visibility.Collapsed; };
            card=new Border { Child=content,Background=Ui.Brush("#FCFDFF"),BorderBrush=Ui.Brush("#DCE3F1"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(14),Margin=new Thickness(18),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Width=500,Visibility=Visibility.Collapsed }; root.Children.Add(card);
            Content=root;
            Loaded+=delegate { Native.SetBounds(this,frame.Bounds); Activate(); DrawWords(); };
            SizeChanged+=delegate { toolbar.MaxWidth=Math.Max(150,ActualWidth-36); card.Width=Math.Max(170,Math.Min(500,ActualWidth-36)); DrawWords(); DrawRegion(); };
            surface.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) { if(document==null&&!ImageMode) return; start=ToImage(e.GetPosition(surface)); rectangle=ImageMode||(Keyboard.Modifiers&ModifierKeys.Shift)!=0; dragging=true; surface.CaptureMouse(); UpdateSelection(start,start,rectangle); e.Handled=true; };
            surface.MouseMove+=delegate(object sender,MouseEventArgs e) { if(dragging) UpdateSelection(start,ToImage(e.GetPosition(surface)),rectangle); };
            surface.MouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e) { if(!dragging) return; dragging=false; surface.ReleaseMouseCapture(); UpdateSelection(start,ToImage(e.GetPosition(surface)),rectangle); if(!ImageMode&&automatic && SelectedText.Length>0) Submit("translate"); e.Handled=true; };
            KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Escape) Close(); if(e.Key==Key.Enter && (Keyboard.Modifiers&ModifierKeys.Control)!=0) { Submit("translate"); e.Handled=true; } };
            SetDocument(doc);
        }
        Point ToImage(Point point) { return new Point(point.X*frame.Image.PixelWidth/Math.Max(1,surface.ActualWidth),point.Y*frame.Image.PixelHeight/Math.Max(1,surface.ActualHeight)); }
        internal Point ImageToScreen(Point point) { return surface.PointToScreen(new Point(point.X*surface.ActualWidth/frame.Image.PixelWidth,point.Y*surface.ActualHeight/frame.Image.PixelHeight)); }
        public void SetDocument(OcrDocument doc) {
            document=doc; if(ImageMode) { DrawWords(); DrawRegion(); return; } hint.Text=doc==null?"正在本机识字… 可切换「框图 / 公式」 · Esc 退出":doc.Words.Count==0?"未识别到文字，可切换「框图 / 公式」理解图片。":"拖选文字；Shift + 拖动框选区域 · Esc 退出";
            if(doc!=null&&automatic&&doc.Words.Count>0) hint.Text+=" · 松开后自动翻译"; hint.Text=ProductLanguage.T(hint.Text); DrawWords();
        }
        public void SetError(string message) { if(!ImageMode) hint.Text=message+" · 仍可切换「框图 / 公式」"; }
        internal void SetImageMode(bool value) {
            ImageMode=value; ClearSelection(); surface.Cursor=value?Cursors.Cross:Cursors.IBeam; imagePreview.Visibility=value?Visibility.Visible:Visibility.Collapsed;
            previewLabel.Text=value?"截图问题 · 可补充你想问的内容":"选中的原文 · 可以直接修正"; preview.Tag=value?"例如：这一步为什么成立？":"";
            translateButton.Content=value?"识别并翻译":"翻译"; hint.Text=value?"拖动框选公式、曲线或示意图，预览后点击处理；不会自动上传整屏。":"拖选文字；Shift + 拖动框选区域 · Esc 退出"; previewLabel.Text=ProductLanguage.T(previewLabel.Text); preview.Tag=ProductLanguage.T(Convert.ToString(preview.Tag)); translateButton.Content=ProductLanguage.T(Convert.ToString(translateButton.Content)); hint.Text=ProductLanguage.T(hint.Text); DrawWords();
        }
        void DrawRegion() {
            if(regionBox!=null) surface.Children.Remove(regionBox); regionBox=null; if(!ImageMode||imageArea.IsEmpty) return;
            double sx=surface.ActualWidth/frame.Image.PixelWidth,sy=surface.ActualHeight/frame.Image.PixelHeight;
            regionBox=new Rectangle { Width=imageArea.Width*sx,Height=imageArea.Height*sy,Stroke=Ui.Brush("#8CD3DF"),StrokeThickness=2,Fill=new SolidColorBrush(Color.FromArgb(35,140,211,223)),IsHitTestVisible=false };
            Canvas.SetLeft(regionBox,imageArea.X*sx); Canvas.SetTop(regionBox,imageArea.Y*sy); surface.Children.Add(regionBox);
        }
        void DrawWords() {
            if(surface==null) return; surface.Children.Clear(); boxes.Clear(); if(document==null||ImageMode) return;
            double sx=surface.ActualWidth/frame.Image.PixelWidth,sy=surface.ActualHeight/frame.Image.PixelHeight;
            foreach(var word in document.Words) {
                var box=new Rectangle { Width=word.Bounds.Width*sx,Height=word.Bounds.Height*sy,Stroke=Ui.Brush("#A5B4EA"),StrokeThickness=0.6,Opacity=0.65,IsHitTestVisible=false,RadiusX=2,RadiusY=2 };
                Canvas.SetLeft(box,word.Bounds.X*sx); Canvas.SetTop(box,word.Bounds.Y*sy); surface.Children.Add(box); boxes.Add(box);
            }
        }
        void UpdateSelection(Point first,Point last,bool rectangular) {
            if(ImageMode) {
                imageArea=Rect.Intersect(new Rect(first,last),new Rect(0,0,frame.Image.PixelWidth,frame.Image.PixelHeight)); DrawRegion();
                bool valid=!imageArea.IsEmpty&&imageArea.Width>=8&&imageArea.Height>=8; card.Visibility=!dragging&&valid?Visibility.Visible:Visibility.Collapsed;
                if(!dragging&&valid) { var crop=ImageRequest.Crop(frame,imageArea,"explain",""); using(var stream=new System.IO.MemoryStream(crop.Png)) { var bitmap=new System.Windows.Media.Imaging.BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bitmap.StreamSource=stream; bitmap.EndInit(); imagePreview.Source=bitmap; } hint.Text="已框选 "+crop.Width+"×"+crop.Height+"；点击后仅发送这块图片。"; } return;
            }
            if(document==null) return; var indices=document.Indices(first,last,rectangular); var selected=new HashSet<int>(indices); preview.Text=document.Text(indices);
            card.Visibility=!dragging && SelectedText.Length>0?Visibility.Visible:Visibility.Collapsed;
            for(int i=0;i<boxes.Count;i++) { bool on=selected.Contains(i); boxes[i].Fill=on?new SolidColorBrush(Color.FromArgb(65,88,100,216)):Brushes.Transparent; boxes[i].Opacity=on?1:0.65; }
        }
        void ClearSelection() { preview.Clear(); imageArea=Rect.Empty; imagePreview.Source=null; if(regionBox!=null) surface.Children.Remove(regionBox); regionBox=null; card.Visibility=Visibility.Collapsed; foreach(var box in boxes) box.Fill=Brushes.Transparent; }
        void Submit(string mode) {
            if(ImageMode) { SubmitImage(mode); return; }
            string text=SelectedText; if(text.Length==0) return; if(text.Length>6000) { hint.Text="选中文字超过 6000 字符，请缩小选区或修改原文。"; return; }
            WasSubmitted=true; Close(); if(Submitted!=null) Submitted(text,mode);
        }
        void SubmitImage(string action) {
            if(!ImageMode||imageArea.IsEmpty) return;
            try { if(preview.Text.Length>3000) throw new ArgumentException("补充问题超过 3000 字符，请缩短。"); var image=ImageRequest.Crop(frame,imageArea,action,preview.Text); WasSubmitted=true; Close(); if(ImageSubmitted!=null) ImageSubmitted(image); } catch(Exception e) { hint.Text=e.Message; }
        }
        internal void SelectForTest(Point first,Point last,bool rectangular) { UpdateSelection(first,last,rectangular); }
        internal void EditForTest(string text) { preview.Text=text; }
        internal void SubmitForTest(string mode) { Submit(mode); }
    }
}
