using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
namespace LightTranslate {
    // Only action type/position is observed. Key values and typed text are never retained.
    internal sealed class InputActionObserver:IDisposable {
        delegate IntPtr Hook(int code,IntPtr message,IntPtr data);
        readonly Hook mouseCallback,keyCallback; readonly Dispatcher dispatcher; IntPtr mouseHook,keyHook; bool disposed;
        public long Sequence { get; private set; }
        public event Action<long,int,Point> Acted;
        public bool Ready { get { return mouseHook!=IntPtr.Zero&&keyHook!=IntPtr.Zero; } }
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int kind,Hook callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        public InputActionObserver(Dispatcher ui) {
            dispatcher=ui; mouseCallback=Mouse; keyCallback=Keyboard; IntPtr module=GetModuleHandle(null);
            mouseHook=SetWindowsHookEx(14,mouseCallback,module,0); keyHook=SetWindowsHookEx(13,keyCallback,module,0);
        }
        void Notify(int kind,Point point) { long sequence=++Sequence; if(!disposed&&!dispatcher.HasShutdownStarted) dispatcher.BeginInvoke(new Action(delegate { if(!disposed&&Acted!=null) Acted(sequence,kind,point); })); }
        IntPtr Mouse(int code,IntPtr message,IntPtr data) {
            if(code>=0) { int m=message.ToInt32(); if(m==0x20A||m==0x20E) Notify(1,Native.Cursor()); else if(m==0x201||m==0x204||m==0x207||m==0x20B) Notify(2,Native.Cursor()); }
            return CallNextHookEx(mouseHook,code,message,data);
        }
        IntPtr Keyboard(int code,IntPtr message,IntPtr data) {
            if(code>=0&&(message.ToInt32()==0x100||message.ToInt32()==0x104)) { int key=Marshal.ReadInt32(data); if(key!=0x10&&key!=0x11&&key!=0x12&&key!=0x5B&&key!=0x5C&&(key<0xA0||key>0xA5)) Notify(1,Native.Cursor()); }
            return CallNextHookEx(keyHook,code,message,data);
        }
        public void Dispose() { if(disposed) return; disposed=true; if(mouseHook!=IntPtr.Zero) UnhookWindowsHookEx(mouseHook); if(keyHook!=IntPtr.Zero) UnhookWindowsHookEx(keyHook); mouseHook=keyHook=IntPtr.Zero; Acted=null; }
    }
}
