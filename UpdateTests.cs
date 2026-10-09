using System;
using System.IO;
using System.Text;
using System.IO.Compression;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace LightTranslate {
 internal static class UpdateTests {
  internal static int Run(string folder) {
   Directory.CreateDirectory(folder); string run=Path.Combine(folder,Guid.NewGuid().ToString("N")); Directory.CreateDirectory(run); int pass=0,fail=0; var report=new StringBuilder();
   Action<string,Action> check=delegate(string name,Action action) { try { action(); pass++; report.AppendLine("PASS "+name); } catch(Exception e) { fail++; report.AppendLine("FAIL "+name+": "+e.Message); } };
   check("release discovery follows repository renames without trusting other owners",delegate {
    foreach(string repository in new[]{"dafeiyi","deepseek-dafeiyi-translator","future-name"}) { var release=ParseRelease(ReleaseFixture(repository)); Assert(release.ArchiveUrl.Contains("/"+repository+"/releases/download/"),"rename rejected"); }
    Reject(()=>ParseRelease(ReleaseFixture("dafeiyi","someone-else")));
    Reject(()=>ParseRelease(ReleaseFixture("dafeiyi",assetOwner:"someone-else")));
    Reject(()=>ParseRelease(ReleaseFixture("dafeiyi",assetRepository:"other-repository")));
   });
   check("release discovery rejects malformed URLs versions drafts and missing checksums",delegate {
    foreach(string change in new[]{"draft","prerelease","tag","query","port","checksum"}) Reject(()=>ParseRelease(ReleaseFixture("dafeiyi",change:change)));
   });
   check("verified checksum and manifest stage a program payload",delegate { string zip=Fixture(Path.Combine(run,"valid")); string root=AppUpdates.StageArchive(zip,AppUpdates.Hash(zip),Path.Combine(run,"stage-valid"),"9.9.9"); Assert(File.ReadAllText(Path.Combine(root,"轻译.exe"))=="new-program","payload absent"); });
   check("corrupt download never begins extraction",delegate { string zip=Fixture(Path.Combine(run,"corrupt")); string work=Path.Combine(run,"stage-corrupt"); Reject(()=>AppUpdates.StageArchive(zip,new string('0',64),work,"9.9.9")); Assert(!Directory.Exists(work),"corrupt package wrote files"); });
   check("archive traversal cannot write a sibling profile",delegate { string zip=Fixture(Path.Combine(run,"traversal"),false,false,true); string work=Path.Combine(run,"stage-traversal"); Reject(()=>AppUpdates.StageArchive(zip,AppUpdates.Hash(zip),work,"9.9.9")); Assert(!Directory.Exists(work),"unsafe archive wrote files"); });
   check("personal stores are forbidden even with a valid archive checksum",delegate { string zip=Fixture(Path.Combine(run,"personal"),false,true); Reject(()=>AppUpdates.StageArchive(zip,AppUpdates.Hash(zip),Path.Combine(run,"stage-personal"),"9.9.9")); Assert(!AppUpdates.AllowedFile("history.dat")&&!AppUpdates.AllowedFile("terms.dat"),"personal stores allowed"); });
   check("manifest checksum and version mismatches cannot install",delegate { string zip=Fixture(Path.Combine(run,"bad-manifest"),true); Reject(()=>AppUpdates.StageArchive(zip,AppUpdates.Hash(zip),Path.Combine(run,"stage-bad"),"9.9.9")); string good=Fixture(Path.Combine(run,"wrong-version")); Reject(()=>AppUpdates.StageArchive(good,AppUpdates.Hash(good),Path.Combine(run,"stage-wrong-version"),"9.9.8")); });
   check("installer retains settings login history terms background and unknown files",delegate {
    string zip=Fixture(Path.Combine(run,"install-good")),package=AppUpdates.StageArchive(zip,AppUpdates.Hash(zip),Path.Combine(run,"stage-install"),"9.9.9"),target=Path.Combine(run,"installed"); Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target,"轻译.exe"),"old-program");
    string[] preserved={"settings.json","history.dat","terms.dat","custom-background.png","DeepSeekWebProfile/login.marker"}; foreach(string name in preserved) { string path=Path.Combine(target,name); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path,"keep-me"); }
    Assert(Install(package,target,Path.Combine(run,"install-success"))==0,"installer failed"); Assert(File.ReadAllText(Path.Combine(target,"轻译.exe"))=="new-program","program not updated"); foreach(string name in preserved) Assert(File.ReadAllText(Path.Combine(target,name))=="keep-me","local file changed: "+name);
   });
   check("locked replacement restores old files and preserves personal data",delegate {
    string zip=Fixture(Path.Combine(run,"locked")),package=AppUpdates.StageArchive(zip,AppUpdates.Hash(zip),Path.Combine(run,"stage-locked"),"9.9.9"),target=Path.Combine(run,"locked-installed"); Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target,"轻译.exe"),"old-program"); string config=Path.Combine(target,"轻译.exe.config"); File.WriteAllText(config,"old-config"); File.WriteAllText(Path.Combine(target,"history.dat"),"keep-history"); string work=Path.Combine(run,"install-rollback");
    using(var locked=new FileStream(config,FileMode.Open,FileAccess.ReadWrite,FileShare.Read)) Assert(Install(package,target,work)!=0,"locked file replaced"); Assert(File.ReadAllText(Path.Combine(target,"轻译.exe"))=="old-program"&&File.ReadAllText(config)=="old-config"&&File.ReadAllText(Path.Combine(target,"history.dat"))=="keep-history","rollback lost data"); Assert(File.ReadAllText(Path.Combine(work,"update-result.json")).Contains("restored"),"rollback result misleading");
   });
   check("installer refuses the user data directory as a program target",delegate {
    string zip=Fixture(Path.Combine(run,"profile-target")),package=AppUpdates.StageArchive(zip,AppUpdates.Hash(zip),Path.Combine(run,"stage-profile"),"9.9.9"),local=Path.Combine(run,"fake-local-data"),target=Path.Combine(local,"LightTranslate"); Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target,"轻译.exe"),"protected-program"); File.WriteAllText(Path.Combine(target,"settings.json"),"protected-settings"); Assert(Install(package,target,Path.Combine(run,"install-profile"),local)!=0,"profile directory accepted"); Assert(File.ReadAllText(Path.Combine(target,"轻译.exe"))=="protected-program"&&File.ReadAllText(Path.Combine(target,"settings.json"))=="protected-settings","profile changed");
   });
   report.AppendLine(string.Format("RESULT {0} passed, {1} failed",pass,fail)); File.WriteAllText(Path.Combine(folder,"update-test.txt"),report.ToString(),new UTF8Encoding(true)); return fail==0?0:1;
  }
  static void Assert(bool value,string message) { if(!value) throw new Exception(message); }
  static ReleaseUpdate ParseRelease(string json) {
   var method=typeof(AppUpdates).GetMethod("ParseRelease",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic); if(method==null) throw new Exception("release parser cannot handle renamed repositories");
   try { return (ReleaseUpdate)method.Invoke(null,new object[]{json}); } catch(System.Reflection.TargetInvocationException e) { throw e.InnerException; }
  }
  static string ReleaseFixture(string repository,string owner="gary1110086",string assetOwner=null,string assetRepository=null,string change=null) {
   string prefix="https://github.com/"+(assetOwner??owner)+"/"+(assetRepository??repository)+"/releases/download/v9.9.9/",page="https://github.com/"+owner+"/"+repository+"/releases/tag/v9.9.9";
   if(change=="query") page+="?fake=1"; if(change=="port") page=page.Replace("github.com/","github.com:444/");
   var assets=new List<object> { new {name="DaFeiYi-Windows-x64-v9.9.9.zip",browser_download_url=prefix+"DaFeiYi-Windows-x64-v9.9.9.zip"} }; if(change!="checksum") assets.Add(new {name="SHA256SUMS.txt",browser_download_url=prefix+"SHA256SUMS.txt"});
   return new JavaScriptSerializer().Serialize(new {draft=change=="draft",prerelease=change=="prerelease",tag_name=change=="tag"?"v9.9.bad":"v9.9.9",html_url=page,assets=assets});
  }
  static void Reject(Action action) { bool rejected=false; try { action(); } catch(InvalidOperationException) { rejected=true; } Assert(rejected,"unsafe update accepted"); }
  static string Fixture(string folder,bool badHash=false,bool personal=false,bool traversal=false) {
   Directory.CreateDirectory(folder); string payload=Path.Combine(folder,"payload"); Directory.CreateDirectory(payload);
   var files=new Dictionary<string,byte[]> {{"轻译.exe",Encoding.UTF8.GetBytes("new-program")},{"轻译.exe.config",Encoding.UTF8.GetBytes("new-config")},{"update-install.ps1",File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"update-install.ps1"))}}; if(personal) files.Add("settings.json",Encoding.UTF8.GetBytes("unwanted-settings")); var listed=new List<object>();
   foreach(var file in files) { string path=Path.Combine(payload,file.Key); File.WriteAllBytes(path,file.Value); listed.Add(new Dictionary<string,string>{{"path",file.Key},{"sha256",badHash?new string('0',64):AppUpdates.Hash(path)}}); }
   File.WriteAllText(Path.Combine(payload,"distribution-manifest.json"),new JavaScriptSerializer().Serialize(new {version="9.9.9",files=listed}),Encoding.UTF8); string archive=Path.Combine(folder,"fixture.zip");
   using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create)) { foreach(string file in Directory.GetFiles(payload)) zip.CreateEntryFromFile(file,"DaFeiYi-Windows-x64/"+Path.GetFileName(file)); if(traversal) using(var writer=new StreamWriter(zip.CreateEntry("DaFeiYi-Windows-x64/../../settings.json").Open())) writer.Write("escape"); } return archive;
  }
  static int Install(string package,string target,string work,string localAppData=null) {
   Directory.CreateDirectory(work); string script=Path.Combine(work,"apply.ps1"); File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"update-install.ps1"),script);
   var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),"-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -PackageDirectory \""+package+"\" -InstallDirectory \""+target+"\" -NoLaunch") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
   if(localAppData!=null) start.EnvironmentVariables["LOCALAPPDATA"]=localAppData;
   using(var p=Process.Start(start)) { var output=p.StandardOutput.ReadToEndAsync(); var error=p.StandardError.ReadToEndAsync(); if(!p.WaitForExit(30000)) { p.Kill(); throw new Exception("installer fixture timed out"); } if(!File.Exists(Path.Combine(work,"update-result.json"))) throw new Exception("installer missing result: "+error.Result); return p.ExitCode; }
  }
 }
}
