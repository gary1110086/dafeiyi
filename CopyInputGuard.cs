using System;
using System.Runtime.InteropServices;
using System.Threading;
namespace LightTranslate {
    // Observe action occurrence only; retain no keys or text. Ignore only this copy's marked injection.
    internal sealed class CopyInputGuard:IDisposable {
        internal readonly UIntPtr Tag=new UIntPtr(BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(),0));
        delegate IntPtr Hook(int code,IntPtr message,IntPtr data);
        readonly Hook mouseCallback,keyCallback; IntPtr mouseHook,keyHook; long changes;
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int kind,Hook callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        internal bool Ready { get { return mouseHook!=IntPtr.Zero&&keyHook!=IntPtr.Zero; } }
        internal bool Unchanged { get { return Interlocked.Read(ref changes)==0; } }
        internal CopyInputGuard() { mouseCallback=Mouse; keyCallback=Keyboard; var module=GetModuleHandle(null); mouseHook=SetWindowsHookEx(14,mouseCallback,module,0); keyHook=SetWindowsHookEx(13,keyCallback,module,0); }
        IntPtr Mouse(int code,IntPtr message,IntPtr data) { if(code>=0) Interlocked.Increment(ref changes); return CallNextHookEx(mouseHook,code,message,data); }
        IntPtr Keyboard(int code,IntPtr message,IntPtr data) {
            if(code>=0) { uint flags=unchecked((uint)Marshal.ReadInt32(data,8)); ulong tag=unchecked((ulong)Marshal.ReadInt64(data,16)); if((flags&0x10)==0||tag!=Tag.ToUInt64()) Interlocked.Increment(ref changes); }
            return CallNextHookEx(keyHook,code,message,data);
        }
        public void Dispose() { if(mouseHook!=IntPtr.Zero) UnhookWindowsHookEx(mouseHook); if(keyHook!=IntPtr.Zero) UnhookWindowsHookEx(keyHook); mouseHook=keyHook=IntPtr.Zero; }
    }
}
