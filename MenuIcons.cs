using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
namespace LightTranslate {
    internal static class MenuIcons {
        internal static FrameworkElement Create(string name) {
            string data=name=="screen"?"M1,5V1H5 M11,1H15V5 M15,11V15H11 M5,15H1V11 M4,5H12 M4,8H12 M4,11H9":
                name=="clipboard"?"M5,3H2V15H14V3H11 M5,1H11V5H5Z M5,8H11 M5,11H9":
                name=="result"?"M2,2H14V12H8L4,15V12H2Z M5,5H11 M5,8H9":
                name=="history"?"M2,5A6,6 0 1 1 2,10 M1,1V5H5 M8,4V8L11,10":
                name=="bookmark"?"M4,1H12V15L8,11L4,15Z":
                name=="settings"?"M8,1V3 M8,13V15 M1,8H3 M13,8H15 M3,3L4,4 M12,12L13,13 M3,13L4,12 M12,4L13,3 M8,4A4,4 0 1 1 7.99,4 M8,6A2,2 0 1 1 7.99,6":
                name=="companion"?"M12,1A6,6 0 1 0 15,11A7,7 0 0 1 12,1Z":
                name=="heart"?"M8,14L2,8C-1,3 4,-1 8,4C12,-1 17,3 14,8Z":
                name=="food"?"M2,6H14L12,13H4Z M6,1V3 M10,1V3 M2,15H14":
                name=="actions"?"M4,1L14,8L4,15Z":
                name=="size"?"M1,6V1H6 M10,15H15V10 M1,1L6,6 M15,15L10,10":
                name=="motion"?"M2,5C5,1 7,9 10,5S14,5 15,3 M2,11C5,7 7,15 10,11S14,11 15,9":
                name=="edge"?"M1,1V15 M4,5H14V11H4 M7,3L4,8L7,13":
                name=="label"?"M2,2H14V11H8L4,15V11H2Z":
                "M2,2H14V14H2Z M5,8H11";
            return new Path { Data=System.Windows.Media.Geometry.Parse(data),Stroke=Ui.Brush("#AEBFD3"),StrokeThickness=1.35,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=16,Height=16,Stretch=Stretch.Uniform,IsHitTestVisible=false };
        }
    }
}
