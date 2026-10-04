using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace LightTranslate {
    internal static class PopupResize {
        // Sides: left=1, right=2, top=4, bottom=8. Coordinates below are physical pixels.
        internal static Rect Calculate(Rect bounds,Rect work,int sides,Vector delta,double minWidth,double minHeight) {
            double minW=Math.Min(minWidth,work.Width),minH=Math.Min(minHeight,work.Height);
            double left=bounds.Left,right=bounds.Right,top=bounds.Top,bottom=bounds.Bottom;
            if((sides&1)!=0) left=Math.Max(work.Left,Math.Min(left+delta.X,right-minW));
            if((sides&2)!=0) right=Math.Min(work.Right,Math.Max(right+delta.X,left+minW));
            if((sides&4)!=0) top=Math.Max(work.Top,Math.Min(top+delta.Y,bottom-minH));
            if((sides&8)!=0) bottom=Math.Min(work.Bottom,Math.Max(bottom+delta.Y,top+minH));
            return new Rect(left,top,Math.Max(1,right-left),Math.Max(1,bottom-top));
        }
        internal static void Attach(Window window,Action manualResize) {
            window.ResizeMode=ResizeMode.CanResize; window.MinWidth=360; window.MinHeight=330;
            var frame=(UIElement)window.Content; window.Content=null; var overlay=new Grid(); overlay.Children.Add(frame); window.Content=overlay;
            foreach(int side in new[]{1,2,4,8,5,6,9,10}) {
                int flags=side; bool horizontal=side==4||side==8,vertical=side==1||side==2;
                var grip=new Thumb { Tag="resize:"+side,Width=vertical?8:horizontal?double.NaN:18,Height=horizontal?8:vertical?double.NaN:18,Margin=new Thickness(horizontal?30:8,vertical?30:8,horizontal?30:8,vertical?30:8),
                    HorizontalAlignment=horizontal?HorizontalAlignment.Stretch:(side&1)!=0?HorizontalAlignment.Left:HorizontalAlignment.Right,
                    VerticalAlignment=vertical?VerticalAlignment.Stretch:(side&4)!=0?VerticalAlignment.Top:VerticalAlignment.Bottom,
                    Cursor=horizontal?Cursors.SizeNS:vertical?Cursors.SizeWE:side==5||side==10?Cursors.SizeNWSE:Cursors.SizeNESW,
                    ToolTip="拖动调整窗口大小",Focusable=false };
                var template=new ControlTemplate(typeof(Thumb)); var surface=new FrameworkElementFactory(typeof(Border)); surface.SetValue(Border.BackgroundProperty,Brushes.Transparent); template.VisualTree=surface; grip.Template=template;
                grip.DragDelta+=delegate(object sender,DragDeltaEventArgs e) {
                    if(e.HorizontalChange==0&&e.VerticalChange==0) return; manualResize();
                    var dpi=VisualTreeHelper.GetDpi(window); var bounds=Native.Bounds(window); var area=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle).WorkingArea;
                    var work=new Rect(area.Left+12,area.Top+12,Math.Max(1,area.Width-24),Math.Max(1,area.Height-24));
                    var target=Calculate(bounds,work,flags,new Vector(e.HorizontalChange*dpi.DpiScaleX,e.VerticalChange*dpi.DpiScaleY),window.MinWidth*dpi.DpiScaleX,window.MinHeight*dpi.DpiScaleY); Native.SetBounds(window,target); e.Handled=true;
                }; overlay.Children.Add(grip);
            }
            var mark=new System.Windows.Shapes.Path { Data=System.Windows.Media.Geometry.Parse("M 0,8 L 8,0 M 5,9 L 9,5"),Stroke=Ui.Brush("#6396A6"),StrokeThickness=1.1,Width=10,Height=10,Margin=new Thickness(0,0,18,18),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false,Opacity=0.65 };
            overlay.Children.Add(mark);
        }
    }
}
