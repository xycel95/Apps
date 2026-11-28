// The caller has selected messages in this dedicated Outlook window. Only inspect and
// operate the rendered folder list and its reading pane. Never derive message URLs.
function folderRows() {
    const token=arg.token;
    if (!/^[a-f0-9]{32}$/.test(token)) return fail("lista-cambiada");
    const mark='data-correo-lista', scrollMark='data-correo-scroll', prior='data-correo-antes';
    const tag=attribute=>'['+attribute+'="'+token+'"]';
    if (operation==="rows-clear") {
        for (const attribute of [mark,scrollMark,prior]) for (const el of all(document,tag(attribute))) el.removeAttribute(attribute);
        return true;
    }
    const rowSelector='[role="option"],[role="listitem"],[role="row"],[role="treeitem"]';
    const groupSelector='[role="listbox"],[role="list"],[role="grid"],[role="treegrid"],[role="tree"],ul,ol';
    const folderName=/lista de (mensajes|correos|conversaciones)|message list|conversation list/i;
    const checked=r=>r.getAttribute('aria-selected')==='true' || r.getAttribute('aria-checked')==='true' ||
        r.getAttribute('data-is-selected')==='true' || all(r,'[role="checkbox"][aria-checked="true"],input[type=checkbox]:checked').some(c=>c.closest(rowSelector)===r);
    const eligible=r=>!withinContent(r) && !r.closest('nav,[role="navigation"],[role="menu"],[role="dialog"]') && display(r);
    function owner(r) {
        let nearest=null;
        for(let p=r.parentElement;p && p!==document.body;p=p.parentElement) {
            const name=p.getAttribute('aria-label') || '';
            if(listName(name) || /^(carpetas|folders|navigation pane|panel de navegación)$/i.test(name)) return null;
            if(folderName.test(name)) return p;
            if(!nearest && p.matches(groupSelector)) nearest=p;
        }
        return nearest;
    }
    const rowsOf=list=>all(list,rowSelector).filter(r=>eligible(r) && owner(r)===list &&
        !r.parentElement.closest(rowSelector)?.closest(tag(mark)));
    function scroller(list,first) {
        for(let p=first?.parentElement;p && p!==document.documentElement;p=p.parentElement) {
            if(p.clientHeight>0 && p.scrollHeight>p.clientHeight+2 && /auto|scroll|overlay/.test(getComputedStyle(p).overflowY)) return p;
        }
        return list;
    }
    function scrollToOffset(element,offset) {
        const old=element.scrollTop;
        element.scrollTop=offset;
        if(old!==element.scrollTop) element.dispatchEvent(new Event('scroll'));
        return element.scrollTop;
    }
    if(operation==="rows-start") {
        if(editor || preview || selectionCount()!==arg.count) return fail("cambio-seleccion");
        const groups=new Set(all(document,rowSelector).filter(r=>eligible(r)&&checked(r)).map(owner).filter(Boolean));
        const preferred=[...groups].filter(l=>folderName.test(l.getAttribute('aria-label')||''));
        const list=preferred.length===1?preferred[0]:groups.size===1?[...groups][0]:null;
        if(!list) return {available:false};
        list.setAttribute(mark,token);
        const box=scroller(list,rowsOf(list)[0]);
        box.setAttribute(scrollMark,token);
        const saved=box.scrollTop;
        scrollToOffset(box,0);
        return {available:true,saved,visible:document.visibilityState==="visible"};
    }
    const list=document.querySelector(tag(mark)), box=document.querySelector(tag(scrollMark));
    if(!list || !box || !display(list) || editor || preview) return fail("lista-cambiada");
    const rows=rowsOf(list);
    const pos=r=>Math.round(box.scrollTop+r.getBoundingClientRect().top-box.getBoundingClientRect().top);
    const stamp=text=>{
        let hash=2166136261;
        for(let i=0;i<text.length;i++) hash=Math.imul(hash^text.charCodeAt(i),16777619);
        return text.length+'/'+hash;
    };
    const panelStamp=v=>stamp(v.body.innerText)+"|"+stamp([v.title,...v.headers].join("\n"));
    const parts=r=>{
        const values=[...lines(r), ...all(r,'[title],span[aria-label]').filter(display).map(el=>clean(el.getAttribute('title') || el.getAttribute('aria-label')))];
        return [...new Set([...values,...all(r,'span,div').filter(e=>!e.childElementCount && display(e)).map(e=>clean(e.textContent))].filter(Boolean))];
    };
    function identity(r) {
        const wrapper=r.parentElement;
        const uniqueWrapper=wrapper && wrapper.querySelectorAll(rowSelector).length===1 ? wrapper : null;
        for(const field of ['data-item-id','data-itemid','data-convid','data-conversation-id','data-message-id','data-selection-key','data-conversationid']) {
            const id=r.getAttribute(field) || uniqueWrapper?.getAttribute(field);
            if(id) return field+'='+id;
        }
        const index=r.getAttribute('aria-posinset') || r.getAttribute('aria-rowindex');
        if(index && /^[1-9]\d*$/.test(index)) return 'posición='+index;
        if(r.id) return 'id='+r.id;
        const text=clean(r.innerText);
        if(text) return 'texto='+text;
        return r.getAttribute('aria-label') ? 'etiqueta='+clean(r.getAttribute('aria-label')) : '';
    }
    function witness(r) {
        if(!r.querySelector(rowSelector)) return clean(r.innerText) || accessibleName(r);
        return all(r,'div,span').filter(e=>!e.childElementCount && display(e) && e.closest(rowSelector)===r &&
            !e.closest('button,[role="button"]')).map(e=>clean(e.textContent)).filter(Boolean).join(" ") || accessibleName(r);
    }
    if(operation==="rows-page") {
        if(selectionCount()!==arg.count) return fail("cambio-seleccion");
        return {top:box.scrollTop,height:box.clientHeight,end:box.scrollTop+box.clientHeight>=box.scrollHeight-3,
            rows:rows.filter(checked).map(r=>({key:identity(r),text:witness(r),y:pos(r),top:box.scrollTop}))};
    }
    if(operation==="rows-scroll") return scrollToOffset(box,arg.top);
    const toggleName=/^(?:expandir|contraer|expand|collapse|desplegar|plegar)\s+(?:(?:la|esta|the|this)\s+)?(?:conversación|conversation)\b/iu;
    const toggle=r=>buttons(r).find(b=>b.closest(rowSelector)===r && toggleName.test(label(b)));
    function isExpanded(r,b) {
        const state=b?.getAttribute('aria-expanded') ?? r.getAttribute('aria-expanded');
        return state===null ? /^(?:contraer|collapse|plegar) /i.test(b?label(b):"") : state==="true";
    }
    const itemFields=['data-item-id','data-itemid','data-message-id','data-selection-key'];
    const itemId=e=>itemFields.map(k=>e.getAttribute(k)).find(Boolean);
    const memberKey=r=>itemId(r) || r.id;
    function members(parent) {
        const b=toggle(parent);
        if(!b || !isExpanded(parent,b)) return [];
        const refs=[b,parent].flatMap(e=>['aria-controls','aria-owns'].flatMap(a=>(e.getAttribute(a)||'').split(/\s+/))).filter(Boolean);
        const owned=refs.map(id=>document.getElementById(id)).filter(e=>e && e!==parent && e!==list &&
            !withinContent(e) && !e.contains(parent) && !e.closest('nav,[role="navigation"]'));
        let found=[...new Set([parent,...owned].flatMap(g=>[...(g!==parent && g.matches(rowSelector)?[g]:[]),...all(g,rowSelector)]))].filter(eligible);
        if(!found.length) {
            const level=Number(parent.getAttribute('aria-level'));
            const conversation=parent.getAttribute('data-convid') || parent.getAttribute('data-conversation-id');
            const flat=all(list,rowSelector).filter(eligible);
            for(const r of flat.slice(flat.indexOf(parent)+1)) {
                const deeper=level>0 && Number(r.getAttribute('aria-level'))===level+1;
                const same=conversation && (r.getAttribute('data-convid') || r.getAttribute('data-conversation-id'))===conversation && itemId(r);
                if(!deeper && !same) break;
                found.push(r);
            }
        }
        return found;
    }
    function findUnique(items,predicate) {
        const hits=items.filter(predicate);
        return hits.length===1?hits[0]:null;
    }
    const parent=arg.parentKey ? findUnique(rows,r=>identity(r)===arg.parentKey) : null;
    const matches=arg.parentKey ? (parent?members(parent).filter(r=>memberKey(r)===arg.key):[]) : rows.filter(r=>identity(r)===arg.key);
    if(matches.length>1) return fail("fila-ambigua");
    const row=matches[0];
    if(operation==="rows-before") { const pane=view(); return pane?panelStamp(pane):null; }
    if(operation==="rows-metrics") return {top:box.scrollTop,height:box.clientHeight,end:box.scrollTop+box.clientHeight>=box.scrollHeight-3};
    if(operation==="rows-inspect") {
        if(!row) return null;
        if(witness(row)!==arg.text) return fail("fila-cambiada");
        if(arg.first && (selectionCount()!==arg.count || !checked(parent || row))) return fail("cambio-seleccion");
        const b=toggle(row);
        return {group:!arg.parentKey && (!!b || row.hasAttribute('aria-expanded')),expanded:!!b && isExpanded(row,b),
            titles:[...new Set([...parts(row),...(arg.parentTitles || [])])], hasFile:fileHint(row) || all(row,'[aria-label],[title],[data-icon-name]').some(e=>
                e.closest(rowSelector)===row && (fileHint(e) || /^(?:attach|paperclip)$/i.test(e.getAttribute('data-icon-name') || '')))};
    }
    if(operation==="rows-toggle") {
        const b=row && toggle(row);
        if(!b || isExpanded(row,b)!==arg.expanded) return null;
        return b;
    }
    if(operation==="rows-members") {
        if(!row) return null;
        const found=members(row);
        if(!found.length) return null;
        const setSizes=found.map(r=>Number(r.getAttribute('aria-setsize'))).filter(n=>n>0);
        const b=toggle(row);
        const countText=[row.getAttribute('aria-label'),b?.getAttribute('aria-label'),b?.getAttribute('title')].filter(Boolean).join(' ');
        const counts=[...countText.matchAll(/\b(\d+)\s+(?:mensajes?|messages?|correos?)\b/ig)].map(m=>Number(m[1]));
        const expected=new Set([...setSizes,...counts]);
        if(expected.size!==1 || !expected.has(found.length)) return null;
        const keys=found.map(memberKey);
        if(keys.some(k=>!k) || keys.length!==new Set(keys).size || found.some(r=>toggle(r))) return null;
        return found.map((r,i)=>({key:keys[i],text:witness(r),parentKey:arg.key,y:pos(r),top:box.scrollTop}));
    }
    if(operation==="rows-open") {
        if(!row || witness(row)!==arg.text) return fail("fila-cambiada");
        if(arg.first && (selectionCount()!==arg.count || !checked(parent || row))) return fail("cambio-seleccion");
        for(const old of all(document,tag(prior))) old.removeAttribute(prior);
        for(const old of bodies) old.setAttribute(prior,token);
        const leaf=all(row,'span,div').find(e=>display(e) && !e.childElementCount && e.closest(rowSelector)===row && clean(e.textContent) &&
            !e.closest('button,a,input,[role="button"],[role="checkbox"]') && e.getBoundingClientRect().width>8);
        return leaf || row;
    }
    if(operation==="rows-diagnostic") {
        const external=s=>all(document,s).filter(e=>display(e) && !withinContent(e) && !list.contains(e));
        return {bodies:bodies.length, documents:all(document,'[role="document"]').filter(display).length,
            headings:external(subjectSelector).length,replies:buttons(document).filter(e=>!list.contains(e) && replyAction.test(label(e))).length,
            sizes:external('span,div,small').filter(e=>!e.childElementCount && sizeLine(e.textContent)).length};
    }
    if(operation==="rows-ready") {
        if(!row) return "fila-fuera-de-vista";
        if(selectionCount()>1) return "seleccion-multiple";
        if(bodies.length>1) return "varios-cuerpos";
        if(!bodies.length) return "cuerpo-sin-identificar";
        if(arg.before && bodies[0].getAttribute(prior)===token && stamp(bodies[0].innerText)===arg.before.split("|")[0]) return "anterior";
        const pane=view();
        if(!pane) return "cabecera-sin-identificar";
        if(pane.body.closest('[aria-busy="true"]') || pane.area.closest('[aria-busy="true"]')) return "cargando";
        if(buttons(pane.area).some(b=>b.getAttribute('aria-expanded')==='false' &&
            /^(?:expandir|expand|mostrar|show) (?:el |the )?(?:mensaje|message)\b/i.test(label(b)))) return "varios-cuerpos";
        // An Outlook message ID can be used as evidence only when Outlook itself
        // exposes the same value on the row and the open message or the current URL.
        const id=itemId(row);
        let urlId=null;
        try { const tail=/\/id\/([^/]+)\/?$/i.exec(location.pathname); if(tail) urlId=decodeURIComponent(tail[1]); } catch {}
        let node=pane.body.parentElement, paneId=null;
        for(;node && pane.area.contains(node);node=node.parentElement) {paneId=itemId(node);if(paneId || node===pane.area) break;}
        if(paneId && id && id!==paneId) return "identidad-distinta";
        const agrees=!!id && (id===paneId || id===urlId);
        const current=checked(row) || row.contains(document.activeElement) || ['true','page'].includes(row.getAttribute('aria-current')) ||
            (!!row.id && list.getAttribute('aria-activedescendant')===row.id) ||
            (row.getAttribute('tabindex')==='0' && all(list,rowSelector).filter(r=>display(r) && r.getAttribute('tabindex')==='0').length===1);
        if(!current && !agrees) return "fila-sin-marca-activa";
        const subject=subjectKey(pane.title);
        const wording=lower(row.getAttribute('aria-label'));
        const candidates=activeRow?.titles || parts(row);
        if(!agrees && (!subject || !(candidates.some(t=>subjectKey(t)===subject) || (' '+wording+' ').includes(' '+subject+' ')))) return "asunto-distinto";
        if(arg.before && (panelStamp(pane)===arg.before ||
            (pane.body.getAttribute(prior)===token && stamp(pane.body.innerText)===arg.before.split("|")[0]))) return "anterior";
        return "listo";
    }
    return fail("operacion");
}
