using System;
namespace LightTranslate {
    public class ChipLifetime {
        long created,lastNear,farSince=-1; bool approached;
        public void Start(long now) { created=lastNear=now; farSince=-1; approached=false; }
        public bool ShouldHide(long now,bool near,bool far,bool focusChanged,bool action) {
            if(focusChanged||action) return true;
            if(near) { lastNear=now; farSince=-1; approached=true; return false; }
            if(far) { if(farSince<0) farSince=now; if(now-farSince>=220) return true; } else farSince=-1;
            return approached?now-lastNear>=550:now-created>=2300;
        }
    }
}
