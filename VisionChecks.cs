using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace LightTranslate {
 internal static class VisionChecks {
  static void Assert(bool value,string message) { if(!value) throw new Exception(message); }
  internal static void Run(Action<string,Action> check) {
   check("image requests contain real image bytes and preserve the original image on follow-up",delegate {
    var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aL1kAAAAASUVORK5CYII=");
    var s=new Settings { Image=new ImageRequest(png,1,1,"chart","Which shape is blue?") };
    var data=new JavaScriptSerializer().DeserializeObject(ApiClient.BuildBody(s,"Which shape is blue?","explain",null)) as Dictionary<string,object>;
    var messages=(IList)data["messages"]; var first=(Dictionary<string,object>)messages[1];
    Assert(first["content"] is IList,"image request remained plain text");
    var parts=(IList)first["content"]; var image=(Dictionary<string,object>)((Dictionary<string,object>)parts[1])["image_url"];
    Assert(Convert.ToString(image["url"])=="data:image/png;base64,"+Convert.ToBase64String(png),"original image bytes missing");
    var history=new List<Dictionary<string,string>> { new Dictionary<string,string>{{"role","system"},{"content","system"}},new Dictionary<string,string>{{"role","user"},{"content","original question"}},new Dictionary<string,string>{{"role","assistant"},{"content","blue square"}} };
    data=(Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(ApiClient.BuildBody(s,"Why?","followup",history)); messages=(IList)data["messages"];
    Assert(((Dictionary<string,object>)messages[1])["content"] is IList&&Convert.ToString(((Dictionary<string,object>)messages[3])["content"])=="Why?","image did not stay with original user message");
   });
   check("website readiness distinguishes a login, a personal draft, and missing controls",delegate {
    var state=WebsiteState.From(new Dictionary<string,object>{{"editor",true},{"send",true},{"occupied",true}}); Assert(state.Code=="draft"&&!state.Ready,"unsent draft considered ready");
    state=WebsiteState.From(new Dictionary<string,object>{{"editor",true},{"send",true},{"occupied",false}}); Assert(state.Ready,"ready composer rejected");
    state=WebsiteState.From(new Dictionary<string,object>{{"login",true},{"editor",true},{"send",true}}); Assert(state.Code=="login"&&!state.Ready,"login dismissed as layout failure");
   });
   check("output language persists, separates cache, and applies to both services and follow-up",delegate {
    var s=new Settings(); var cache=new SessionCache(); cache.Store(s,"sample","translate","中文"); s.TargetLanguage="en"; CachedResult result; Assert(!cache.TryGet(s,"sample","translate",out result),"language reused old answer");
    Assert(ApiClient.BuildBody(s,"sample","translate",null).Contains("Output language: English")&&ServiceProfile.WebPrompt(s,"sample","translate").Contains("Output language: English"),"language missing from one service");
    var history=new List<Dictionary<string,string>> { new Dictionary<string,string>{{"role","system"},{"content","中文回答"}},new Dictionary<string,string>{{"role","user"},{"content","sample"}} };
    Assert(ApiClient.BuildBody(s,"Why?","followup",history).Contains("Output language: English")&&ServiceProfile.WebPrompt(s,"Why?","followup",history).EndsWith(ProductLanguage.Output(s)),"follow-up language ignored");
    s.InterfaceLanguage="en"; s.OnboardingSeen=true; string path=System.IO.Path.GetTempFileName(); try { s.Save(path); var restored=Settings.Load(path); Assert(restored.TargetLanguage=="en"&&restored.InterfaceLanguage=="en"&&restored.OnboardingSeen,"product preferences not persisted"); } finally { System.IO.File.Delete(path); }
   });
   check("image models validate capabilities and image bytes never enter saved settings",delegate {
    var s=new Settings { Image=new ImageRequest(new byte[]{1,2,3},8,8,"chart","") }; s.Model="deepseek-v4-pro"; bool rejected=false; try { ApiClient.BuildBody(s,"test","explain",null); } catch(ArgumentException) { rejected=true; } Assert(rejected,"Pro accepted image");
    string path=System.IO.Path.GetTempFileName(); try { s.Save(path); Assert(!System.IO.File.ReadAllText(path).Contains("AQID")&&Settings.Load(path).Image==null,"transient image persisted"); } finally { System.IO.File.Delete(path); }
   });
  }
 }
}
