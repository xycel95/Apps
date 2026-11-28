// W3C Execute Script on the explicitly selected Outlook origin. No network calls or session storage.
const [origin, operation, arg, activeRow] = arguments;
const fail = code => ({error:code});
if (location.origin !== origin) return fail("origen");
const all = (root, selector) => Array.from(root.querySelectorAll(selector));
const clean = value => (value || "").replace(/[\u200e\u200f\u202a-\u202e]/g,"").replace(/\s+/g," ").trim();
const lower = value => clean(value).toLocaleLowerCase();
const display = e => !!e && e.getClientRects().length > 0 && getComputedStyle(e).visibility !== "hidden";
const label = e => clean(e.getAttribute("aria-label") || e.getAttribute("title") || e.innerText);
const lines = e => (e.innerText || "").split(/\r?\n/).map(clean).filter(Boolean);
const editorSelector = '[contenteditable="true"],[role="textbox"]';
const heading2 = 'h2,[role="heading"][aria-level="2"]';
const heading3 = 'h3,[role="heading"][aria-level="3"]';
const bodySelector = '[role="document"],[aria-label*="cuerpo" i],[aria-label*="message body" i],[role="region"][aria-labelledby],article[aria-labelledby],[id^="UniqueMessageBody" i],[data-app-section="MessageBody" i]';
function accessibleName(e) {
    const referenced=(e.getAttribute("aria-labelledby") || "").split(/\s+/).map(id=>document.getElementById(id)?.textContent || "").join(" ");
    return clean(e.getAttribute("aria-label") || referenced);
}
const isBody = e => /^(?:cuerpo (?:del |de este )?(?:mensaje|correo(?: electrónico)?)|(?:email |e-mail )?message body)(?:$|\s*[:,·–—-]\s*|\s+(?:de|from)\s)/i.test(accessibleName(e)) ||
    /^UniqueMessageBody(?:$|[_-]|\d)/i.test(e.id) || e.getAttribute("data-app-section")?.toLowerCase()==="messagebody";
const folderControls='nav,[role="navigation"],[role="tree"],[role="listbox"],[role="menu"],[role="tablist"]';
const mailRegion='[id="ReadingPaneContainerId" i],[aria-label*="panel de lectura" i],[aria-label*="reading pane" i],[data-app-section="ConversationContainer" i],[data-app-section="MailReadCompose" i]';
const possibleBodies=all(document,bodySelector+',.allowTextSelection').filter(e=>{
    if(!display(e) || e.closest(editorSelector) || e.closest(folderControls)) return false;
    if(isBody(e)) return true;
    if(!e.closest(mailRegion)) return false;
    return e.matches('[role="document"]') && !accessibleName(e) ||
        e.matches('.allowTextSelection') && !e.querySelector('[role="toolbar"],[aria-haspopup],button') && !!e.querySelector('p,table,[dir]');
});
const bodies=possibleBodies.filter(e=>!possibleBodies.some(parent=>parent!==e && parent.contains(e)));
const withinContent = e => !!e && (!!e.closest(editorSelector+',[role="document"],[id^="UniqueMessageBody" i]') || bodies.some(b=>b.contains(e)));
const buttons = root => all(root,'button,[role="button"]').filter(e=>!withinContent(e) && display(e) && !e.disabled && e.getAttribute("aria-disabled")!=="true");
const named = (items,pattern) => items.find(e=>pattern.test(label(e))) || null;
const dialogs = all(document,'[role="dialog"]').filter(e=>!withinContent(e) && display(e));
const preview = dialogs.findLast(d=>bodies.some(b=>d.contains(b)));
const listName = name => /^(?:archivos? adjuntos?|datos? adjuntos?|attachments?)$/i.test(clean(name));
const lists = root => all(root,'[role="listbox"],[role="list"]').filter(l=>!withinContent(l) && display(l) && listName(label(l)));
const options = l => all(l,'[role="option"],[role="listitem"]').filter(c=>c.closest('[role="listbox"],[role="list"]')===l);
const mailCard = c => /(?:elemento de outlook|outlook item)/i.test([label(c),...lines(c)].join(" "));
const fileMenu = /^(?:(?:mostrar |ver |show )?(?:más acciones|more actions|más opciones|more options)|(?:opciones (?:de|del) (?:archivo|adjunto)|attachment options))(?:$|\s+(?:para|de|for)\s|\s*[:·])/i;
const menus = c => named(buttons(c),fileMenu) || buttons(c).find(b=>b.getAttribute("aria-haspopup")==="menu") || null;
const cardKey = c => clean(c.getAttribute("aria-label") || lines(c).join(" "));
const attachmentName = c => {
    const names=[c,...all(c,'[title],[aria-label]')].filter(e=>!withinContent(e)).flatMap(e=>[e.getAttribute("title"),e.getAttribute("aria-label")]).map(clean);
    const complete=names.find(n=>/^[^\n<>]+\.[\p{L}\d]{1,12}$/u.test(n) && !fileMenu.test(n) && !/^(?:vista previa|preview|abrir |open |descargar |download )/i.test(n));
    return complete || lines(c).find(v=>!fileMenu.test(v) && !/^(?:elemento de outlook|outlook item)$/i.test(v)) || "";
};
const editor = all(document,editorSelector).some(e=>display(e) && isBody(e));
function selectionCount() {
    const re=/^(?:([\d.,\u00a0 ]+)\s+(?:(?:conversaciones?|mensajes?|elementos?)\s+seleccionad[oa]s?|(?:conversations?|items?|messages?)\s+selected)|(?:seleccionad[oa]s?|selected)\s*[:·]?\s*([\d.,\u00a0 ]+))\s*$/i;
    for (const e of all(document,"span,div,p,button,[role=status]")) {
        if (e.childElementCount>1 || withinContent(e) || e.closest('[role="tab"],[role="tablist"]')) continue;
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
const subjectKey=t=>lower(t).replace(/^(?:(?:re|fw|fwd|rv)\s*:\s*)+/i,'');
const titleOf=e=>clean(e.getAttribute("title") || e.innerText || accessibleName(e));
const subjectSelector='h1,h2,[role="heading"][aria-level="1"],[role="heading"][aria-level="2"],[data-app-section="MailSubject" i],[data-app-section="MessageSubject" i],[aria-label="Asunto" i],[aria-label="Subject" i]';
const headerSelector='[role="heading"][aria-level="3"],h3,address,time,[datetime],[data-app-section="Sender" i],[data-app-section="MessageHeader" i]';
const dateLine=t=>/\b(?:\d{1,4}[-/.]\d{1,2}(?:[-/.]\d{1,4})?|\d{1,2}:\d{2})\b/.test(t);
const recipient=t=>/^(?:para|to|cc)\s*:/i.test(t);
const replyAction=/^(?:responder(?: a todos)?|reenviar|reply(?: all)?|forward)(?:$|\s*\()/i;
function normalCard(body) {
    for(let p=body.parentElement;p && p!==document.body;p=p.parentElement) {
        if(all(p,'[data-correo-lista],nav,[role="tree"]').some(e=>!withinContent(e))) break;
        if(buttons(p).some(e=>replyAction.test(label(e)))) return p;
    }
    return null;
}
function visibleHeader(card,body) {
    // Only consider the region before the body, outside attachments and action bars.
    const before=e=>display(e) && !e.contains(body) && !withinContent(e) &&
        !!(e.compareDocumentPosition(body)&Node.DOCUMENT_POSITION_FOLLOWING) &&
        !e.closest('[role="toolbar"],[role="list"],[role="listbox"],[role="option"],[role="menu"],[role="menuitem"],[role="listitem"]');
    const values=[];
    for(const e of all(card,'span,div,button,a,'+headerSelector)) {
        if(!before(e) || e.childElementCount || e.closest(subjectSelector) || e.closest('[aria-haspopup="menu"]')) continue;
        const t=clean(e.innerText || accessibleName(e));
        if(!t || t.length>400 || replyAction.test(t) || fileMenu.test(t) || /^(?:resumir|summarize|reacciones|reactions|me gusta|like)(?:\s|$)/i.test(t)) continue;
        const control=e.closest('button,[role="button"]');
        if(control && !recipient(t) && !e.closest('[aria-haspopup="dialog"],[data-app-section="Sender" i]') &&
            !/(?:perfil|profile|contacto|contact card)/i.test(accessibleName(control))) continue;
        if(values.length===0 && /^[A-Z]{1,2}$/.test(t)) continue;
        if(!values.includes(t)) values.push(t);
        if(dateLine(t) && values.some(recipient)) break;
    }
    const rich=all(card,headerSelector).filter(before).filter(e=>!e.querySelector(headerSelector)).map(e=>clean(e.innerText || accessibleName(e))).filter(Boolean);
    const details=values.filter(t=>!sizeLine(t));
    const hasDate=values.some(dateLine);
    const hasSender=details.some(t=>!dateLine(t) && !recipient(t) && /\p{L}{2}/u.test(t));
    return hasDate && hasSender || values.some(recipient) && rich.length ? [...new Set([...details,...rich])] : rich;
}
function view() {
    const candidates=preview?bodies.filter(e=>preview.contains(e)):bodies;
    if(candidates.length!==1) return null;
    const body=candidates[0];
    let titlesFromRow=activeRow?.titles;
    if(!titlesFromRow && !preview) {
        const selected=all(document,'[role="option"][aria-selected="true"],[role="row"][aria-selected="true"],[role="treeitem"][aria-selected="true"]').filter(e=>display(e) && !withinContent(e) && !e.closest('[role="dialog"]'));
        if(selected.length===1) titlesFromRow=all(selected[0],'span,div').filter(e=>display(e) && !e.childElementCount).map(titleOf);
    }
    const wanted=(titlesFromRow || []).map(subjectKey);
    const card=preview?null:normalCard(body);
    for(let p=body.parentElement;p && p!==document.body;p=p.parentElement) {
        if(!preview && (all(p,'[data-correo-lista],nav,[role="tree"]').some(e=>!withinContent(e)) || p.matches('[data-correo-lista]'))) break;
        let titles=all(p,preview?heading2:subjectSelector).filter(e=>!withinContent(e) && display(e) && !e.closest('button,[role="button"],[role="toolbar"]'));
        if(titles.length>1 && wanted.length) titles=titles.filter(e=>wanted.includes(subjectKey(titleOf(e))));
        if(!titles.length && card && p!==card && p.contains(card) && wanted.length) {
            // Outlook can put a plain-text subject in a strip above the message card.
            titles=all(p,'span,div').filter(e=>!e.childElementCount && !card.contains(e) && display(e) &&
                !e.closest('button,[role="button"],[role="toolbar"],[role="listbox"]') && wanted.includes(subjectKey(titleOf(e))));
        }
        if(titles.length===1) {
            const title=titleOf(titles[0]);
            const headerNodes=all(p,heading3).filter(e=>!withinContent(e) && display(e) && !e.contains(body));
            const headers=headerNodes.map(e=>clean(e.innerText || label(e))).filter(Boolean);
            if(!headers.length && card && p.contains(card)) headers.push(...visibleHeader(card,body));
            if((preview || headers.length) && (title || headers.length)) return {area:p,body,title:title || "(Sin asunto)",headers};
        }
        if(p===preview) break;
    }
    return null;
}
function fileHint(e) {
    const name=accessibleName(e) || e.getAttribute("title") || "";
    return /\b(?:archivos? adjuntos?|datos? adjuntos?|attachments?|adjuntos?)\b/i.test(name) &&
        !/\b(?:sin (?:archivos? |datos? )?adjuntos?|no attachments?)\b/i.test(name);
}
const sizeLine=t=>/^\d+(?:[,.]\d+)?\s*(?:bytes?|[kmgt]?i?b|[kmgt]o)$/i.test(clean(t));
function attachmentCards(v) {
    const result=[];
    for(const l of lists(v.area)) {
        const items=options(l);
        const size=Math.max(Number(l.getAttribute("aria-setsize")) || 0,...items.map(c=>Number(c.getAttribute("aria-setsize"))||0));
        if(!items.length || size>items.length) return null;
        result.push(...items);
    }
    // Some reading panes render a file tile with its size and menu but no ARIA list.
    const outside=e=>!withinContent(e) && !result.some(c=>c.contains(e));
    const sizes=all(v.area,'span,div,small').filter(e=>outside(e) && display(e) && !e.childElementCount && sizeLine(e.textContent));
    const controls=buttons(v.area);
    for(const size of sizes) {
        if(!outside(size)) continue;
        let card=null;
        for(let p=size.parentElement;p && p!==v.area && !p.contains(v.body);p=p.parentElement) {
            if(sizes.filter(s=>p.contains(s)).length!==1) break;
            const actions=controls.filter(b=>p.contains(b) && (fileMenu.test(label(b)) || b.getAttribute("aria-haspopup")==="menu"));
            if(actions.length===1 && lines(p).some(t=>!sizeLine(t) && !fileMenu.test(t))) {card=p;break;}
        }
        if(!card) return null;
        result.push(card);
    }
    const unresolved=all(v.area,'[aria-label],[title]').some(e=>outside(e) && display(e) &&
        !result.some(c=>e.contains(c)) && (fileHint(e) || fileMenu.test(label(e)) && /(?:archivo|adjunto|attachment|file)/i.test(label(e))));
    if(unresolved || (activeRow?.hasFile && !result.length)) return null;
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
if(operation.startsWith("rows-")) return folderRows();
if(operation==="clear") { clearPrint(arg.token); return true; }
if(operation==="state") {
    if(preview) return {kind:"preview",count:0,keys:[]};
    const list=draft();
    if(editor && list.root) return {kind:"draft",count:list.cards.length,keys:list.cards.map(cardKey)};
    if(editor) return {kind:"compose",count:lists(document).reduce((n,l)=>n+options(l).length,0),keys:[]};
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
