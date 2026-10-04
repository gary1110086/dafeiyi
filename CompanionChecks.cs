using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
namespace LightTranslate {
    internal static class CompanionChecks {
        static void Assert(bool ok,string reason) { if(!ok) throw new Exception(reason); }
        internal static void Run(Action<string,Action> check) {
            check("term collection exceeds history limit and duplicate save cannot erase notes",delegate {
                var store=new TermStore(null); for(int i=0;i<40;i++) store.SaveTerm("","term "+i,"explanation","context","my note");
                Assert(store.Search("").Count==40,"terms were limited to recent 30"); bool rejected=false; try { store.SaveTerm("","TERM 1","new answer","",""); } catch(ArgumentException) { rejected=true; }
                Assert(rejected&&store.Search("term 1").Find(x=>x.Term=="term 1").Note=="my note","duplicate silently erased note");
            });
            check("failed collection write does not commit a phantom favorite",delegate {
                string folder=Path.Combine(Path.GetTempPath(),"dafeiyi-locked-"+Guid.NewGuid()); Directory.CreateDirectory(folder); string path=Path.Combine(folder,"terms.dat");
                try { var store=new TermStore(path); store.SaveTerm("","first","answer","","note"); using(var locked=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.None)) { bool rejected=false; try { store.SaveTerm("","second","answer","",""); } catch(IOException) { rejected=true; } Assert(rejected&&store.Search("").Count==1,"failed write committed in memory"); } Assert(new TermStore(path).Search("").Count==1,"failed write destroyed persisted terms"); }
                finally { if(File.Exists(path)) File.Delete(path); Directory.Delete(folder); }
            });
            check("edge and idle-caption preferences survive restart",delegate {
                string path=Path.Combine(Path.GetTempPath(),"dafeiyi-prefs-"+Guid.NewGuid()+".json");
                try { var settings=new Settings { EdgeHide=false,HideIdleCaption=false,Mode="companion" }; settings.Save(path); var copy=Settings.Load(path); Assert(!copy.EdgeHide&&!copy.HideIdleCaption&&copy.Mode=="companion","companion display preferences lost"); } finally { if(File.Exists(path)) File.Delete(path); }
            });
            check("terminology survives encrypted restart, search, update and deletion",delegate {
                var type=typeof(Settings).Assembly.GetType("LightTranslate.TermStore"); Assert(type!=null,"TermStore missing");
                string path=Path.Combine(Path.GetTempPath(),"dafeiyi-terms-"+Guid.NewGuid()+".dat");
                try {
                    object store=Activator.CreateInstance(type,new object[]{path});
                    // Reflection keeps the initial test build independent of the new implementation.
                    var save=type.GetMethod("SaveTerm");
                    object item=save.Invoke(store,new object[]{"","lattice","**晶格**\n\\(a^2\\)","原文 context","区别 basis"});
                    string id=(string)item.GetType().GetField("Id").GetValue(item);
                    Assert(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("lattice"),"plaintext terms");
                    store=Activator.CreateInstance(type,new object[]{path});
                    var found=(IList)type.GetMethod("Search").Invoke(store,new object[]{"BASIS"}); Assert(found.Count==1,"search lost notes");
                    save.Invoke(store,new object[]{id,"Lattice","updated","context","note"});
                    Assert(((IList)type.GetMethod("Search").Invoke(store,new object[]{""})).Count==1,"edit duplicated term");
                    type.GetMethod("Delete").Invoke(store,new object[]{id});
                    Assert(((IList)type.GetMethod("Search").Invoke(Activator.CreateInstance(type,new object[]{path}),new object[]{""})).Count==0,"delete did not persist");
                } finally { if(File.Exists(path)) File.Delete(path); }
            });
            check("term storage failure retains previous memory and corrupt data is not overwritten",delegate {
                var t=typeof(Settings).Assembly.GetType("LightTranslate.TermStore"); Assert(t!=null,"TermStore missing");
                string path=Path.Combine(Path.GetTempPath(),"dafeiyi-bad-"+Guid.NewGuid()+".dat"); File.WriteAllText(path,"corrupt");
                try { object store=Activator.CreateInstance(t,new object[]{path}); bool rejected=false; try { t.GetMethod("SaveTerm").Invoke(store,new object[]{"","term","answer","",""}); } catch(TargetInvocationException) { rejected=true; } Assert(rejected&&File.ReadAllText(path)=="corrupt","damaged collection overwritten"); }
                finally { File.Delete(path); }
            });
            check("edge reveal reverses smoothly, waits on leave, respects locks and reduced motion",delegate {
                var t=typeof(Settings).Assembly.GetType("LightTranslate.EdgeReveal"); Assert(t!=null,"EdgeReveal missing"); object p=Activator.CreateInstance(t);
                var tick=t.GetMethod("Tick"); var value=t.GetProperty("Progress");
                tick.Invoke(p,new object[]{200,true,false,false}); double first=(double)value.GetValue(p,null); Assert(first>0&&first<1,"no continuous reveal");
                tick.Invoke(p,new object[]{200,false,false,false}); Assert((double)value.GetValue(p,null)>=first,"collapsed immediately after leaving");
                tick.Invoke(p,new object[]{2000,false,true,false}); Assert((double)value.GetValue(p,null)==1,"menu lock collapsed pet");
                tick.Invoke(p,new object[]{2000,false,false,true}); Assert((double)value.GetValue(p,null)==0,"reduced motion never settles");
            });
        }
    }
}
