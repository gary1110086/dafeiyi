using System.Collections.Generic;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightTranslate {
    internal static class WhaleTheme {
        static readonly Dictionary<string,string> colors=new Dictionary<string,string>(System.StringComparer.OrdinalIgnoreCase) {
            {"#202A3B","#F4F1E8"},{"#263047","#F4F1E8"},{"#303B50","#DEE8F4"},{"#34405A","#DEE8F4"},{"#4D5870","#CAD7E8"},
            {"#5864D8","#8CD3DF"},{"#737F95","#AEBFD3"},{"#7C879B","#AEBFD3"},{"#7D879B","#AEBFD3"},{"#7F8A9F","#AEBFD3"},
            {"#8690A3","#9DAFC7"},{"#8893A6","#9DAFC7"},{"#929CAE","#9DAFC7"},{"#939CAE","#9DAFC7"},{"#9AA4B6","#92A8C2"},
            {"#C16262","#FFB5AE"},{"#D5DBE5","#344A63"},{"#E1E6EF","#344A63"},{"#E5E9F2","#344A63"},{"#E9ECFF","#274860"},
            {"#F0F2F7","#21344F"},{"#F1F3F8","#21344F"},{"#F4F6FA","#101E32"},{"#F7F8FB","#101E32"},{"#FEFEFF","#17263E"},{"#FCFDFF","#17263E"},
            {"#DCE3F1","#42607D"},{"#A5B4EA","#94CDDD"}
        };
        internal static string Color(string original) { string value; return colors.TryGetValue(original,out value)?value:original; }
        internal static string Xaml(string source) { foreach(var pair in colors) source=source.Replace(pair.Key,pair.Value); return source; }
        internal static FrameworkElement Portrait(double size) {
            BitmapSource face=PortraitImage;
            var frame=face==null?PetAssets.Shared.Image("idle",0):null;
            if(frame!=null) { face=new CroppedBitmap(frame,new Int32Rect((int)(frame.PixelWidth*0.27),(int)(frame.PixelHeight*0.18),(int)(frame.PixelWidth*0.50),(int)(frame.PixelHeight*0.52))); face.Freeze(); }
            return new Border { Width=size,Height=size,Background=Ui.Brush("#F4F6FA"),CornerRadius=new CornerRadius(size/2),Margin=new Thickness(0,0,8,0),Child=new Image { Source=face,Stretch=Stretch.Uniform },IsHitTestVisible=false };
        }
        internal static void Install(Application app) {
            app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='PasswordBox'><Setter Property='Foreground' Value='#DEE8F4'/><Setter Property='CaretBrush' Value='#8CD3DF'/><Setter Property='SelectionBrush' Value='#406982'/></Style>
<Style TargetType='CheckBox'><Setter Property='Foreground' Value='#DEE8F4'/></Style>
<Style TargetType='RadioButton'><Setter Property='Foreground' Value='#DEE8F4'/></Style>
<Style TargetType='Expander'><Setter Property='Foreground' Value='#DEE8F4'/></Style>
<Style TargetType='ContextMenu'><Setter Property='Background' Value='#17263E'/><Setter Property='Foreground' Value='#DEE8F4'/><Setter Property='FontSize' Value='12'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ContextMenu'><Border Background='#17263E' BorderBrush='#46617E' BorderThickness='1' CornerRadius='13' Padding='5'><StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Cycle'/></Border></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='MenuItem'><Setter Property='Foreground' Value='#DEE8F4'/><Setter Property='Padding' Value='10,7'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='MenuItem'><Grid><Border x:Name='Body' CornerRadius='7' Padding='{TemplateBinding Padding}' Background='Transparent'><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='24'/><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/><ColumnDefinition Width='14'/></Grid.ColumnDefinitions><ContentPresenter x:Name='MenuIcon' ContentSource='Icon' VerticalAlignment='Center'/><TextBlock x:Name='Check' Text='✓' Visibility='Hidden' Foreground='#8CD3DF'/><ContentPresenter Grid.Column='1' ContentSource='Header' RecognizesAccessKey='True' VerticalAlignment='Center' Margin='0,0,20,0'/><TextBlock Grid.Column='2' Text='{TemplateBinding InputGestureText}' Foreground='#91A7C0' FontSize='10' VerticalAlignment='Center'/><TextBlock x:Name='Arrow' Grid.Column='3' Text='›' Foreground='#91A7C0' Visibility='Hidden' HorizontalAlignment='Right'/></Grid></Border><Popup x:Name='PART_Popup' Placement='Right' IsOpen='{TemplateBinding IsSubmenuOpen}' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'><Border Background='#17263E' BorderBrush='#46617E' BorderThickness='1' CornerRadius='12' Padding='5'><StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Cycle'/></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Body' Property='Background' Value='#294760'/></Trigger><Trigger Property='IsChecked' Value='True'><Setter TargetName='Check' Property='Visibility' Value='Visible'/><Setter TargetName='MenuIcon' Property='Visibility' Value='Hidden'/></Trigger><Trigger Property='HasItems' Value='True'><Setter TargetName='Arrow' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='Separator'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Separator'><Border Height='1' Background='#344A63' Margin='9,5'/></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ToolTip'><Setter Property='Background' Value='#17263E'/><Setter Property='Foreground' Value='#DEE8F4'/><Setter Property='BorderBrush' Value='#46617E'/><Setter Property='Padding' Value='10,7'/></Style>
<Style TargetType='ComboBox'><Setter Property='Foreground' Value='#DEE8F4'/><Setter Property='Background' Value='#101E32'/><Setter Property='MinHeight' Value='34'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid><Border Background='{TemplateBinding Background}' BorderBrush='#344A63' BorderThickness='1' CornerRadius='9'/><Grid Margin='10,5'><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='22'/></Grid.ColumnDefinitions><ContentPresenter x:Name='Selection' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' VerticalAlignment='Center'/><TextBox x:Name='PART_EditableTextBox' Style='{x:Null}' Background='Transparent' Foreground='#DEE8F4' CaretBrush='#8CD3DF' BorderThickness='0' Padding='0' Visibility='Hidden' VerticalAlignment='Center' IsReadOnly='{TemplateBinding IsReadOnly}'/><ToggleButton Grid.Column='1' IsChecked='{Binding IsDropDownOpen,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}' Focusable='False' ClickMode='Press'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='Transparent'><TextBlock Text='⌄' Foreground='#91A7C0' FontSize='16' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate></ToggleButton.Template></ToggleButton></Grid><Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False'><Border Background='#17263E' BorderBrush='#46617E' BorderThickness='1' CornerRadius='9' Padding='4' MinWidth='{TemplateBinding ActualWidth}'><ScrollViewer MaxHeight='240'><ItemsPresenter/></ScrollViewer></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property='IsEditable' Value='True'><Setter TargetName='Selection' Property='Visibility' Value='Hidden'/><Setter TargetName='PART_EditableTextBox' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ComboBoxItem'><Setter Property='Foreground' Value='#DEE8F4'/><Setter Property='Padding' Value='8,7'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='Body' Background='Transparent' CornerRadius='5' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Body' Property='Background' Value='#294760'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>"));
        }
        static BitmapSource portraitImage;
        static bool portraitLoaded;
        internal static BitmapSource PortraitImage {
            get {
                if(!portraitLoaded) {
                    portraitLoaded=true;
                    try {
                        var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad;
                        image.UriSource=new System.Uri(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"Assets","Reading","whale-portrait.png"));
                        image.EndInit(); image.Freeze(); portraitImage=image;
                    } catch(System.IO.IOException) { } catch(System.NotSupportedException) { }
                }
                return portraitImage;
            }
        }
    }
}
