using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace LightTranslate {
 internal sealed class ReleaseUpdate {
  internal string Version,ArchiveUrl,SumsUrl;
  internal bool Available { get { return new Version(Version)>new Version(ProductLanguage.Version); } }
  internal string ArchiveName { get { return "DaFeiYi-Windows-x64-v"+Version+".zip"; } }
 }
 internal sealed class PreparedUpdate { internal string Package,Work; }
 internal static class AppUpdates {
  // GitHub repository IDs survive renames. The release response comes from this fixed repository.
  internal const string LatestApi="https://api.github.com/repositories/1404583974/releases/latest";
  internal const string LatestPage="https://github.com/gary1110086/dafeiyi/releases/latest";
  static readonly string[] ProgramFiles={"轻译.exe","轻译.exe.config","setup.ps1","update-install.ps1","README.md","README.en.md","LICENSE","THIRD_PARTY_NOTICES.md","SECURITY.md","安装到桌面.cmd","安装并开机启动.cmd","distribution-manifest.json"};
  internal static bool AllowedFile(string relative) {
   if(string.IsNullOrEmpty(relative)||relative.Contains("\\")||relative.StartsWith("/")||relative.Split('/').Any(x=>x==".."||x=="."||x.Length==0)||relative.Contains(":")) return false;
   string file=Path.GetFileName(relative); if(new[]{"settings.json","history.dat","terms.dat"}.Contains(file,StringComparer.OrdinalIgnoreCase)) return false;
   return ProgramFiles.Contains(relative,StringComparer.OrdinalIgnoreCase)||(!relative.Contains("/")&&relative.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))||relative.StartsWith("Assets/",StringComparison.Ordinal)||relative.StartsWith("Libraries/",StringComparison.Ordinal);
  }
  internal static string Hash(string path) { using(var hash=SHA256.Create()) using(var file=File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(file)).Replace("-","").ToLowerInvariant(); }
  static Uri Trusted(string text) {
   Uri uri; if(!Uri.TryCreate(text,UriKind.Absolute,out uri)||uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo.Length>0||!new[]{"api.github.com","github.com","release-assets.githubusercontent.com","objects.githubusercontent.com"}.Contains(uri.Host,StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("更新下载地址无效。"); return uri;
  }
  static async Task Download(string url,string path,long limit,Action<long,long> progress,CancellationToken token) {
   Uri uri=Trusted(url); for(int redirects=0;redirects<6;redirects++) {
    token.ThrowIfCancellationRequested(); var request=(HttpWebRequest)WebRequest.Create(uri); request.AllowAutoRedirect=false; request.UserAgent="DaFeiYi/"+ProductLanguage.Version; request.Timeout=20000; request.ReadWriteTimeout=60000;
    using(token.Register(request.Abort)) using(var response=(HttpWebResponse)await request.GetResponseAsync()) {
     if((int)response.StatusCode>=300&&(int)response.StatusCode<400) { uri=Trusted(new Uri(uri,response.Headers["Location"]).AbsoluteUri); continue; }
     if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>limit) throw new InvalidOperationException("更新文件大小或服务器响应无效。");
     using(var input=response.GetResponseStream()) using(var output=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None,65536,true)) {
      var buffer=new byte[65536]; long total=0; int count; while((count=await input.ReadAsync(buffer,0,buffer.Length,token))>0) { total+=count; if(total>limit) throw new InvalidOperationException("更新文件超过大小限制。"); await output.WriteAsync(buffer,0,count,token); if(progress!=null) progress(total,response.ContentLength); }
     }
     return;
    }
   }
   throw new InvalidOperationException("更新下载重定向过多。");
  }
  static async Task<string> Text(string url,int limit,CancellationToken token) {
   string path=Path.Combine(Path.GetTempPath(),"DaFeiYi-update-"+Guid.NewGuid().ToString("N")+".txt"); try { await Download(url,path,limit,null,token); return File.ReadAllText(path,Encoding.UTF8); } finally { if(File.Exists(path)) File.Delete(path); }
  }
  internal static async Task<ReleaseUpdate> Check(CancellationToken token) {
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   return ParseRelease(await Text(LatestApi,1024*1024,token));
  }
  internal static ReleaseUpdate ParseRelease(string json) {
   var data=new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string,object>;
   if(data==null||!data.ContainsKey("draft")||!data.ContainsKey("prerelease")||!data.ContainsKey("tag_name")||!data.ContainsKey("html_url")||!data.ContainsKey("assets")||Convert.ToBoolean(data["draft"])||Convert.ToBoolean(data["prerelease"])) throw new InvalidOperationException("未找到正式版本。");
   string tag=Convert.ToString(data["tag_name"]); Version version; if(!Regex.IsMatch(tag,@"^v\d+\.\d+\.\d+$")||!System.Version.TryParse(tag.Substring(1),out version)) throw new InvalidOperationException("版本号格式无效。");
   string page=Convert.ToString(data["html_url"]); Uri pageUri=Trusted(page);
   var repository=Regex.Match(page,@"^https://github\.com/gary1110086/([A-Za-z0-9_.-]+)/releases/tag/"+Regex.Escape(tag)+"$");
   if(pageUri.Host!="github.com"||!repository.Success) throw new InvalidOperationException("更新版本来源无效。");
   string downloadRoot="https://github.com/gary1110086/"+repository.Groups[1].Value+"/releases/download/"+tag+"/";
   var update=new ReleaseUpdate { Version=tag.Substring(1) }; var assets=data["assets"] as object[];
   foreach(var asset in assets??new object[0]) { var row=asset as Dictionary<string,object>; if(row==null||!row.ContainsKey("name")||!row.ContainsKey("browser_download_url")) continue; string name=Convert.ToString(row["name"]),url=Convert.ToString(row["browser_download_url"]); if(url!=downloadRoot+name) continue; if(name==update.ArchiveName) update.ArchiveUrl=url; if(name=="SHA256SUMS.txt") update.SumsUrl=url; }
   if(update.ArchiveUrl==null||update.SumsUrl==null) throw new InvalidOperationException("正式版本的安装包或校验文件尚未就绪。"); return update;
  }
  internal static string StageArchive(string archive,string expectedHash,string work,string version) {
   if(!Regex.IsMatch(expectedHash??"",@"^[a-fA-F0-9]{64}$")||!string.Equals(Hash(archive),expectedHash,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("更新包校验失败，旧版未被修改。");
   string root=Path.GetFullPath(Path.Combine(work,"extracted")); var entries=new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total=0;
   using(var zip=ZipFile.OpenRead(archive)) {
    foreach(var entry in zip.Entries) {
     string name=entry.FullName; if(!name.StartsWith("DaFeiYi-Windows-x64/",StringComparison.Ordinal)||name.Contains("\\")) throw new InvalidOperationException("更新包路径无效。");
     if(name.EndsWith("/")) continue; string relative=name.Substring("DaFeiYi-Windows-x64/".Length);
     if(!AllowedFile(relative)||!entries.Add(relative)||(entry.ExternalAttributes>>16&0xF000)==0xA000) throw new InvalidOperationException("更新包含有不允许的文件。");
     total+=entry.Length; if(entry.Length>256L*1024*1024||total>600L*1024*1024) throw new InvalidOperationException("解压文件超过大小限制。");
    }
    Directory.CreateDirectory(root); foreach(var entry in zip.Entries) { if(entry.FullName.EndsWith("/")) continue; string path=Path.GetFullPath(Path.Combine(root,entry.FullName.Substring("DaFeiYi-Windows-x64/".Length))); if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("更新包路径越界。"); Directory.CreateDirectory(Path.GetDirectoryName(path)); entry.ExtractToFile(path,false); }
   }
   ValidatePackage(root,version); return root;
  }
  internal static void ValidatePackage(string root,string version) {
   var data=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(root,"distribution-manifest.json"),Encoding.UTF8)) as Dictionary<string,object>;
   if(data==null||Convert.ToString(data["version"])!=version) throw new InvalidOperationException("更新包版本与清单不一致。");
   var listed=new HashSet<string>(StringComparer.OrdinalIgnoreCase); var files=data["files"] as object[];
   foreach(var entry in files??new object[0]) { var file=entry as Dictionary<string,object>; string relative=file==null?null:Convert.ToString(file["path"]); if(!AllowedFile(relative)||relative=="distribution-manifest.json"||!listed.Add(relative)) throw new InvalidOperationException("更新清单含有不允许的路径。"); string path=Path.Combine(root,relative); if(!File.Exists(path)||!string.Equals(Hash(path),Convert.ToString(file["sha256"]),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("更新文件校验失败，旧版未被修改。"); }
   if(!listed.Contains("轻译.exe")||!listed.Contains("轻译.exe.config")||!listed.Contains("update-install.ps1")) throw new InvalidOperationException("更新包缺少必要程序文件。");
   foreach(string file in Directory.GetFiles(root,"*",SearchOption.AllDirectories)) { string relative=file.Substring(root.Length+1).Replace('\\','/'); if(relative!="distribution-manifest.json"&&!listed.Contains(relative)) throw new InvalidOperationException("更新包存在未列出的文件。"); }
  }
  internal static async Task<PreparedUpdate> Prepare(ReleaseUpdate release,Action<long,long> progress,CancellationToken token) {
   string sums=await Text(release.SumsUrl,65536,token); var match=Regex.Match(sums,@"(?m)^([a-fA-F0-9]{64})\s+"+Regex.Escape(release.ArchiveName)+@"\s*$"); if(!match.Success) throw new InvalidOperationException("安装包校验记录缺失。");
   string work=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DaFeiYiUpdates",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); string archive=Path.Combine(work,"update.zip");
   await Download(release.ArchiveUrl,archive,256L*1024*1024,progress,token); token.ThrowIfCancellationRequested(); string root=await Task.Run(()=>StageArchive(archive,match.Groups[1].Value,work,release.Version),token); token.ThrowIfCancellationRequested(); return new PreparedUpdate { Work=work,Package=root };
  }
  static string Quote(string value) { if(value.Contains("\"")||value.Contains("\r")||value.Contains("\n")) throw new InvalidOperationException("更新路径无效。"); return "\""+value+"\""; }
  internal static void Install(PreparedUpdate update) {
   string current=Path.GetDirectoryName(typeof(Program).Assembly.Location),script=Path.Combine(current,"update-install.ps1"),copy=Path.Combine(update.Work,"apply-update.ps1");
   File.Copy(script,copy,false);
   var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),"-NoProfile -ExecutionPolicy Bypass -File "+Quote(copy)+" -PackageDirectory "+Quote(update.Package)+" -InstallDirectory "+Quote(current)+" -WaitProcessId "+Process.GetCurrentProcess().Id) { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden };
   using(var child=Process.Start(start)) { if(child==null) throw new InvalidOperationException("更新安装器未能启动。"); }
   System.Windows.Application.Current.Shutdown();
  }
 }
}
