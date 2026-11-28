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
    const witness=r=>clean(r.innerText) || clean(r.getAttribute('aria-label'));
    if(operation==="rows-page") {
        if(selectionCount()!==arg.count) return fail("cambio-seleccion");
        return {top:box.scrollTop,height:box.clientHeight,end:box.scrollTop+box.clientHeight>=box.scrollHeight-3,
            rows:rows.filter(checked).map(r=>({key:identity(r),text:witness(r),y:pos(r),top:box.scrollTop}))};
    }
    if(operation==="rows-scroll") return scrollToOffset(box,arg.top);
    const matches=rows.filter(r=>identity(r)===arg.key);
    if(matches.length>1) return fail("fila-ambigua");
    const row=matches[0];
    if(operation==="rows-open") {
        if(!row) return null;
        if(witness(row)!==arg.text) return fail("fila-cambiada");
        if(arg.first && (selectionCount()!==arg.count || !checked(row))) return fail("cambio-seleccion");
        // Opening a conversation with several messages cannot certify an individual mail.
        if(buttons(row).some(b=>/^(?:expandir|contraer|expand|collapse) (?:la |the )?(?:conversación|conversation)\b/i.test(label(b))))
            return fail("hilo-agrupado");
        for(const old of all(document,tag(prior))) old.removeAttribute(prior);
        for(const old of bodies) old.setAttribute(prior,token);
        // Click neutral row content, away from the check box, flag and other actions.
        const leaf=all(row,'span,div').find(e=>display(e) && !e.childElementCount && clean(e.textContent) &&
            !e.closest('button,a,input,[role="button"],[role="checkbox"]') && e.getBoundingClientRect().width>8);
        return leaf || row;
    }
    if(operation==="rows-ready") {
        if(!row || (!checked(row) && !['true','page'].includes(row.getAttribute('aria-current'))) || selectionCount()>1) return false;
        if(bodies.length>1) return fail("hilo-agrupado");
        const pane=view();
        if(!pane || pane.area.closest('[aria-busy="true"]') || pane.body.closest('[aria-busy="true"]')) return false;
        if(pane.area.querySelector('button[aria-expanded="false"][aria-label*="mensaje" i],button[aria-expanded="false"][aria-label*="message" i]')) return fail("hilo-agrupado");
        const subject=lower(pane.title).replace(/^(?:(?:re|fw|fwd|rv)\s*:\s*)+/i,'');
        const sameTitle=t=>lower(t).replace(/^(?:(?:re|fw|fwd|rv)\s*:\s*)+/i,'')===subject;
        const wording=clean(row.getAttribute('aria-label')||'').toLocaleLowerCase();
        if(!subject || !(parts(row).some(sameTitle) || (' '+wording+' ').includes(' '+subject+' '))) return false;
        // A reused reading pane is accepted only after its displayed message has changed.
        if(arg.before && (panelStamp(pane)===arg.before ||
            (pane.body.getAttribute(prior)===token && stamp(pane.body.innerText)===arg.before.split("|")[0]))) return false;
        return true;
    }
    if(operation==="rows-before") { const pane=view(); return pane?panelStamp(pane):null; }
    return fail("operacion");
}
