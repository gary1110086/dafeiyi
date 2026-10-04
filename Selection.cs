using System;
using System.Windows;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Windows.Automation;
namespace LightTranslate {
    public class SelectionResult { public string Text=""; public bool Password; }
    public class SelectionService {
        static int busy;
        public static async Task<SelectionResult> ReadAsync(IntPtr hwnd,Point point) {
            if(hwnd==IntPtr.Zero || Native.IsOwnWindow(hwnd) || Interlocked.CompareExchange(ref busy,1,0)!=0) return new SelectionResult();
            var tcs=new TaskCompletionSource<SelectionResult>();
            var thread=new Thread(delegate() {
                try { tcs.TrySetResult(Read(hwnd,point)); }
                catch(Exception) { tcs.TrySetResult(new SelectionResult()); }
                finally { Interlocked.Exchange(ref busy,0); }
            }) { IsBackground=true,Name="LightTranslate UIA" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            if(await Task.WhenAny(tcs.Task,Task.Delay(1000))!=tcs.Task) return new SelectionResult();
            return await tcs.Task;
        }
        static SelectionResult Read(IntPtr hwnd,Point point) {
            var root=AutomationElement.FromHandle(hwnd); if(root==null) return new SelectionResult();
            var candidates=new List<AutomationElement>();
            try { var focused=AutomationElement.FocusedElement; if(focused!=null && focused.Current.ProcessId==root.Current.ProcessId) candidates.Add(focused); } catch { }
            try { var hit=AutomationElement.FromPoint(point); for(int i=0;i<7 && hit!=null;i++) { if(hit.Current.ProcessId!=root.Current.ProcessId) break; candidates.Add(hit); hit=TreeWalker.ControlViewWalker.GetParent(hit); } } catch { }
            foreach(var candidate in candidates) {
                if(candidate.Current.IsPassword) return new SelectionResult { Password=true };
                var found=Selected(candidate); if(found.Text.Length>0) return found;
            }
            // Breadth-first, with a strict node budget. Never scan the entire desktop.
            var queue=new Queue<AutomationElement>(); queue.Enqueue(root); int visited=0;
            while(queue.Count>0 && visited++<220) {
                var element=queue.Dequeue();
                if(element.Current.IsPassword) continue;
                var found=Selected(element); if(found.Text.Length>0) return found;
                var child=TreeWalker.ControlViewWalker.GetFirstChild(element); int siblings=0;
                while(child!=null && siblings++<35 && queue.Count<220) { queue.Enqueue(child); child=TreeWalker.ControlViewWalker.GetNextSibling(child); }
            }
            return new SelectionResult();
        }
        static SelectionResult Selected(AutomationElement e) {
            object obj;
            if(!e.TryGetCurrentPattern(TextPattern.Pattern,out obj)) return new SelectionResult();
            var ranges=((TextPattern)obj).GetSelection(); var text=new System.Text.StringBuilder();
            foreach(var range in ranges) { string part=range.GetText(6001); if(!string.IsNullOrWhiteSpace(part)) { if(text.Length>0) text.AppendLine(); text.Append(part); } if(text.Length>6000) break; }
            return new SelectionResult { Text=text.ToString().Trim() };
        }
    }
}
