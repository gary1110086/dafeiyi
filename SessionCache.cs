using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.IO;
namespace LightTranslate {
    public class CachedResult { public string Source,Answer,Mode,Model,SystemPrompt,CreatedUtc; }
    public class SessionCache {
        public class Item { public string Key; public CachedResult Result; }
        LinkedList<Item> order=new LinkedList<Item>(); Dictionary<string,LinkedListNode<Item>> entries=new Dictionary<string,LinkedListNode<Item>>();
        readonly string historyPath;
        public string LastError="";
        public static string DefaultPath { get { return Path.Combine(Path.GetDirectoryName(Settings.DefaultPath),"history.dat"); } }
        static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength=16*1024*1024 }; }
        public SessionCache() { }
        public SessionCache(string path) {
            historyPath=path;
            if(string.IsNullOrEmpty(path)||!File.Exists(path)) return;
            try {
                if(new FileInfo(path).Length>16*1024*1024) throw new IOException("历史文件过大");
                byte[] plain=ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser);
                var items=Serializer().Deserialize<List<Item>>(Encoding.UTF8.GetString(plain));
                foreach(var item in items) {
                    var r=item==null?null:item.Result;
                    if(r==null||string.IsNullOrEmpty(item.Key)||entries.ContainsKey(item.Key)||string.IsNullOrWhiteSpace(r.Source)||r.Source.Length>6000||string.IsNullOrWhiteSpace(r.Answer)||r.Answer.Length>200000||(r.Mode!="translate"&&r.Mode!="explain")) continue;
                    entries[item.Key]=order.AddLast(item); if(order.Count==30) break;
                }
            } catch(Exception) { order.Clear(); entries.Clear(); LastError="历史记录无法读取，可清空后重新记录。"; }
        }
        void SaveHistory() {
            if(string.IsNullOrEmpty(historyPath)) return;
            string temp=historyPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(historyPath)));
                byte[] plain=Encoding.UTF8.GetBytes(Serializer().Serialize(new List<Item>(order)));
                File.WriteAllBytes(temp,ProtectedData.Protect(plain,null,DataProtectionScope.CurrentUser));
                if(File.Exists(historyPath)) File.Replace(temp,historyPath,null); else File.Move(temp,historyPath);
                LastError="";
            } catch(Exception) { LastError="历史未能保存，本次结果仍可查看。"; }
            finally { try { if(File.Exists(temp)) File.Delete(temp); } catch(IOException) { } }
        }
        public void Clear() { order.Clear(); entries.Clear(); SaveHistory(); }
        public IList<CachedResult> Recent {
            get { var list=new List<CachedResult>(); foreach(var item in order) list.Add(item.Result); return list.AsReadOnly(); }
        }
        static string Key(Settings s,string source,string mode) {
            var profile=new List<string>{(s.BaseUrl??"").Trim().TrimEnd('/'),(s.Model??"").Trim(),s.ApiKey,mode,mode=="explain"?s.ExplainPrompt:s.TranslatePrompt,(source??"").Trim()};
            if(s.Service=="web") profile=new List<string>{"service:web",s.Thinking?"thinking":"standard",mode,mode=="explain"?s.ExplainPrompt:s.TranslatePrompt,(source??"").Trim()};
            // Keep old standard-mode cache keys compatible; isolate thinking results.
            if(s.Service!="web"&&s.Thinking&&ServiceProfile.SupportsThinking(s.Model)) profile.Add("thinking:high");
            using(var hash=SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(profile))));
        }
        public void Store(Settings settings,string source,string mode,string answer) {
            if(string.IsNullOrWhiteSpace(source)||string.IsNullOrWhiteSpace(answer)||mode=="followup") return;
            string key=Key(settings,source,mode); LinkedListNode<Item> old;
            if(entries.TryGetValue(key,out old)) { order.Remove(old); entries.Remove(key); }
            var result=new CachedResult { Source=source.Trim(),Answer=answer,Mode=mode,Model=settings.Service=="web"?ServiceProfile.Label(settings):settings.Model,SystemPrompt=mode=="explain"?settings.ExplainPrompt:settings.TranslatePrompt,CreatedUtc=DateTime.UtcNow.ToString("o") };
            entries[key]=order.AddFirst(new Item { Key=key,Result=result });
            while(order.Count>30) { var last=order.Last; entries.Remove(last.Value.Key); order.RemoveLast(); }
            SaveHistory();
        }
        public bool TryGet(Settings settings,string source,string mode,out CachedResult result) {
            LinkedListNode<Item> hit;
            if(entries.TryGetValue(Key(settings,source,mode),out hit)) { order.Remove(hit); order.AddFirst(hit); result=hit.Value.Result; SaveHistory(); return true; }
            result=null; return false;
        }
    }
}
