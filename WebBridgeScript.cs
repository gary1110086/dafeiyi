namespace LightTranslate {
    internal static class WebBridgeScript {
        // Observe only the completion initiated by this app. No cookies, headers, passwords or tokens are inspected.
        internal const string Capture=@"(()=>{
if(location.origin!=='https://chat.deepseek.com'||window.__dafeiyiBridgeInstalled)return;
window.__dafeiyiBridgeInstalled=true;
window.__dafeiyiCapture={armed:false,started:false,body:'',done:false,status:0};
const match=url=>{try{const u=new URL(url,location.href);return u.origin===location.origin&&/\/chat\/(completion|continue)$/.test(u.pathname);}catch{return false;}};
const claim=url=>{const s=window.__dafeiyiCapture;if(!s||!s.armed||!match(url))return null;s.armed=false;s.started=true;return s;};
const fetch=window.fetch;window.fetch=async function(...args){const s=claim(typeof args[0]==='string'?args[0]:args[0]?.url);try{const r=await fetch.apply(this,args);if(s){s.status=r.status;const reader=r.clone().body.getReader(),decoder=new TextDecoder();s.cancel=()=>reader.cancel();(async()=>{try{while(true){const x=await reader.read();if(x.done)break;s.body+=decoder.decode(x.value,{stream:true});if(s.body.length>1000000)throw Error('oversize');}s.body+=decoder.decode();}catch{s.error='read';}finally{s.done=true;}})();}return r;}catch(e){if(s){s.error='network';s.done=true;}throw e;}};
const open=XMLHttpRequest.prototype.open,send=XMLHttpRequest.prototype.send;
XMLHttpRequest.prototype.open=function(method,url,...args){this.__dafeiyiUrl=url;return open.call(this,method,url,...args);};
XMLHttpRequest.prototype.send=function(...args){const s=claim(this.__dafeiyiUrl);if(s){s.cancel=()=>this.abort();const update=()=>{try{s.body=this.responseText;s.status=this.status;if(s.body.length>1000000){s.error='oversize';this.abort();}}catch{}};this.addEventListener('progress',update);this.addEventListener('error',()=>s.error='network');this.addEventListener('loadend',()=>{update();s.done=true;});}return send.apply(this,args);};
})();";
        internal const string State="(()=>{const s=window.__dafeiyiCapture;return s?{id:s.id,armed:s.armed,started:s.started,body:s.body,done:s.done,status:s.status,error:s.error||''}:null;})()";
        internal const string Stop=@"(()=>{const s=window.__dafeiyiCapture;if(!s)return;s.armed=false;if(s.started&&!s.done){const b=document.querySelector('.ds-button--primary.ds-button--circle:not(.ds-button--disabled)');if(b&&!b.innerHTML.includes('V15.0417'))b.click();if(s.cancel)s.cancel();}s.done=true;})();";
        internal const string Editor="!!document.querySelector('textarea[placeholder*=\"DeepSeek\"],textarea[placeholder*=\"发送消息\"]')&&!/sign_in/.test(location.pathname)";
        internal const string Send=@"(()=>{const b=document.querySelector('.ds-button--primary.ds-button--circle:not(.ds-button--disabled)');if(!b||b.getAttribute('aria-disabled')==='true')return false;b.click();return true;})()";
        internal static string Configure(bool thinking) { return @"(()=>{const toggles=[...document.querySelectorAll('.ds-toggle-button')];const t=toggles.find(e=>/深度思考|DeepThink/.test(e.innerText));if(!t)return false;const chosen=t.getAttribute('aria-pressed')==='true'||t.classList.contains('ds-toggle-button--selected');if(chosen!=="+(thinking?"true":"false")+@")t.click();const search=toggles.find(e=>/智能搜索|Search/.test(e.innerText));if(search&&(search.getAttribute('aria-pressed')==='true'||search.classList.contains('ds-toggle-button--selected')))search.click();return true;})()"; }
    }
}
