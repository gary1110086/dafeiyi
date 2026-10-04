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
        public bool WasSubmitted;
        public bool Ready { get { return document!=null; } }
        ScreenFrame frame; OcrDocument document; bool automatic,dragging,rectangle; Point start;
        Canvas surface; TextBox preview; TextBlock hint; Border card; List<Rectangle> boxes=new List<Rectangle>();
        public OcrOverlay(ScreenFrame capture,OcrDocument doc,bool auto) {
            frame=capture; automatic=auto; Title="大肥译 · 屏幕识字"; Width=capture.Bounds.Width; Height=capture.Bounds.Height;
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; ShowInTaskbar=false; Topmost=true; Background=Brushes.Black;
            var root=new Grid(); root.Children.Add(new Image { Source=frame.Image,Stretch=Stretch.Fill }); root.Children.Add(new Border { Background=new SolidColorBrush(Color.FromArgb(16,15,23,42)),IsHitTestVisible=false });
            surface=new Canvas { Background=Brushes.Transparent,Cursor=Cursors.IBeam,ClipToBounds=true }; root.Children.Add(surface);
            var header=new Grid(); header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var label=new StackPanel(); label.Children.Add(Ui.Text("屏幕识字",14,"#263047")); hint=Ui.Text("正在本机识字…  Esc 退出",11,"#7D879B"); hint.Margin=new Thickness(0,4,0,0); label.Children.Add(hint); header.Children.Add(label);
            var close=Ui.Button("×",Close,false); close.Background=Brushes.Transparent; close.FontSize=20; close.Padding=new Thickness(8,0,8,0); Grid.SetColumn(close,1); header.Children.Add(close);
            var toolbar=new Border { Child=header,Background=Ui.Brush("#FCFDFF"),BorderBrush=Ui.Brush("#DCE3F1"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(16,10,10,10),Margin=new Thickness(18),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,MaxWidth=590 };
            root.Children.Add(toolbar);
            var content=new StackPanel(); content.Children.Add(Ui.Text("选中的原文 · 可以直接修正",11,"#7D879B"));
            preview=new TextBox { TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,Height=80,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,8,0,10),Padding=new Thickness(9,7,9,7) }; content.Children.Add(preview);
            var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
            var reset=Ui.Button("重新选择",delegate { ClearSelection(); },false); reset.Background=Brushes.Transparent; actions.Children.Add(reset);
            var explain=Ui.Button("解释",delegate { Submit("explain"); },false); explain.Margin=new Thickness(6,0,0,0); actions.Children.Add(explain);
            var translate=Ui.Button("翻译",delegate { Submit("translate"); },true); translate.Margin=new Thickness(6,0,0,0); actions.Children.Add(translate); content.Children.Add(actions);
            card=new Border { Child=content,Background=Ui.Brush("#FCFDFF"),BorderBrush=Ui.Brush("#DCE3F1"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(14),Margin=new Thickness(18),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Width=500,Visibility=Visibility.Collapsed }; root.Children.Add(card);
            Content=root;
            Loaded+=delegate { Native.SetBounds(this,frame.Bounds); Activate(); DrawWords(); };
            SizeChanged+=delegate { toolbar.MaxWidth=Math.Max(150,ActualWidth-36); card.Width=Math.Max(170,Math.Min(500,ActualWidth-36)); DrawWords(); };
            surface.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) { if(document==null) return; start=ToImage(e.GetPosition(surface)); rectangle=(Keyboard.Modifiers&ModifierKeys.Shift)!=0; dragging=true; surface.CaptureMouse(); UpdateSelection(start,start,rectangle); e.Handled=true; };
            surface.MouseMove+=delegate(object sender,MouseEventArgs e) { if(dragging) UpdateSelection(start,ToImage(e.GetPosition(surface)),rectangle); };
            surface.MouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e) { if(!dragging) return; dragging=false; surface.ReleaseMouseCapture(); UpdateSelection(start,ToImage(e.GetPosition(surface)),rectangle); if(automatic && SelectedText.Length>0) Submit("translate"); e.Handled=true; };
            KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Escape) Close(); if(e.Key==Key.Enter && (Keyboard.Modifiers&ModifierKeys.Control)!=0) { Submit("translate"); e.Handled=true; } };
            SetDocument(doc);
        }
        Point ToImage(Point point) { return new Point(point.X*frame.Image.PixelWidth/Math.Max(1,surface.ActualWidth),point.Y*frame.Image.PixelHeight/Math.Max(1,surface.ActualHeight)); }
        internal Point ImageToScreen(Point point) { return surface.PointToScreen(new Point(point.X*surface.ActualWidth/frame.Image.PixelWidth,point.Y*surface.ActualHeight/frame.Image.PixelHeight)); }
        public void SetDocument(OcrDocument doc) {
            document=doc; hint.Text=doc==null?"正在本机识字…  Esc 退出":doc.Words.Count==0?"未识别到文字。Esc 退出后放大内容再试。":"拖选文字；Shift + 拖动框选区域 · Esc 退出";
            if(doc!=null&&automatic&&doc.Words.Count>0) hint.Text+=" · 松开后自动翻译"; DrawWords();
        }
        public void SetError(string message) { hint.Text=message+" · Esc 退出"; }
        void DrawWords() {
            if(surface==null) return; surface.Children.Clear(); boxes.Clear(); if(document==null) return;
            double sx=surface.ActualWidth/frame.Image.PixelWidth,sy=surface.ActualHeight/frame.Image.PixelHeight;
            foreach(var word in document.Words) {
                var box=new Rectangle { Width=word.Bounds.Width*sx,Height=word.Bounds.Height*sy,Stroke=Ui.Brush("#A5B4EA"),StrokeThickness=0.6,Opacity=0.65,IsHitTestVisible=false,RadiusX=2,RadiusY=2 };
                Canvas.SetLeft(box,word.Bounds.X*sx); Canvas.SetTop(box,word.Bounds.Y*sy); surface.Children.Add(box); boxes.Add(box);
            }
        }
        void UpdateSelection(Point first,Point last,bool rectangular) {
            if(document==null) return; var indices=document.Indices(first,last,rectangular); var selected=new HashSet<int>(indices); preview.Text=document.Text(indices);
            card.Visibility=!dragging && SelectedText.Length>0?Visibility.Visible:Visibility.Collapsed;
            for(int i=0;i<boxes.Count;i++) { bool on=selected.Contains(i); boxes[i].Fill=on?new SolidColorBrush(Color.FromArgb(65,88,100,216)):Brushes.Transparent; boxes[i].Opacity=on?1:0.65; }
        }
        void ClearSelection() { preview.Clear(); card.Visibility=Visibility.Collapsed; foreach(var box in boxes) box.Fill=Brushes.Transparent; }
        void Submit(string mode) {
            string text=SelectedText; if(text.Length==0) return; if(text.Length>6000) { hint.Text="选中文字超过 6000 字符，请缩小选区或修改原文。"; return; }
            WasSubmitted=true; Close(); if(Submitted!=null) Submitted(text,mode);
        }
        internal void SelectForTest(Point first,Point last,bool rectangular) { UpdateSelection(first,last,rectangular); }
        internal void EditForTest(string text) { preview.Text=text; }
        internal void SubmitForTest(string mode) { Submit(mode); }
    }
}
