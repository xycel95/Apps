// W3C Execute Script on the explicitly selected Outlook origin. No network calls or session storage.
const [origin, operation, arg] = arguments;
const fail = code => ({error:code});
if (location.origin !== origin) return fail("origen");
const all = (root, selector) => Array.from(root.querySelectorAll(selector));
const clean = value => (value || "").replace(/[\u200e\u200f\u202a-\u202e]/g,"").replace(/\s+/g," ").trim();
const lower = value => clean(value).toLocaleLowerCase();
const display = e => !!e && e.getClientRects().length > 0 && getComputedStyle(e).visibility !== "hidden";
const label = e => clean(e.getAttribute("aria-label") || e.getAttribute("title") || e.innerText);
const lines = e => (e.innerText || "").split(/\r?\n/).map(clean).filter(Boolean);
const bodySelector = '[role="document"][aria-label]';
const editorSelector = '[contenteditable="true"],[role="textbox"]';
const withinContent = e => !!e.closest(bodySelector + "," + editorSelector);
const buttons = root => all(root,'button,[role="button"]').filter(e=>!withinContent(e) && display(e) && !e.disabled && e.getAttribute("aria-disabled")!=="true");
const named = (items,pattern) => items.find(e=>pattern.test(label(e))) || null;
const heading2 = 'h2,[role="heading"][aria-level="2"]';
const heading3 = 'h3,[role="heading"][aria-level="3"]';
const isBody = e => /^(cuerpo del mensaje|message body)$/i.test(clean(e.getAttribute("aria-label")));
const bodies = all(document,bodySelector).filter(e=>isBody(e) && display(e) && !withinContent(e.parentElement));
const dialogs = all(document,'[role="dialog"]').filter(e=>!withinContent(e) && display(e));
const preview = dialogs.findLast(d=>bodies.some(b=>d.contains(b)));
const listName = name => /^(?:archivos? adjuntos?|datos? adjuntos?|attachments?)$/i.test(clean(name));
const lists = root => all(root,'[role="listbox"],[role="list"]').filter(l=>!withinContent(l) && display(l) && listName(label(l)));
const options = l => all(l,'[role="option"],[role="listitem"]').filter(c=>c.closest('[role="listbox"],[role="list"]')===l);
const mailCard = c => /(?:elemento de outlook|outlook item)/i.test([label(c),...lines(c)].join(" "));
const menus = c => named(buttons(c),/^(más acciones|more actions|mostrar más acciones|show more actions)$/i);
const cardKey = c => clean(c.getAttribute("aria-label") || lines(c).join(" "));
const attachmentName = c => {
    const line = lines(c).find(v=>!/^(más acciones|more actions|elemento de outlook|outlook item)$/i.test(v));
    return line || clean(c.getAttribute("title"));
};
const editor = all(document,editorSelector).some(e=>display(e) && isBody(e));
function selectionCount() {
    const re=/^(?:([\d.,\u00a0 ]+)\s+(?:(?:conversaciones?|mensajes?|elementos?)\s+seleccionad[oa]s?|(?:conversations?|items?|messages?)\s+selected)|(?:seleccionad[oa]s?|selected)\s*[:·]?\s*([\d.,\u00a0 ]+))\s*$/i;
    for (const e of all(document,"span,div,p,button,[role=status]")) {
        if (e.childElementCount>1 || withinContent(e)) continue;
        const text=e.textContent;
        if (!text || text.length>85) continue;
        const match=clean(text).match(re);
        if (match && display(e)) return Number((match[1]||match[2]).replace(/\D/g,""));
    }
    return 0;
}
function draft() {
    const eligible=lists(document).filter(l=>{const cards=options(l);return cards.length && cards.every(mailCard);});
    const root=eligible.length===1?eligible[0]:null;
    return {root,cards:root?options(root):[]};
}
function view() {
    const scope=preview || document;
    const candidates=bodies.filter(e=>scope.contains(e));
    if (candidates.length!==1) return null;
    const body=candidates[0];
    for (let p=body.parentElement;p && p!==document.body;p=p.parentElement) {
        const subjects=all(p,heading2).filter(e=>!withinContent(e) && display(e));
        if (subjects.length===1 && (preview || all(p,heading3).some(e=>!withinContent(e)))) {
            const title=clean(subjects[0].getAttribute("title") || subjects[0].innerText);
            const headerNodes=all(p,heading3).filter(e=>!withinContent(e) && display(e));
            const headers=headerNodes.map(e=>clean(e.innerText || label(e))).filter(Boolean);
            if (!title && !headers.length) return null;
            return {area:p,body,title:title || "(Sin asunto)",headers};
        }
        if(p===preview) break;
    }
    return null;
}
function attachmentCards(v) {
    const result=[];
    for(const l of lists(v.area)) {
        const items=options(l);
        const size=Math.max(Number(l.getAttribute("aria-setsize")) || 0,...items.map(c=>Number(c.getAttribute("aria-setsize"))||0));
        if(!items.length || size>items.length) return null;
        result.push(...items);
    }
    return result;
}
function contents(v) {
    const cards=attachmentCards(v);
    if(!cards) return fail("lista-adjuntos");
    const images=all(v.body,"img").filter(display);
    if(images.some(i=>!i.complete || !i.naturalWidth || !i.naturalHeight)) return fail("imagen");
    if(all(v.body,"iframe,embed,object").some(display)) return fail("cuerpo-incrustado");
    const denied=buttons(v.area).some(e=>/^(?:no reenviar|do not forward)$/i.test(label(e))) ||
        all(v.area,'[role="alert"],[role="status"]').some(e=>!withinContent(e) && display(e) && /(?:no (?:se )?pued[eas]* (?:imprimir|descargar)|printing is (?:disabled|not allowed)|cannot print|rights.protected)/i.test(clean(e.innerText)));
    if(denied) return fail("protegido");
    const files=cards.map(c=>({name:attachmentName(c),key:cardKey(c),mail:mailCard(c)}));
    if(files.some(f=>!f.name)) return fail("nombre-adjunto");
    return {title:v.title,headers:v.headers,body:v.body.innerText || "",files,
        images:new Set(images.map(i=>i.currentSrc || i.src).filter(s=>!/^data:image\/svg\+xml/i.test(s) && !/\.svg(?:$|[?#])/i.test(s))).size};
}
function clearPrint(token) {
    if(!/^[a-f0-9]{32}$/.test(token)) return;
    document.getElementById("cd-style-"+token)?.remove();
    document.getElementById("cd-head-"+token)?.remove();
    for(const attr of ["data-cd-path","data-cd-part"])
        for(const el of all(document,"["+attr+'="'+token+'"]')) el.removeAttribute(attr);
}
if(operation==="clear") { clearPrint(arg.token); return true; }
if(operation==="state") {
    if(preview) return {kind:"preview",count:0,keys:[]};
    const list=draft();
    if(editor && list.root) return {kind:"draft",count:list.cards.length,keys:list.cards.map(cardKey)};
    if(editor) return {kind:"compose",count:0,keys:[]};
    const count=selectionCount();
    if(count>1) return {kind:"selection",count,keys:[]};
    return {kind:view()?"single":"empty",count,keys:[]};
}
if(operation==="forward") {
    if(editor || preview || selectionCount()!==arg.count) return fail("cambio-seleccion");
    const matches=buttons(document).filter(e=>/^(?:reenviar|forward)(?:\s*\([^)]*\))?$/i.test(label(e)) && !e.closest('[role=option],[role=listitem]'));
    return matches.length===1?matches[0]:fail("reenviar");
}
if(operation==="card") {
    const list=draft();
    if(!editor || preview || list.cards.length!==arg.count || !list.cards[arg.index] || cardKey(list.cards[arg.index])!==arg.key) return fail("cambio-lote");
    return menus(list.cards[arg.index]) || fail("acciones-correo");
}
if(operation==="menu") {
    const allowed=arg.type==="preview"?/^(vista previa|preview)$/i:/^(descargar|download)$/i;
    const items=all(document,'[role="menuitem"]').filter(e=>!withinContent(e) && display(e) && e.getAttribute("aria-disabled")!=="true" && !e.disabled && allowed.test(label(e)));
    return items.length===1?items[0]:null;
}
if(operation==="close") return preview?named(buttons(preview),/^(cerrar|close)$/i):null;
const v=view();
if(!v || (arg.dialog && !preview)) return null;
if(operation==="info") return contents(v);
if(operation==="attachment") {
    const cards=attachmentCards(v);
    const c=cards?.[arg.index];
    if(!c || cardKey(c)!==arg.key || mailCard(c)) return fail("cambio-adjunto");
    return menus(c) || fail("acciones-adjunto");
}
if(operation==="print") {
    const info=contents(v);
    if(info.error) return info;
    const token=arg.token;
    if(!/^[a-f0-9]{32}$/.test(token)) return fail("impresion");
    clearPrint(token);
    const head=document.createElement("section");
    head.id="cd-head-"+token; head.setAttribute("data-cd-part",token);
    head.style.display="none";
    for(const [i,t] of [info.title,...info.headers].entries()) {
        const el=document.createElement(i===0?"h1":"p"); el.textContent=t;head.append(el);
    }
    document.body.prepend(head);
    v.body.setAttribute("data-cd-part",token);
    for(let p=v.body.parentElement;p;p=p.parentElement) p.setAttribute("data-cd-path",token);
    const path='[data-cd-path="'+token+'"]', part='[data-cd-part="'+token+'"]';
    const style=document.createElement("style");
    style.id="cd-style-"+token;
    style.textContent='@media print { '+path+' {display:block!important;position:static!important;inset:auto!important;float:none!important;transform:none!important;contain:none!important;clip:auto!important;clip-path:none!important;overflow:visible!important;width:auto!important;height:auto!important;min-height:0!important;max-height:none!important;margin:0!important;padding:0!important;border:0!important;background:white!important;} '+path+' > :not('+path+'):not('+part+'){display:none!important;} '+part+'{display:block!important;position:static!important;overflow:visible!important;height:auto!important;max-height:none!important;visibility:visible!important;} #'+head.id+'{font:11pt Arial,sans-serif!important;color:#111!important;margin-bottom:12pt!important;} #'+head.id+' h1{font: bold 17pt Arial,sans-serif!important;margin:0 0 8pt!important;} #'+head.id+' p{margin:2pt 0!important;} '+part+' img{max-width:100%!important;} '+part+' table{max-width:100%!important;} }';
    document.head.append(style);
    return info;
}
return fail("operacion");
