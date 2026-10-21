/* OpenBEP translation runtime.
   The dictionary (translations/canbim_strings.csv) is keyed by the exact English text.
   trText() finds the translation of one piece of English text:
   1. the exact sentence;
   2. the sentence with its numbers and dates replaced by {1}, {2}... ("Issues ({1})");
      numbers are copied as they are (section numbers and IFC 4.3 must keep their dot);
   3. a list whose every part is in the dictionary ("Walls, Concept" or "A · B · C");
   4. several sentences, each of them in the dictionary;
   5. "Label: rest" when both the label and the rest are in the dictionary.
   Text with no letters stays as is; a leading "· " or a trailing ":" is kept around the
   translation (French gets its no-break space before the colon; Japanese gets a full-width
   colon, the 、 list mark and no space between sentences).
   It returns null when nothing matches, so the page keeps the English.
   build_i18n.py copies the block between the I18NCORE markers into app/bep-builder.html. */
/*I18NCORE*/
const I18N_COLON=L=>L==='fr'?'\u00a0:':L==='ja'||L==='zh'?'：':':', I18N_CJK=L=>L==='ja'||L==='zh';
const I18N_NUM=/\bv\d+(?:\.\d+)+\b|\b\d{4}-\d{2}-\d{2}\b|(?<![\w.,])\d{1,3}(?:,\d{3})+(?![\w,])|(?<![\w.,])\d+(?:\.\d+)?(?![\w])/g, I18N_LEAD=/^([·:;,.–—-]\s*)(.+)$/s;
function i18nKey(k){const m=k.match(I18N_LEAD); if(m) k=m[2]; if(/[^:]:$/.test(k)) k=k.slice(0,-1).trim(); let i=0; return k.replace(I18N_NUM,()=>'{'+(++i)+'}')}
// part: k is one piece of a longer sentence, so a product or firm name (every long word capitalised) stays as typed.
const I18N_NAME=k=>{const w=k.split(/\s+/); return /^[A-Z0-9]/.test(k)&&!/[.!?]$/.test(k)&&w.every(x=>x.length<4||/^[A-Z0-9(]/.test(x))&&(w.filter(x=>/^[A-Z]/.test(x)).length>1||/^\S*[a-z]\S*[A-Z0-9]/.test(k))};
// A page can define I18N_KEEP(text) to keep text the user typed exactly as typed (never translated).
function trText(k,T,L,part){if(!k) return null; if(!/[A-Za-z]/.test(k)) return k; if(typeof I18N_KEEP==='function'&&I18N_KEEP(k)) return k; if(T[k]) return T[k];
  let m=k.match(I18N_LEAD); if(m){const t=trText(m[2],T,L); return t&&m[1]+t}
  if(/[^:]:$/.test(k)){const t=trText(k.slice(0,-1).trim(),T,L); return t&&t+I18N_COLON(L)}
  const nums=k.match(I18N_NUM); if(nums){const t=T[i18nKey(k)]; if(t) return t.replace(/\{(\d+)\}/g,(x,i)=>nums[i-1]==null?x:nums[i-1])}
  for(const sep of [' · ','; ',', ']) if(k.includes(sep)){const out=k.split(sep).map(p=>trText(p.trim(),T,L,1)); if(out.every(Boolean)) return out.join(I18N_CJK(L)&&sep!==' · '?'、':sep)}
  const ss=k.split(/(?<=[.!?]) (?=[A-Z])/); if(ss.length>1){const out=ss.map(p=>trText(p,T,L)); if(out.every(Boolean)) return out.join(I18N_CJK(L)?'':' ')}
  for(let c=k.indexOf(': ');c>0;c=k.indexOf(': ',c+1)){const h=k.slice(0,c), th=T[h+':']||((x=>x&&x+I18N_COLON(L))(trText(h,T,L)||(I18N_NAME(h)||/^[A-Z]{2,5}$/.test(h)||/^[\u3040-\u30ff\u4e00-\u9fff][\u3040-\u30ff\u4e00-\u9fff ・]*$/.test(h)?h:null))), tr=th&&trText(k.slice(c+2).trim(),T,L,1); if(tr) return th+(I18N_CJK(L)&&/：$/.test(th)?'':' ')+tr}
  if(/[^.]\.$/.test(k)){const t=trText(k.slice(0,-1),T,L,part); if(t) return /[.。！？]$/.test(t)?t:t+(I18N_CJK(L)?'。':'.')}
  if(/^[a-z][a-z]/.test(k)){const t=trText(k[0].toUpperCase()+k.slice(1),T,L,part); if(t) return I18N_CJK(L)||!/^.[a-zà-ÿ]/.test(t)?t:t[0].toLowerCase()+t.slice(1)}
  if(part&&(I18N_NAME(k)||/^(?=.*\d)[A-Z0-9-]{4,}$/.test(k)||/[\u3040-\u30ff\u4e00-\u9fff]/.test(k)&&!/[A-Za-z]{3,} [a-z]{2,}/.test(k))) return k;
  // A value typed by the user in brackets at the end (a product name, or text in the reader's own script) stays as typed.
  m=(()=>{const e=k.endsWith(').')?k.length-2:k.endsWith(')')?k.length-1:-1; if(e<0) return null; let d=0; for(let i=e;i>0;i--){if(k[i]===')') d++; else if(k[i]==='('&&!--d) return k[i-1]===' '?[k,k.slice(0,i-1),k.slice(i+1,e),k.slice(e+1)]:null} return null})();
  if(m&&(I18N_NAME(m[2])||!/[A-Za-z]{3}/.test(m[2])||typeof I18N_KEEP==='function'&&I18N_KEEP(m[2]))){const t=trText(m[1]+m[3],T,L,part); if(t) return I18N_CJK(L)?t.replace(/。$/,'')+'（'+m[2]+'）'+(m[3]?'。':''):t.replace(/\.$/,'')+' ('+m[2]+')'+m[3]}
  return null}
/*/I18NCORE*/

/* Page runtime, for every site page except the BEP Builder (which has its own).
   Load translations/strings.js first, then this file. Pick the language with
   <select data-i18n-lang></select> anywhere on the page, or ?lang=fr in the address.
   Mark anything that must stay as typed (names, codes) with data-notr. */
(function(){
  if(typeof document==='undefined') return;
  const TABLE=window.OPENBEP_STRINGS||{}, LANGS=window.OPENBEP_LANGS||{en:'English'}, KEY='openbep-lang';
  const ATTRS=['placeholder','aria-label','title','alt'], ORIG=new WeakMap(), SEEN=new Set();
  let L=new URLSearchParams(location.search).get('lang');
  try{L=L||localStorage.getItem(KEY)}catch(e){}
  if(!LANGS[L]){const b=(navigator.language||'').slice(0,2); L=LANGS[b]?b:'en'}
  const ok=n=>{const p=n.parentNode; return p&&!/^(SCRIPT|STYLE|TEXTAREA)$/.test(p.nodeName)&&!(p.closest&&p.closest('[data-notr]'))};
  function run(root){const T=TABLE[L]||{};
    const w=document.createTreeWalker(root,NodeFilter.SHOW_TEXT,{acceptNode:n=>ok(n)?NodeFilter.FILTER_ACCEPT:NodeFilter.FILTER_REJECT});
    let n; while((n=w.nextNode())){if(!ORIG.has(n)) ORIG.set(n,n.nodeValue); const o=ORIG.get(n), k=o.trim(); if(!k||!/[A-Za-z]/.test(k)) continue;
      SEEN.add(k); const x=L!=='en'&&trText(k,T,L), nv=x?o.replace(k,x):o; if(n.nodeValue!==nv) n.nodeValue=nv}
    if(root.querySelectorAll) [root,...root.querySelectorAll(ATTRS.map(a=>'['+a+']').join(','))].forEach(el=>{if(!el.getAttribute||el.closest('[data-notr]')) return;
      ATTRS.forEach(a=>{const dk='tr'+a.replace('-',''); let o=el.dataset[dk]; if(o==null){o=el.getAttribute(a); if(o==null) return; el.dataset[dk]=o}
        const k=o.trim(); if(!k) return; SEEN.add(k); el.setAttribute(a,(L!=='en'&&trText(k,T,L))||o)})})}
  let busy=false;
  function all(){busy=true; document.documentElement.lang=L; if(document.title){const t=document.querySelector('title'); if(t) run(t)} run(document.body);
    document.querySelectorAll('select[data-i18n-lang]').forEach(s=>{if(!s.options.length) s.innerHTML=Object.entries(LANGS).map(([k,v])=>`<option value="${k}">${v}</option>`).join(''); s.value=L}); busy=false}
  window.openbepSetLang=l=>{if(!LANGS[l]) return; L=l; try{localStorage.setItem(KEY,l)}catch(e){} all()};
  window.openbepStrings=()=>[...SEEN];
  document.addEventListener('change',e=>{if(e.target.matches&&e.target.matches('select[data-i18n-lang]')) window.openbepSetLang(e.target.value)});
  const start=()=>{all(); new MutationObserver(ms=>{if(busy) return; busy=true; ms.forEach(m=>m.addedNodes.forEach(n=>{if(n.nodeType===1) run(n); else if(n.nodeType===3&&n.parentNode) run(n.parentNode)})); busy=false}).observe(document.body,{childList:true,subtree:true})};
  if(document.readyState==='loading') document.addEventListener('DOMContentLoaded',start); else start();
})();
