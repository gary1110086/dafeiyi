using System;
using System.IO;
using System.Collections;
using System.Reflection;
namespace LightTranslate {
    internal static class ReadingChecks {
        static void Assert(bool ok,string message) { if(!ok) throw new Exception(message); }
        internal static void Run(Action<string,Action> check) {
            check("selection chip expires sooner but protects hover and allows deliberate departure",delegate {
                var type=typeof(Settings).Assembly.GetType("LightTranslate.ChipLifetime"); Assert(type!=null,"ChipLifetime missing"); var policy=Activator.CreateInstance(type); var start=type.GetMethod("Start"); var tick=type.GetMethod("ShouldHide");
                start.Invoke(policy,new object[]{0L});
                Func<long,bool,bool,bool,bool,bool> hide=(now,near,far,changed,action)=>(bool)tick.Invoke(policy,new object[]{now,near,far,changed,action});
                Assert(!hide(200,false,false,false,false),"dismissed before user could approach"); Assert(!hide(10000,true,false,false,false),"hovered chip expired"); Assert(!hide(10100,false,true,false,false),"no departure grace"); Assert(hide(10600,false,true,false,false),"hover departure stuck");
                start.Invoke(policy,new object[]{0L}); Assert(hide(2400,false,false,false,false),"still waits seven seconds"); start.Invoke(policy,new object[]{0L}); Assert(hide(50,true,false,true,false),"foreground change left chip floating"); start.Invoke(policy,new object[]{0L}); Assert(hide(50,true,false,false,true),"typing or scroll did not dismiss");
            });
            check("reading typography and terminology preference survive settings restart",delegate {
                var font=typeof(Settings).GetField("ReadingFontSize"); var spacing=typeof(Settings).GetField("ReadingLineSpacing"); var enabled=typeof(Settings).GetField("UseTermPreferences"); Assert(font!=null&&spacing!=null&&enabled!=null,"reading settings missing");
                string path=Path.Combine(Path.GetTempPath(),"dafeiyi-reading-"+Guid.NewGuid()+".json"); try { var settings=new Settings(); font.SetValue(settings,18); spacing.SetValue(settings,1.9); enabled.SetValue(settings,false); settings.Save(path); var loaded=Settings.Load(path); Assert((int)font.GetValue(loaded)==18&&Math.Abs((double)spacing.GetValue(loaded)-1.9)<0.001&&!(bool)enabled.GetValue(loaded),"reading preferences lost"); } finally { if(File.Exists(path)) File.Delete(path); }
            });
            check("term matches choose longest phrases, ignore English substrings and preserve offsets",delegate {
                var type=typeof(Settings).Assembly.GetType("LightTranslate.TermLookup"); Assert(type!=null,"TermLookup missing"); var entries=new System.Collections.Generic.List<TermEntry> { new TermEntry { Id="1",Term="lattice" },new TermEntry { Id="2",Term="Bravais lattice" },new TermEntry { Id="3",Term="晶格" } }; object lookup=Activator.CreateInstance(type,new object[]{entries});
                var matches=(IList)type.GetMethod("Find").Invoke(lookup,new object[]{"BRAVAIS lattice; lattices; 晶格常数; lattice."}); Assert(matches.Count==3,"boundary or longest phrase mismatch"); object first=matches[0]; Assert((int)first.GetType().GetField("Start").GetValue(first)==0&&((TermEntry)first.GetType().GetField("Entry").GetValue(first)).Id=="2","phrase offsets or identity lost");
            });
            check("preferred translation persists and request reference sends only relevant user preferences",delegate {
                var field=typeof(TermEntry).GetField("PreferredTranslation"); var save=typeof(TermStore).GetMethod("SaveTermWithPreference"); var reference=typeof(TermStore).GetMethod("BuildReference"); Assert(field!=null&&save!=null&&reference!=null,"preferred translations missing");
                string path=Path.Combine(Path.GetTempPath(),"dafeiyi-preference-"+Guid.NewGuid()+".dat"); try { var store=new TermStore(path); var entry=(TermEntry)save.Invoke(store,new object[]{"","lattice","private explanation","private context","private note","点阵"}); store.SaveTerm(entry.Id,"lattice","new private explanation","context","note"); var loaded=new TermStore(path); Assert((string)field.GetValue(loaded.Search("")[0])=="点阵","ordinary edit erased preferred translation"); string text=(string)reference.Invoke(loaded,new object[]{"A lattice with a basis"}); Assert(text.Contains("点阵")&&!text.Contains("private")&&!text.Contains("note"),"private explanation or notes leaked into prompt"); Assert((string)reference.Invoke(loaded,new object[]{"unrelated lattices"})=="","irrelevant term added"); } finally { if(File.Exists(path)) File.Delete(path); }
            });
        }
    }
}
