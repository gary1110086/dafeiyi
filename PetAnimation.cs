using System;
using System.Collections.Generic;
namespace LightTranslate {
    public class PetAnimation {
        readonly Dictionary<string,PetClip> clips;
        string basis="idle",overlay; int elapsed,remaining;
        bool reduced;
        public string ActiveClip { get { return overlay??basis; } }
        public string BaseClip { get { return basis; } }
        public bool HasOverlay { get { return overlay!=null; } }
        public int Index { get; private set; }
        public bool ReducedMotion { get { return reduced; } set { reduced=value; if(value) Index=0; } }
        public PetAnimation():this(PetAssets.Shared.Clips) { }
        internal PetAnimation(Dictionary<string,PetClip> available) { clips=available; }
        public void SetBase(string clip) {
            if(!clips.ContainsKey(clip)) clip="idle";
            if(basis==clip) return; basis=clip; if(overlay==null) Reset();
        }
        public void Play(string clip,int holdMs) {
            PetClip action; if(!clips.TryGetValue(clip,out action)) return;
            overlay=clip; remaining=holdMs>0?holdMs:action.frameMs*Math.Max(1,action.frames.Count); Reset();
        }
        public void ClearOverlay() { if(overlay==null) return; overlay=null; remaining=0; Reset(); }
        void Reset() { elapsed=0; Index=0; }
        public void Tick(int deltaMs) {
            if(deltaMs<0) return;
            if(overlay!=null && remaining!=int.MaxValue) { remaining-=deltaMs; if(remaining<=0) { ClearOverlay(); return; } }
            PetClip clip; if(reduced || !clips.TryGetValue(ActiveClip,out clip)||clip.frames.Count==0) { Index=0; return; }
            int count=clip.frames.Count; elapsed=(int)Math.Min(int.MaxValue,(long)elapsed+deltaMs);
            int index=elapsed/Math.Max(1,clip.frameMs);
            Index=clip.loop?index%count:Math.Min(count-1,index);
            if(clip.loop && elapsed>clip.frameMs*count) elapsed%=clip.frameMs*count;
        }
    }
}
