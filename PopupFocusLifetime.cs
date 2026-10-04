using System;
namespace LightTranslate {
    internal sealed class PopupFocusLifetime {
        IntPtr origin; bool visited;
        internal void Start(IntPtr foreground) { origin=foreground; visited=false; }
        internal bool ShouldHide(IntPtr foreground,bool related,bool pinned) {
            if(related) { visited=true; return false; }
            if(pinned||foreground==IntPtr.Zero) return false;
            // Passive display must not steal focus or immediately close while the source remains active.
            return visited||foreground!=origin;
        }
    }
}
