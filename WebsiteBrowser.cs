using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
namespace LightTranslate {
 internal static class WebsiteBrowser {
  // Browser helper only: opening this page does not connect its session to WebView2.
  internal static ProcessStartInfo StartInfo() {
   string chrome=null;
   foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine}) {
    using(var key=hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe")) { var path=key==null?null:key.GetValue(null) as string; if(!string.IsNullOrEmpty(path)&&File.Exists(path)) { chrome=path; break; } }
   }
   if(chrome==null) foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}) {
    string path=Path.Combine(root,@"Google\Chrome\Application\chrome.exe"); if(File.Exists(path)) { chrome=path; break; }
   }
   return chrome==null?new ProcessStartInfo("https://chat.deepseek.com/") { UseShellExecute=true }:new ProcessStartInfo(chrome,"https://chat.deepseek.com/") { UseShellExecute=true };
  }
  internal static void Open() { Process.Start(StartInfo()); }
 }
}
