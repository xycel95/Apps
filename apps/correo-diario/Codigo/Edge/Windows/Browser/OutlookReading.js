// Reading panes can show one message, quoted sections of one message, or distinct cards.
function messageItemId(e) {
    return e && ['data-item-id','data-itemid','data-message-id'].map(a=>e.getAttribute(a)).find(Boolean);
}
function quotedPart(body) {
    if(body.closest('blockquote,[data-app-section="QuotedText" i],[data-app-section="QuotedBody" i],[id*="divRplyFwdMsg" i]')) return true;
    const opening=clean(body.innerText).slice(0,1800);
    return /^(?:de|from)\s*:/i.test(opening) &&
        /(?:enviado(?: el)?|sent|fecha|date)\s*:.+?(?:para|to)\s*:.+?(?:asunto|subject)\s*:/i.test(opening);
}
function cardHeader(area,body) {
    if(!area || !body || !area.contains(body)) return null;
    const inHeader=e=>!withinContent(e) && display(e) && !e.contains(body) &&
        !!(e.compareDocumentPosition(body)&Node.DOCUMENT_POSITION_FOLLOWING);
    const fields=all(area,'time,[datetime],span,div,button,a,address,h3,[role="heading"]').filter(inHeader);
    const leaves=fields.filter(e=>!e.childElementCount);
    const to=leaves.find(e=>recipient(clean(e.innerText)));
    const explicit=fields.find(e=>e.matches('[data-app-section="Sender" i],[data-testid="sender" i],address') ||
        /^(?:de|from|remitente|sender)\s*:/i.test(accessibleName(e) || (!e.childElementCount?clean(e.innerText):'')));
    const person=explicit || (to && leaves.find(e=>
        !!(e.compareDocumentPosition(to)&Node.DOCUMENT_POSITION_FOLLOWING) && !e.closest(subjectSelector+',button[aria-haspopup="menu"]') &&
        !replyAction.test(label(e)) && !dateLine(label(e)) && /\p{L}{3}/u.test(clean(e.innerText))));
    const date=fields.filter(e=>!e.childElementCount || e.matches('time,[datetime]')).flatMap(e=>
        [e.getAttribute('datetime'),e.title,e.getAttribute('aria-label'),!e.childElementCount ? e.innerText : ''].map(clean))
        .find(t=>t.length<110 && /\d{4}/.test(t) && /\d{1,2}:\d{2}/.test(t) && !/completad|seguimiento|follow.up|completed/i.test(t));
    if(!person || !date || !(to || explicit && buttons(area).some(e=>inHeader(e) && replyAction.test(label(e))))) return null;
    const sender=clean(person.innerText || accessibleName(person) || person.title);
    return sender?{sender,date}:null;
}
function readingLayout() {
    if(layoutCache) return layoutCache;
    let candidates=preview ? bodies.filter(b=>preview.contains(b)) : bodies;
    const inPane=candidates.filter(b=>b.closest(mailRegion));
    if(!preview && inPane.length) candidates=inPane;
    let root=preview || candidates[0]?.closest(mailRegion);
    while(root && !candidates.every(b=>root.contains(b))) root=root.parentElement?.closest(mailRegion);
    const native='[data-item-id],[data-itemid],[data-message-id],[data-app-section="ConversationItem" i],[data-app-section="ConversationItemPart" i],[data-app-section="MessageCard" i],[role="listitem"],[role="article"],article';
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
    // A quoted reply can be rendered in its own sibling article after the reply toolbar.
    // Join it only when both sections have a common reading pane and the second has
    // neither its own message identity nor an independent Outlook header or action.
    const merged=[];
    for(const g of groups) {
        const previous=merged.at(-1), body=g.parts[0];
        const region=body?.closest(mailRegion);
        const shell=g.area;
        const previousId=messageItemId(previous?.area), nextId=messageItemId(shell);
        const earlier=previous?.parts.at(-1);
        let common=earlier?.parentElement;
        while(common && !common.contains(body)) common=common.parentElement;
        const sameRegion=common && (root?.contains(common) || (region ? earlier?.closest(mailRegion)===region :
            !earlier?.closest(mailRegion) && !common.matches('body,html') && !common.querySelector('nav,[data-correo-lista],[role="tree"],[contenteditable="true"]')));
        const separateHeader=common && earlier && quotedPart(body) && all(common,'time,[datetime],[data-app-section="Sender" i],span,div').some(e=>
            !e.childElementCount && !withinContent(e) && display(e) && !e.contains(body) &&
            !!(earlier.compareDocumentPosition(e)&Node.DOCUMENT_POSITION_FOLLOWING) &&
            !!(e.compareDocumentPosition(body)&Node.DOCUMENT_POSITION_FOLLOWING) &&
            (e.matches('time,[datetime],[data-app-section="Sender" i]') || /^(?:de|from)\s*:/i.test(clean(e.innerText)) ||
            /\d{1,2}[/.]\d{1,2}[/.]\d{4}.*\d{1,2}:\d{2}/.test(clean(e.innerText))));

        const ownedShell=shell && !shell.contains(previous?.parts[0] || document.body);
        if(previous && sameRegion && !separateHeader && g.parts.every(quotedPart) && previous.parts.some(p=>cardHeader(previous.area,p)) &&
            (!nextId || nextId===previousId) && (!ownedShell || !cardHeader(shell,body) &&
            !buttons(shell).some(b=>replyAction.test(label(b)) || b.getAttribute('aria-expanded')==='false') && !lists(shell).length)) {
            previous.parts.push(...g.parts);
        } else merged.push(g);
    }
    groups=merged;
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
        const header=cardHeader(g.area,g.parts[0]);
        return header?"cabecera="+JSON.stringify([lower(header.sender),header.date]):"";
    };
    for(const g of groups) {
        g.key=key(g);
        // Separate semantic bodies may be sections of an explicitly quoted message.
        // An unlabelled second body never certifies the whole card.
        g.ambiguous=g.parts.length>1 && !g.parts.slice(1).every(quotedPart);
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
function visibleMemberPlan(facts={}) {
    facts.reason="una-tarjeta";
    const {groups,scope}=readingLayout();
    if(groups.length<2) return null;
    if(!scope) {facts.reason="sin-panel-común";return null;}
    if(groups.some(g=>g.ambiguous)) {facts.reason="cuerpos-sin-atribuir";return null;}
    if(groups.some(g=>!g.key)) {facts.reason="tarjeta-sin-identidad";return null;}
    if(new Set(groups.map(g=>g.key)).size!==groups.length) {facts.reason="identidades-repetidas";return null;}
    facts.reason="carga-en-curso";
    if(groups.some(g=>!scope.contains(g.area)) || scope.closest('[aria-busy="true"]') ||
        all(scope,'[aria-busy="true"],[role="progressbar"]').some(display)) return null;
    const total=new Set();
    const include=n=>{if(Number(n)>0) total.add(Number(n));};
    const countIn=name=>{for(const m of (name||'').matchAll(/\b(\d+)\s+(?:mensajes?|messages?|correos?)\b/ig)) include(m[1]);};
    include(activeRow?.memberCount);countIn(accessibleName(scope));
    for(const g of groups) include(g.area.getAttribute('aria-setsize'));
    const list=groups[0].area.parentElement?.closest('[role="list"],ol,ul,[data-app-section="ConversationItems" i]');
    // Some mail panes wrap each card in a layout div and put the total on the heading.
    if(scope) for(const h of all(scope,subjectSelector+', [role="status"],[data-app-section="ConversationHeader" i]'))
        if(display(h) && !withinContent(h) && !groups.some(g=>g.area.contains(h))) {
            countIn(h.getAttribute('aria-description'));countIn(accessibleName(h));
        }
    const explicit=!!list && groups.every(g=>list.contains(g.area));
    if(explicit) {countIn(accessibleName(list));include(list.getAttribute('aria-setsize'));}
    facts.expected=total.size===1?[...total][0]:total.size>1?-1:0;
    facts.reason="cantidad-distinta";
    if(total.size) { if(total.size!==1 || !total.has(groups.length)) return null; }
    else {
        facts.reason="lista-sin-total-comprobable";
        const children=explicit?[...list.children].filter(e=>display(e) && !e.matches('hr,[role="separator"]')):[];
        const mounted=explicit && children.length===groups.length && children.every(c=>groups.filter(g=>c===g.area || c.contains(g.area)).length===1);
        if(!mounted ||
            list.scrollHeight>list.clientHeight+2 || groups.some(g=>getComputedStyle(g.area).position==='absolute') ||
            buttons(scope).some(b=>/^(?:cargar más|mostrar más mensajes|load more|show more messages)/i.test(label(b)))) return null;
    }
    facts.reason="completo";
    return groups.slice().sort((a,b)=>a.area.compareDocumentPosition(b.area)&Node.DOCUMENT_POSITION_FOLLOWING?-1:1).map(g=>g.key);
}

function paneReport() {
    const layout=readingLayout(), groups=layout.groups;
    const hasHeader=g=>g.parts.length>0 && !!cardHeader(g.area,g.parts[0]);
    const choice=selectedCard();
    const v=choice?.parts.length && !choice.ambiguous?cardView(choice):null;
    const meta=v?outlookControls():null;
    const facts={},plan=visibleMemberPlan(facts);
    return {bodies:bodies.length,documents:all(document,'[role="document"]').filter(display).length,
        groups:groups.length,ownHeaders:groups.filter(hasHeader).length,withIds:groups.filter(g=>!!g.key).length,
        quoted:bodies.filter(quotedPart).length,closed:groups.filter(g=>g.trigger).length,expected:facts.expected ?? activeRow?.memberCount ?? 0,reason:facts.reason,
        scope:!!layout.scope,plan:!!plan,recipient:!!meta?.recipientMarker,
        senderControls:meta?.senderControls || 0,nativeSenders:meta?.explicitSenders || 0,address:!!meta?.sender,
        date:!!meta?.dates?.length};
}
