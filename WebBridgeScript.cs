namespace LightTranslate {
    internal static class WebBridgeScript {
        // Observe only the completion initiated by this app. No cookies, headers, passwords or tokens are inspected.
        internal const string Capture=@"(()=>{
if(location.origin!=='https://chat.deepseek.com'||window.__dafeiyiBridgeInstalled)return;
window.__dafeiyiBridgeInstalled=true;
window.__dafeiyiCapture={armed:false,started:false,body:'',done:false,status:0};
const match=url=>{try{const u=new URL(url,location.href);return u.origin===location.origin&&/\/chat\/(completion|continue)$/.test(u.pathname);}catch{return false;}};
const claim=url=>{const s=window.__dafeiyiCapture;if(!s||!s.armed||!match(url))return null;s.armed=false;s.started=true;return s;};
const fetch=window.fetch;window.fetch=async function(...args){const s=claim(typeof args[0]==='string'?args[0]:args[0]?.url);try{if(s){const abort=new AbortController();const signal=args[1]?.signal||(args[0] instanceof Request?args[0].signal:null);if(signal){if(signal.aborted)abort.abort();else signal.addEventListener('abort',()=>abort.abort(),{once:true});}args[1]={...(args[1]||{}),signal:abort.signal};s.cancel=()=>abort.abort();}const r=await fetch.apply(this,args);if(s){s.status=r.status;const reader=r.clone().body.getReader(),decoder=new TextDecoder();const cancel=s.cancel;s.cancel=()=>{cancel();reader.cancel().catch(()=>{});};(async()=>{try{while(true){const x=await reader.read();if(x.done)break;s.body+=decoder.decode(x.value,{stream:true});if(s.body.length>1000000)throw Error('oversize');}s.body+=decoder.decode();}catch{s.error='read';}finally{s.done=true;}})();}return r;}catch(e){if(s){s.error='network';s.done=true;}throw e;}};
const open=XMLHttpRequest.prototype.open,send=XMLHttpRequest.prototype.send;
XMLHttpRequest.prototype.open=function(method,url,...args){this.__dafeiyiUrl=url;return open.call(this,method,url,...args);};
XMLHttpRequest.prototype.send=function(...args){const s=claim(this.__dafeiyiUrl);if(s){s.cancel=()=>this.abort();const update=()=>{try{s.body=this.responseText;s.status=this.status;if(s.body.length>1000000){s.error='oversize';this.abort();}}catch{}};this.addEventListener('progress',update);this.addEventListener('error',()=>s.error='network');this.addEventListener('loadend',()=>{update();s.done=true;});}return send.apply(this,args);};
})();";
        internal const string State="(()=>{const s=window.__dafeiyiCapture;return s?{id:s.id,armed:s.armed,started:s.started,body:s.body,done:s.done,status:s.status,error:s.error||''}:null;})()";
        // Identify the visible composer by semantics. CSS classes are fallbacks, not requirements.
        internal const string Composer=@"(()=>{
const visible=e=>!!e&&e.getClientRects().length>0;
const enabled=e=>visible(e)&&!e.disabled&&e.getAttribute('aria-disabled')!=='true'&&!e.classList.contains('ds-button--disabled');
const editor=()=>[...document.querySelectorAll('textarea,[contenteditable=true][role=textbox],[contenteditable=true]')].find(e=>visible(e)&&!e.disabled&&!e.readOnly);
const scope=()=>{const e=editor();if(!e)return null;for(let n=e.parentElement,i=0;n&&i<9;n=n.parentElement,i++){if(n===document.body)break;if(n.querySelector('button,[role=button],.ds-button,.ds-icon-button,.ds-toggle-button,input[type=file]'))return n;}return e.parentElement;};
const controls=()=>{const p=scope();return p?[...p.querySelectorAll('button,[role=button],.ds-button,.ds-icon-button,.ds-toggle-button')].filter(visible):[];};
const label=e=>(e.getAttribute('aria-label')||'')+' '+(e.getAttribute('title')||'')+' '+(e.innerText||'');
const selected=e=>e.getAttribute('aria-pressed')==='true'||e.getAttribute('aria-checked')==='true'||/--selected|\bselected\b|\bactive\b/.test(e.className||'');
const thinking=()=>controls().find(e=>/深度思考|深度思索|思考|Deep\s*Think|\bThink(?:ing)?\b|\bReason(?:ing)?\b/i.test(label(e)));
const search=()=>controls().find(e=>/智能搜索|联网搜索|搜索|\bSearch\b/i.test(label(e)));
const send=()=>{const all=controls();const semantic=all.find(e=>/发送|提交|\bSend\b|\bSubmit\b/i.test(label(e))||e.getAttribute('data-testid')==='send-button'||e.getAttribute('type')==='submit');if(semantic)return semantic;return all.find(e=>e.classList.contains('ds-button--primary')&&e.classList.contains('ds-button--circle'));};
const stop=()=>controls().find(e=>/停止|停止生成|\bStop\b|\bCancel\b/i.test(label(e))) ;
";
        internal static readonly string Stop=Composer+@"const s=window.__dafeiyiCapture;if(!s)return;s.armed=false;if(s.started&&!s.done){const b=stop();if(enabled(b))b.click();if(s.cancel)s.cancel();}s.done=true;})();";
        internal static readonly string Editor=Composer+"return !!editor()&&!/sign[_-]?in|login/.test(location.pathname);})()";
        internal static readonly string Send=Composer+"const b=send();if(!enabled(b))return false;b.click();return true;})()";
        internal static readonly string Probe=Composer+@"const e=editor(),t=thinking(),s=search(),p=scope();return {editor:!!e,occupied:(!!e&&!!(e.value||e.innerText||'').trim())||[...document.querySelectorAll('input[type=file]')].some(i=>i.files.length>0),login:/sign[_-]?in|login/.test(location.pathname),thinking:!!t,thinkingSelected:!!t&&selected(t),search:!!s,send:!!send(),upload:!!p&&!!p.querySelector('input[type=file]')};})()";
        internal static string Configure(bool deep) { return Composer+@"if(!editor())return false;const t=thinking();if(!t&&"+(deep?"true":"false")+@")return false;if(t&&selected(t)!=="+(deep?"true":"false")+@"){if(!enabled(t))return false;t.click();}const s=search();if(s&&selected(s)){if(!enabled(s))return false;s.click();}return true;})()"; }
        internal static string AttachImage(ImageRequest image) { return Composer+@"const p=scope();const input=(p&&p.querySelector('input[type=file]'))||document.querySelector('input[type=file]');if(!input)return 'missing';if(input.files&&input.files.length)return 'occupied';const raw=atob('"+System.Convert.ToBase64String(image.Png)+@"');const bytes=Uint8Array.from(raw,c=>c.charCodeAt(0));const file=new File([bytes],'dafeiyi-"+image.Hash.Substring(0,12)+@".png',{type:'image/png'});const transfer=new DataTransfer();transfer.items.add(file);input.files=transfer.files;input.dispatchEvent(new Event('change',{bubbles:true}));return 'attached';})()"; }
        internal static readonly string UploadPending=Composer+"const p=scope();return !!p&&!!p.querySelector('[role=progressbar],[aria-busy=true],.ds-loading');})()";
    }
}
