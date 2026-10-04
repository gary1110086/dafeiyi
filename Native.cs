using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace LightTranslate {
    internal static class Native {
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X,Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left,Top,Right,Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct MONITORINFO { public int Size; public RECT Monitor,Work; public uint Flags; }
        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint Type; public INPUTUNION Data; }
        [StructLayout(LayoutKind.Explicit)] struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT Keyboard; [FieldOffset(0)] public MOUSEINPUT Mouse; }
        [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort VirtualKey,Scan; public uint Flags,Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int X,Y; public uint MouseData,Flags,Time; public UIntPtr Extra; }
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hwnd,int id,uint mods,uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd,int id);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT point,uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFO info);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd,int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd,int index,int value);
        [DllImport("user32.dll")] static extern uint SendInput(uint count,INPUT[] inputs,int size);
        [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] internal static extern IntPtr GetClipboardOwner();
        [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint Size,Time; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
        static uint InputStamp() { var info=new LASTINPUTINFO { Size=(uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) }; return GetLastInputInfo(ref info)?info.Time:0; }
        internal static uint InputActivityStamp() { return InputStamp(); }
        internal static bool ModifiersReleased() { return ClipboardCapture.ModifiersClear(Down(0x11),Down(0x12),Down(0x10),Down(0x5B)||Down(0x5C)); }
        public static Point Cursor() { POINT p; GetCursorPos(out p); return new Point(p.X,p.Y); }
        public static IntPtr RootAt(Point p) { return GetAncestor(WindowFromPoint(new POINT { X=(int)p.X,Y=(int)p.Y }),2); }
        public static bool IsOwnWindow(IntPtr hwnd) { uint pid; GetWindowThreadProcessId(hwnd,out pid); return pid==(uint)Process.GetCurrentProcess().Id; }
        public static bool Down(int key) { return (GetAsyncKeyState(key)&0x8000)!=0; }
        public static string ProcessName(IntPtr hwnd) { uint pid; GetWindowThreadProcessId(hwnd,out pid); try { using(var process=Process.GetProcessById((int)pid)) return process.ProcessName; } catch { return ""; } }
        static bool SameExecutable(uint first,uint second) {
            if(first==0||second==0) return false; if(first==second) return true;
            try { using(var a=Process.GetProcessById((int)first)) using(var b=Process.GetProcessById((int)second)) return string.Equals(a.MainModule.FileName,b.MainModule.FileName,StringComparison.OrdinalIgnoreCase); } catch { return false; }
        }
        internal static Rect Bounds(Window window) { RECT r; GetWindowRect(new WindowInteropHelper(window).Handle,out r); return new Rect(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top); }
        internal static void MoveWithoutActivation(Window window,double x,double y) { SetWindowPos(new WindowInteropHelper(window).Handle,IntPtr.Zero,(int)x,(int)y,0,0,0x0001|0x0004|0x0010); }
        internal static void MakeNonActivating(Window window) { var hwnd=new WindowInteropHelper(window).Handle; SetWindowLong(hwnd,-20,GetWindowLong(hwnd,-20)|0x08000000); }
        internal static async Task TestClick(Point point) {
            SetCursorPos((int)point.X,(int)point.Y); await Task.Delay(80);
            var down=new INPUT { Type=0,Data=new INPUTUNION { Mouse=new MOUSEINPUT { Flags=2 } } }; SendInput(1,new[]{down},Marshal.SizeOf(typeof(INPUT))); await Task.Delay(100);
            var up=new INPUT { Type=0,Data=new INPUTUNION { Mouse=new MOUSEINPUT { Flags=4 } } }; SendInput(1,new[]{up},Marshal.SizeOf(typeof(INPUT)));
        }
        internal static void SetBounds(Window window,Rect bounds) { SetWindowPos(new WindowInteropHelper(window).Handle,new IntPtr(-1),(int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height,0x0040); }
        internal static void Clamp(Window window) {
            var hwnd=new WindowInteropHelper(window).Handle; RECT rect; if(hwnd==IntPtr.Zero || !GetWindowRect(hwnd,out rect)) return;
            var monitor=MonitorFromPoint(new POINT { X=(rect.Left+rect.Right)/2,Y=(rect.Top+rect.Bottom)/2 },2);
            var info=new MONITORINFO { Size=Marshal.SizeOf(typeof(MONITORINFO)) }; if(!GetMonitorInfo(monitor,ref info)) return;
            int width=Math.Min(rect.Right-rect.Left,info.Work.Right-info.Work.Left-24),height=Math.Min(rect.Bottom-rect.Top,info.Work.Bottom-info.Work.Top-24);
            int x=Math.Max(info.Work.Left+12,Math.Min(rect.Left,info.Work.Right-width-12)),y=Math.Max(info.Work.Top+12,Math.Min(rect.Top,info.Work.Bottom-height-12));
            if(x!=rect.Left||y!=rect.Top||width!=rect.Right-rect.Left||height!=rect.Bottom-rect.Top) SetWindowPos(hwnd,IntPtr.Zero,x,y,width,height,0x0004|0x0010);
        }
        public static void Place(Window w,Point p) {
            var monitor=MonitorFromPoint(new POINT { X=(int)p.X,Y=(int)p.Y },2);
            var info=new MONITORINFO { Size=Marshal.SizeOf(typeof(MONITORINFO)) }; GetMonitorInfo(monitor,ref info);
            uint dx=96,dy=96; try { GetDpiForMonitor(monitor,0,out dx,out dy); } catch(EntryPointNotFoundException) { } catch(DllNotFoundException) { }
            double scale=dx/96.0;
            int width=(int)Math.Min(w.Width*scale,info.Work.Right-info.Work.Left-24),height=(int)Math.Min(w.Height*scale,info.Work.Bottom-info.Work.Top-24);
            var position=Geometry.Place(p.X,p.Y,width,height,info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top);
            var hwnd=new WindowInteropHelper(w).Handle;
            SetWindowPos(hwnd,new IntPtr(-1),(int)position.X,(int)position.Y,width,height,0x10|0x40);
        }
        static uint CopyKeys(UIntPtr copyTag) {
            uint stamp=unchecked((uint)Environment.TickCount); if(stamp==0) stamp=1;
            var keys=new INPUT[4]; ushort[] codes={0x11,0x43,0x43,0x11};
            for(int i=0;i<4;i++) keys[i]=new INPUT { Type=1,Data=new INPUTUNION { Keyboard=new KEYBDINPUT { VirtualKey=codes[i],Flags=i>=2?2u:0u,Time=stamp,Extra=copyTag } } };
            if(SendInput(4,keys,Marshal.SizeOf(typeof(INPUT)))!=4) throw new InvalidOperationException("当前窗口拒绝复制，请手动 Ctrl+C 后按 Ctrl+Alt+V。");
            return stamp;
        }
        internal static async Task TestDrag(IntPtr hwnd,Point from,Point to) {
            if(hwnd!=IntPtr.Zero) SetForegroundWindow(hwnd); await Task.Delay(200); SetCursorPos((int)from.X,(int)from.Y);
            var down=new INPUT { Type=0,Data=new INPUTUNION { Mouse=new MOUSEINPUT { Flags=2 } } }; SendInput(1,new[]{down},Marshal.SizeOf(typeof(INPUT))); await Task.Delay(120);
            for(int i=1;i<=10;i++) { SetCursorPos((int)(from.X+(to.X-from.X)*i/10),(int)(from.Y+(to.Y-from.Y)*i/10)); await Task.Delay(30); }
            var up=new INPUT { Type=0,Data=new INPUTUNION { Mouse=new MOUSEINPUT { Flags=4 } } }; SendInput(1,new[]{up},Marshal.SizeOf(typeof(INPUT)));
        }
        internal static void TestHotkeyD() {
            ushort[] codes={0x11,0x12,0x44,0x44,0x12,0x11}; var keys=new INPUT[codes.Length];
            for(int i=0;i<keys.Length;i++) keys[i]=new INPUT { Type=1,Data=new INPUTUNION { Keyboard=new KEYBDINPUT { VirtualKey=codes[i],Flags=i>=3?2u:0u } } };
            SendInput((uint)keys.Length,keys,Marshal.SizeOf(typeof(INPUT)));
        }
        internal static void TestHotkeyS() {
            var keys=new INPUT[6]; ushort[] codes={0x11,0x12,0x53,0x53,0x12,0x11};
            for(int i=0;i<keys.Length;i++) keys[i]=new INPUT { Type=1,Data=new INPUTUNION { Keyboard=new KEYBDINPUT { VirtualKey=codes[i],Flags=i>=3?2u:0u } } };
            SendInput((uint)keys.Length,keys,Marshal.SizeOf(typeof(INPUT)));
        }
        internal static void TestRestoreCursor(Point p) { SetCursorPos((int)p.X,(int)p.Y); }
        internal static void TestKey(ushort key) { var inputs=new[]{new INPUT { Type=1,Data=new INPUTUNION { Keyboard=new KEYBDINPUT { VirtualKey=key } } },new INPUT { Type=1,Data=new INPUTUNION { Keyboard=new KEYBDINPUT { VirtualKey=key,Flags=2 } } }}; SendInput(2,inputs,Marshal.SizeOf(typeof(INPUT))); }
        internal static void TestSelectAll() { var inputs=new INPUT[4]; ushort[] codes={0x11,0x41,0x41,0x11}; for(int i=0;i<4;i++) inputs[i]=new INPUT { Type=1,Data=new INPUTUNION { Keyboard=new KEYBDINPUT { VirtualKey=codes[i],Flags=i>=2?2u:0u } } }; SendInput(4,inputs,Marshal.SizeOf(typeof(INPUT))); }
        internal static void TestWheel() { var input=new INPUT { Type=0,Data=new INPUTUNION { Mouse=new MOUSEINPUT { Flags=0x0800,MouseData=120 } } }; if(SendInput(1,new[]{input},Marshal.SizeOf(typeof(INPUT)))!=1) throw new InvalidOperationException("Test wheel input rejected"); }
        internal static void TestFocus(IntPtr hwnd) { SetForegroundWindow(hwnd); }
        internal static string CopyDiagnostic="";
        public static async Task<string> TryCopySelection(IntPtr target) {
            CopyDiagnostic="";
            // Do not send keystrokes after focus changes, or while shortcut modifiers are held.
            for(int i=0;i<20 && !ModifiersReleased();i++) await Task.Delay(35);
            if(!ModifiersReleased()||GetForegroundWindow()!=target) { CopyDiagnostic="focus or modifiers"; return ""; }
            uint targetPid; GetWindowThreadProcessId(target,out targetPid);
            using(var inputGuard=new CopyInputGuard()) {
            if(!inputGuard.Ready) { CopyDiagnostic="copy input guard unavailable"; return ""; }
            uint before=GetClipboardSequenceNumber(); uint inputStamp=CopyKeys(inputGuard.Tag);
            string text="";
            for(int i=0;i<16;i++) {
                await Task.Delay(35);
                uint copied=GetClipboardSequenceNumber();
                uint ownerPid; GetWindowThreadProcessId(GetClipboardOwner(),out ownerPid);
                if(!ClipboardCapture.CanAccept(before,copied,ownerPid,targetPid,GetForegroundWindow()==target,inputGuard.Unchanged,SameExecutable(ownerPid,targetPid))) {
                    if(copied!=before || GetForegroundWindow()!=target || !inputGuard.Unchanged) { CopyDiagnostic="seq changed="+(copied!=before)+", owner="+ownerPid+", target="+targetPid+", no intervening input="+inputGuard.Unchanged+", requested stamp="+inputStamp+", observed stamp="+InputStamp()+", foreground="+(GetForegroundWindow()==target); return ""; }
                    continue;
                }
                uint acknowledgedStamp=InputStamp();
                try { if(Clipboard.ContainsText()) text=Clipboard.GetText(); } catch { return ""; }
                // No automatic clipboard rollback: attribution alone cannot make restoration atomic.
                // Refuse content if another change or user input arrives during the read.
                if(GetClipboardSequenceNumber()!=copied || InputStamp()!=acknowledgedStamp || !inputGuard.Unchanged || GetForegroundWindow()!=target) return "";
                break;
            }
            return text.Trim();
            }
        }
    }
}
