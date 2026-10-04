using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
namespace LightTranslate {
    // Decode the website's own SSE response. This class never makes authenticated requests.
    internal sealed class WebAnswerStream {
        readonly StringBuilder pending=new StringBuilder();
        readonly JavaScriptSerializer json=new JavaScriptSerializer { MaxJsonLength=1024*1024 };
        Dictionary<string,object> document=new Dictionary<string,object>();
        string path="",operation="SET"; public bool Finished; public string Error="";
        public string Answer {
            get {
                object response,fragments; var output=new StringBuilder();
                if(!document.TryGetValue("response",out response)) return "";
                var map=response as Dictionary<string,object>; if(map==null||!map.TryGetValue("fragments",out fragments)) return "";
                var list=fragments as IList; if(list==null) return "";
                foreach(object value in list) { var fragment=value as Dictionary<string,object>; object type,content; if(fragment!=null&&fragment.TryGetValue("type",out type)&&Convert.ToString(type)=="RESPONSE"&&fragment.TryGetValue("content",out content)) output.Append(Convert.ToString(content)); }
                return output.ToString();
            }
        }
        public void Feed(string delta) {
            pending.Append(delta); if(pending.Length>1024*1024) throw new InvalidOperationException("官网返回内容过大，请缩短段落。");
            while(true) { string buffer=pending.ToString(); int end=buffer.IndexOf("\n\n",StringComparison.Ordinal),crlf=buffer.IndexOf("\r\n\r\n",StringComparison.Ordinal),separator=2; if(crlf>=0&&(end<0||crlf<end)) { end=crlf; separator=4; } if(end<0) break; pending.Remove(0,end+separator); Parse(buffer.Substring(0,end)); }
        }
        void Parse(string frame) {
            var data=new StringBuilder(); string eventName="";
            foreach(string line in frame.Split('\n')) { if(line.StartsWith("event:")) eventName=line.Substring(6).Trim(); if(line.StartsWith("data:")) { if(data.Length>0) data.Append('\n'); data.Append(line.Substring(5).Trim()); } }
            string text=data.ToString(); if(text.Length==0) return; if(text=="[DONE]") { Finished=true; return; }
            var item=json.DeserializeObject(text) as Dictionary<string,object>; if(item==null) return;
            if(eventName=="error") { Error="官网生成失败，请打开账号窗口检查额度或验证提示。"; return; }
            if(eventName.Length>0) return; // session IDs and titles are not answer content
            object value; if(!item.TryGetValue("v",out value)) { object code; if(item.TryGetValue("code",out code)&&Convert.ToString(code)!="0") Error="官网拒绝了请求，请打开账号窗口查看提示。"; return; }
            if(item.ContainsKey("p")) path=Convert.ToString(item["p"]); if(item.ContainsKey("o")) operation=Convert.ToString(item["o"]);
            Patch(path,operation,value);
            object response,status; var map=document.TryGetValue("response",out response)?response as Dictionary<string,object>:null;
            if(map!=null&&map.TryGetValue("status",out status)) { string state=Convert.ToString(status); Finished=state=="FINISHED"; if(state=="ERROR"||state=="FAILED") Error="官网未完成回答，请重试。"; }
        }
        void Patch(string target,string op,object value) {
            if(op=="BATCH") { var batch=value as IList; if(batch==null) return; foreach(object entry in batch) { var m=entry as Dictionary<string,object>; if(m==null||!m.ContainsKey("v")) continue; string relative=m.ContainsKey("p")?Convert.ToString(m["p"]):""; Patch((target.Length==0?"":target+"/")+relative,m.ContainsKey("o")?Convert.ToString(m["o"]):"SET",m["v"]); } return; }
            if(target.Length==0) { var root=value as Dictionary<string,object>; if(root!=null) document=root; return; }
            string[] keys=target.Split('/'); object node=document;
            for(int i=0;i<keys.Length-1;i++) { var map=node as Dictionary<string,object>; var list=node as IList; if(map!=null) { object next; if(!map.TryGetValue(keys[i],out next)) { next=new Dictionary<string,object>(); map[keys[i]]=next; } node=next; } else if(list!=null) { int index=Index(keys[i],list.Count); if(index<0||index>=list.Count) return; node=list[index]; } else return; }
            string key=keys[keys.Length-1]; var parent=node as Dictionary<string,object>; var array=node as IList;
            if(parent!=null) { object old; parent.TryGetValue(key,out old); parent[key]=Merge(old,value,op); }
            else if(array!=null) { int index=Index(key,array.Count); if(index>=0&&index<array.Count) array[index]=Merge(array[index],value,op); }
        }
        static int Index(string text,int count) { int n; return int.TryParse(text,out n)?(n<0?count+n:n):-1; }
        static object Merge(object old,object value,string op) {
            if(op!="APPEND") return value;
            if(value is string) return Convert.ToString(old)+Convert.ToString(value);
            var a=old as IList; var b=value as IList;
            if(a!=null&&b!=null) { var result=new List<object>(); foreach(object v in a) result.Add(v); foreach(object v in b) result.Add(v); return result; }
            return value;
        }
    }
}
