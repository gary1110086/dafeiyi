using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace LightTranslate {
 internal sealed class UpdatePanel:StackPanel {
  internal Func<CancellationToken,Task<ReleaseUpdate>> Checker=AppUpdates.Check;
  internal Func<ReleaseUpdate,Action<long,long>,CancellationToken,Task<PreparedUpdate>> Downloader=AppUpdates.Prepare;
  internal Action<PreparedUpdate> Installer=AppUpdates.Install;
  internal readonly TextBlock Status; internal readonly Button CheckButton,InstallButton;
  readonly CancellationTokenSource cancellation=new CancellationTokenSource(); readonly Action save;
  ReleaseUpdate latest; bool busy;
  internal UpdatePanel(Action saveBeforeInstall) {
   save=saveBeforeInstall; Children.Add(Ui.Text(ProductLanguage.T("当前版本")+"  "+ProductLanguage.Version,18,"#8CD3DF"));
   var note=Ui.Text(ProductLanguage.T("更新只替换程序。API 配置、官网登录、历史、术语收藏和自选背景继续保留；桌面快捷方式与开机启动也保留。"),12,"#AEBFD3"); note.Margin=new Thickness(0,14,0,16); Children.Add(note);
   Status=Ui.Text(ProductLanguage.T("点击检查更新。检查只连接公开 GitHub，不发送个人配置。"),12,"#DEE8F4"); Children.Add(Status);
   CheckButton=Ui.Button("检查更新",async delegate { if(busy) return; busy=true; CheckButton.IsEnabled=false; InstallButton.IsEnabled=false; try { using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token)) { timeout.CancelAfter(20000); latest=await Checker(timeout.Token); } if(cancellation.IsCancellationRequested) return; Status.Text=latest.Available?ProductLanguage.T("发现新版")+" "+latest.Version:ProductLanguage.T("已是最新版本"); InstallButton.IsEnabled=latest.Available; } catch(Exception) { if(!cancellation.IsCancellationRequested) Status.Text=ProductLanguage.T("暂时无法检查更新。当前版本仍可使用，也可打开下载页。"); } finally { busy=false; CheckButton.IsEnabled=true; } },false); CheckButton.Margin=new Thickness(0,16,0,8); CheckButton.HorizontalAlignment=HorizontalAlignment.Left; Children.Add(CheckButton);
   InstallButton=Ui.Button("保存设置并更新",async delegate { if(busy||latest==null||!latest.Available) return; busy=true; CheckButton.IsEnabled=false; InstallButton.IsEnabled=false; Status.Text=ProductLanguage.T("正在下载与校验，完成后会重启应用…"); try { var prepared=await Downloader(latest,delegate(long done,long total) { if(!cancellation.IsCancellationRequested) Status.Text=ProductLanguage.T("下载更新")+" · "+(total>0?(done*100/total)+"%":(done/1024/1024)+" MB"); },cancellation.Token); if(cancellation.IsCancellationRequested) return; if(save!=null) save(); Installer(prepared); } catch(Exception) { if(!cancellation.IsCancellationRequested) { Status.Text=ProductLanguage.T("更新未完成，当前程序与本地数据保留。可重试或打开下载页。"); InstallButton.IsEnabled=true; } } finally { busy=false; CheckButton.IsEnabled=true; } },true); InstallButton.IsEnabled=false; InstallButton.HorizontalAlignment=HorizontalAlignment.Left; Children.Add(InstallButton);
   var fallback=Ui.Button("打开最新版本下载",delegate { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppUpdates.LatestPage) { UseShellExecute=true }); },false); fallback.Margin=new Thickness(0,16,0,0); fallback.HorizontalAlignment=HorizontalAlignment.Left; Children.Add(fallback);
   var manual=Ui.Text(ProductLanguage.T("旧版首次升级：下载新版、完整解压，运行安装脚本即可覆盖更新，无需先卸载。新版以后可在这里完成更新。"),11,"#AEBFD3"); manual.Margin=new Thickness(0,14,0,0); Children.Add(manual);
  }
  internal void Cancel() { cancellation.Cancel(); }
 }
}
