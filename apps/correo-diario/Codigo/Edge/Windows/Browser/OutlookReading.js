// Reading panes can show one message, quoted sections of one message, or distinct cards.
function messageItemId(e) {
    return e && ['data-item-id','data-itemid','data-message-id'].map(a=>e.getAttribute(a)).find(Boolean);
}
function readingLayout() {
    if(layoutCache) return layoutCache;
    let candidates=preview ? bodies.filter(b=>preview.contains(b)) : bodies;
    const inPane=candidates.filter(b=>b.closest(mailRegion));
    if(!preview && inPane.length) candidates=inPane;
    const root=preview || candidates[0]?.closest(mailRegion);
    const native='[data-item-id],[data-itemid],[data-message-id],[data-app-section="ConversationItem" i],[role="listitem"],[role="article"],article';
    const expandLabel=e=>/^(?:expandir|mostrar|expand|show) (?:este |this |el |the )?(?:mensaje|message)(?:$|[:,·]|\s+(?:from|de)\b)/i.test(label(e));
    function owner(e) {
        let shell=e.parentElement?.closest(native);
        if(shell && (shell.closest('[data-correo-lista]') || (root && !root.contains(shell)))) shell=null;
        const nearest=normalCard(e);
        if(shell && nearest && shell.contains(nearest) && candidates.some(b=>shell.contains(b) && !nearest.contains(b) && normalCard(b)!==nearest))
            return nearest;
        return shell || nearest;
    }
    let groups=[];
    for(const body of candidates) {
        const card=owner(body);
        let g=card && groups.find(g=>g.area===card);
        if(!g){g={area:card,parts:[],trigger:null};groups.push(g);}
        g.parts.push(body);
    }
    let scope=root;
    if(!scope && groups.length) {
        scope=groups[0].area;
        while(scope && !groups.every(g=>g.area && scope.contains(g.area))) scope=scope.parentElement;
        // Without a reading landmark, use only an explicit conversation list or its card.
        scope=scope?.closest('[role="list"],[data-app-section="ConversationContainer" i]') || scope;
        if(scope?.matches('body,html,main') || scope?.querySelector('[data-correo-lista]')) scope=null;
    }
    if(scope && !preview) {
        const controls=all(scope,'[aria-expanded="false"]').filter(e=>display(e) && !withinContent(e) &&
            !e.closest('[data-correo-lista]') && (expandLabel(e) || (e.matches(native) && (messageItemId(e) || e.hasAttribute('aria-controls')))));
        for(const ctl of controls) {
            const card=ctl.closest(native) || (ctl.hasAttribute('aria-controls') ? ctl : null);
            if(!card || groups.some(g=>g.area===card && g.parts.length)) continue;
            if(!groups.some(g=>g.area===card)) groups.push({area:card,parts:[],trigger:ctl});
        }
    }
    const key=g=>{
        if(!g.area) return "";
        const id=messageItemId(g.area);
        if(id) return "correo="+id;
        const reference=(g.trigger || g.area).getAttribute('aria-controls');
        if(g.area.id) return "tarjeta="+g.area.id;
        if(reference) return "control="+reference;
        return "";
    };
    for(const g of groups) {
        g.key=key(g);
        // Separate semantic bodies may be sections of an explicitly quoted message.
        // An unlabelled second body never certifies the whole card.
        g.ambiguous=g.parts.length>1 && !g.parts.slice(1).every(b=>
            !!b.closest('blockquote,[data-app-section="QuotedText" i],[data-app-section="QuotedBody" i],[id*="divRplyFwdMsg" i]') ||
            /^(?:de|from)\s*:/i.test(clean(b.innerText)) && /(?:enviado el|sent)\s*:/i.test(b.innerText) && /(?:asunto|subject)\s*:/i.test(b.innerText));
    }
    layoutCache={groups,scope};
    return layoutCache;
}
function selectedCard() {
    const {groups}=readingLayout();
    if(paneMember) return groups.find(g=>g.key===paneMember) || null;
    if(groups.length===1) return groups[0];
    if(activeRow?.itemId && (activeRow.child || !activeRow.conversation))
        return groups.find(g=>messageItemId(g.area)===activeRow.itemId) || null;
    return null;
}
function visibleMemberPlan() {
    const {groups,scope}=readingLayout();
    if(!scope || groups.length<2 || groups.some(g=>!g.key || g.ambiguous) || new Set(groups.map(g=>g.key)).size!==groups.length) return null;
    if(groups.some(g=>!scope.contains(g.area)) || scope.closest('[aria-busy="true"]') ||
        all(scope,'[aria-busy="true"],[role="progressbar"]').some(display)) return null;
    const total=new Set();
    const include=n=>{if(Number(n)>0) total.add(Number(n));};
    const countIn=name=>{for(const m of (name||'').matchAll(/\b(\d+)\s+(?:mensajes?|messages?|correos?)\b/ig)) include(m[1]);};
    include(activeRow?.memberCount);countIn(accessibleName(scope));
    for(const g of groups) include(g.area.getAttribute('aria-setsize'));
    const list=groups[0].area.parentElement?.closest('[role="list"],ol,ul');
    const explicit=!!list && groups.every(g=>list.contains(g.area));
    if(explicit) {countIn(accessibleName(list));include(list.getAttribute('aria-setsize'));}
    if(total.size) { if(total.size!==1 || !total.has(groups.length)) return null; }
    else {
        if(!explicit || !groups.every(g=>g.area.parentElement===list) || list.children.length!==groups.length ||
            list.scrollHeight>list.clientHeight+2 || groups.some(g=>getComputedStyle(g.area).position==='absolute') ||
            buttons(scope).some(b=>/^(?:cargar más|mostrar más mensajes|load more|show more messages)/i.test(label(b)))) return null;
    }
    return groups.slice().sort((a,b)=>a.area.compareDocumentPosition(b.area)&Node.DOCUMENT_POSITION_FOLLOWING?-1:1).map(g=>g.key);
}
