using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace LightTranslate {
 internal sealed class WelcomeView:Window {
  internal readonly TextBox Sample;
  internal readonly TextBlock Result;
  internal readonly Button Translate;
  internal WelcomeView(Action settings,Action companion) {
   Title=ProductLanguage.T("启动体验"); Width=530; Height=480; WindowStartupLocation=WindowStartupLocation.CenterScreen;
   var root=new Grid(); root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
   var header=new DockPanel(); var close=Ui.Button("×",Close,false); DockPanel.SetDock(close,Dock.Right); header.Children.Add(close); header.Children.Add(WhaleTheme.Portrait(52)); var name=Ui.Text(ProductLanguage.T("大肥译"),23,"#DEE8F4"); name.Margin=new Thickness(12,8,0,0); header.Children.Add(name); Ui.Row(root,header,0);
   var content=new StackPanel { Margin=new Thickness(0,22,0,0) }; content.Children.Add(Ui.Text(ProductLanguage.T("选中下面的一段英文，再点翻译。不用登录，也不会请求 API。"),13,"#AEBFD3"));
   Sample=new TextBox { Text="A crystal lattice is a periodic arrangement of points in space.",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,FontSize=16,Margin=new Thickness(0,16,0,12),Padding=new Thickness(12),MinHeight=76 };
   content.Children.Add(Sample); Translate=Ui.Button("翻译",delegate { Result.Text=Sample.SelectedText.Trim().Length==0?ProductLanguage.T("先选中文字，试试划词的感觉。"):ProductLanguage.Interface=="en"?"A crystal lattice is a periodic arrangement of points in space.\n\n"+ProductLanguage.T("这是内置示例译文，未请求 AI。"):"晶格是空间中周期性排列的点阵。\n\n"+ProductLanguage.T("这是内置示例译文，未请求 AI。"); },true); Translate.HorizontalAlignment=HorizontalAlignment.Left; content.Children.Add(Translate);
   Result=Ui.Text("",14,"#DEE8F4"); Result.Margin=new Thickness(0,12,0,0); content.Children.Add(Result); Ui.Row(root,new ScrollViewer { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto },1);
   var footer=new WrapPanel { Margin=new Thickness(0,15,0,0) }; var choose=Ui.Button("选择连接方式",delegate { Close(); if(settings!=null) settings(); else { var view=new SettingsView(Settings.Load(Settings.DefaultPath),delegate(Settings value) { value.Save(Settings.DefaultPath); }); view.Show(); } },true); footer.Children.Add(choose);
   var pet=Ui.Button("开始陪伴",delegate { Close(); if(companion!=null) companion(); },false); pet.Margin=new Thickness(10,0,0,0); footer.Children.Add(pet); Ui.Row(root,footer,2); Ui.Frame(this,root,22);
   Loaded+=delegate { Height=Math.Min(Height,SystemParameters.WorkArea.Height-32); }; KeyDown+=delegate(object sender,System.Windows.Input.KeyEventArgs e) { if(e.Key==System.Windows.Input.Key.Escape) Close(); };
  }
 }
}
