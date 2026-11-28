// Bind a menu to the tile or sender which opened it. Never follow a link in the message.
function outlookControls() {
    if(!/^[a-f0-9]{32}$/.test(arg.token)) return fail("control-cambiado");
    const token=arg.token, mark='data-cd-disparador', old='data-cd-control-previo';
    const tagged=a=>'['+a+'="'+token+'"]';
    const actionSelector='button,a[href],[role="menuitem"],[role="button"],[role="option"],[tabindex]';
    const nativePopup='[role="menu"],[role="listbox"],[role="dialog"],[role="tooltip"],[data-is-focus-trap-zone],[data-popper-placement],.ms-Callout,.fui-PopoverSurface';
    const unique=nodes=>nodes.length===1 ? nodes[0] : null;
    const outer=nodes=>nodes.filter(e=>!nodes.some(p=>p!==e && p.contains(e)));
    const trusted=e=>display(e) && !withinContent(e) && !e.closest('[data-correo-lista],nav,[role="navigation"]');
    const enabled=e=>!e.closest('[aria-disabled="true"],[disabled]');
    const words=e=>[accessibleName(e),clean(e.getAttribute('title')),clean(e.innerText)].filter(Boolean);
    function download(e) {
        return words(e).some(t=>/^(?:descargar|download)(?: (?:una copia|a copy|archivo(?: adjunto)?|attachment|file))?$/i.test(t) ||
            !!arg.name && [lower('Descargar '+arg.name),lower('Download '+arg.name)].includes(lower(t)));
    }
    function choices(root) {
        const nodes=outer(all(root,actionSelector).filter(e=>trusted(e) && download(e)));
        for(const leaf of all(root,'span,div').filter(e=>!e.childElementCount && trusted(e) && download(e)))
            if(!nodes.some(e=>e.contains(leaf))) nodes.push(leaf);
        return nodes;
    }
    function linked(trigger,popup) {
        return ['aria-controls','aria-owns'].some(a=>(trigger.getAttribute(a)||'').split(/\s+/).some(id=>{
            const p=document.getElementById(id);return p && (p===popup || p.contains(popup));
        }));
    }
    if(operation==="control-clear") {
        for(const a of [mark,old]) for(const el of all(document,tagged(a))) el.removeAttribute(a);
        return true;
    }
    if(operation==="file-menu-visible") {
        const e=document.querySelector(tagged(mark));return !!e && e.getAttribute('aria-expanded')==='true';
    }
    if(operation==="file-start" || operation==="file-action") {
        const v=view();if(!v || arg.dialog && !preview) return fail("cambio-mensaje");
        const card=attachmentCards(v)?.[arg.index];
        if(!card || cardKey(card)!==arg.key || mailCard(card)) return fail("cambio-adjunto");
        if(operation==="file-start") {
            const direct=unique(choices(card).filter(enabled));
            if(direct) return {mode:"direct",element:direct};
            const ctl=menus(card);
            if(!ctl) return fail("acciones-adjunto");
            for(const e of choices(document)) e.setAttribute(old,token);
            ctl.setAttribute(mark,token);
            return {mode:"menu",element:ctl};
        }
        const trigger=card.querySelector(tagged(mark));
        if(!trigger) return fail("cambio-adjunto");
        const fresh=choices(document).filter(e=>e.getAttribute(old)!==token && !e.closest(tagged(old)));
        function fileMenuFor(e) {
            if(card.contains(e) || linked(trigger,e)) return true;
            // Outlook portals can render the four commands without menuitem roles.
            // Require an opened native menu or the surrounding attachment commands.
            for(let p=e.parentElement;p && p!==document.body;p=p.parentElement) {
                if(p.contains(v.body) || p.contains(trigger) || p.matches('main,[data-correo-lista]')) break;
                if(p.matches('[role="menu"]')) return true;
                const names=lines(p);
                if(names.length<=22 && names.some(t=>/^(?:vista previa|preview)$/i.test(t)) &&
                    names.some(t=>/^(?:copiar|copy|guardar en onedrive|save to onedrive)\b/i.test(t))) return true;
            }
            return false;
        }
        const options=fresh.filter(fileMenuFor);
        if(options.length>1) return fail("descarga-ambigua");
        if(options.length===1 && !enabled(options[0])) return fail("descarga-bloqueada");
        return options[0] || null;
    }
    if(operation.startsWith("details-")) {
        const detailName=/^(?:(?:ver |mostrar |view |show )?(?:detalles del mensaje|message details)|encabezados (?:de internet|del mensaje)|internet headers)$/i;
        if(operation==="details-start") {
            const v=view(); if(!v) return null;
            const tiles=attachmentCards(v) || [];
            const choices=buttons(v.card || v.area).filter(b=>!tiles.some(c=>c.contains(b)) &&
                !!(b.compareDocumentPosition(v.body)&Node.DOCUMENT_POSITION_FOLLOWING) &&
                /^(?:más (?:acciones|opciones)|more (?:actions|options)|otras acciones)(?:$| (?:para|del|for|on) )/i.test(label(b)));
            const button=unique(choices);if(!button) return null;
            for(const e of all(document,nativePopup+',[role="menuitem"]').filter(trusted)) e.setAttribute(old,token);
            button.setAttribute(mark,token);return button;
        }
        if(operation==="details-action") {
            const trigger=document.querySelector(tagged(mark)); if(!trigger) return null;
            const items=outer(all(document,actionSelector).filter(e=>trusted(e) && enabled(e) && e.getAttribute(old)!==token &&
                (e.closest('[role="menu"],[role="menuitem"],.ms-Callout,[data-popper-placement]') || linked(trigger,e)) &&
                (words(e).some(t=>detailName.test(t)) || !arg.submenu && words(e).some(t=>/^(?:ver|vista|view)$/i.test(t)))));
            const direct=unique(items.filter(e=>words(e).some(t=>detailName.test(t))));
            if(direct) return {kind:"details",element:direct};
            return items.length===1?{kind:"submenu",element:items[0]}:null;
        }
        const panel=unique(all(document,'[role="dialog"],[role="alertdialog"]').filter(p=>trusted(p) && p.getAttribute(old)!==token &&
            (detailName.test(accessibleName(p)) || all(p,'h1,h2,h3,[role="heading"]').some(e=>display(e) && detailName.test(clean(e.innerText))))));
        if(!panel) return null;
        if(operation==="details-close") return named(buttons(panel),/^(?:cerrar|aceptar|close|ok)$/i);
        const blocks=all(panel,'textarea,pre,[role="textbox"]').filter(display).map(e=>e.value || e.innerText);
        if(!blocks.length) blocks.push(panel.innerText);
        for(const block of blocks) {
            const fields=String(block).replace(/\r?\n[ \t]+/g,' ');
            const from=[...fields.matchAll(/^From:[ \t]*(.+)$/gmi)],date=[...fields.matchAll(/^Date:[ \t]*(.+)$/gmi)];
            if(from.length===1 && date.length===1) return {sender:clean(from[0][1]),date:clean(date[0][1])};
        }
        return null;
    }
    const email=txt=>[...new Set((txt||'').match(/[A-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Z0-9-]+(?:\.[A-Z0-9-]+)+/ig)||[])];
    if(operation==="sender-value" || operation==="sender-close") {
        const trigger=document.querySelector(tagged(mark));if(!trigger) return null;
        const name=lower(arg.person);
        const popups=all(document,nativePopup).filter(p=>trusted(p) && p.getAttribute(old)!==token && !p.contains(trigger) &&
            (linked(trigger,p) || name && (lower(p.innerText).includes(name) || lower(accessibleName(p)).includes(name))));
        const p=unique(popups.filter(el=>!popups.some(child=>child!==el && el.contains(child))));
        if(!p) return null;
        if(operation==="sender-close") return named(buttons(p),/^(?:cerrar(?: tarjeta(?: de perfil)?)?|close(?: profile card)?)$/i);
        const candidates=all(p,'a[href^="mailto:" i],[aria-label],[title]').filter(trusted);
        const addrs=new Set(candidates.flatMap(a=>email([a.getAttribute('href')?.split('?')[0],accessibleName(a),a.title].join(' '))));
        if(!addrs.size) for(const a of email(p.innerText)) addrs.add(a);
        return addrs.size===1 ? [...addrs][0] : null;
    }
    const v=view();if(!v) return null;
    const area=normalCard(v.body) || v.card || v.area;
    const tiles=attachmentCards(v) || [];
    const before=e=>trusted(e) && !e.contains(v.body) && !!(e.compareDocumentPosition(v.body)&Node.DOCUMENT_POSITION_FOLLOWING) &&
        !tiles.some(c=>c.contains(e)) && !e.closest('[role="toolbar"],[role="listbox"],[role="menu"]');
    const fields=all(area,'span,div,button,a,time,address,h3,[role="heading"],[datetime]').filter(before);
    const recipientNode=fields.find(e=>/^(?:para|to|cc)\s*:/i.test(clean(e.innerText)) && (!e.childElementCount || clean(e.innerText).length<100));
    const explicit=fields.filter(e=>e.matches('[data-app-section="Sender" i],[data-testid="sender" i]') ||
        /^(?:de|from|remitente|sender)\s*:/i.test(accessibleName(e) || (!e.childElementCount?clean(e.innerText):'')));
    const inSender=e=>explicit.some(s=>s===e || s.contains(e)) ||
        (recipientNode && !!(e.compareDocumentPosition(recipientNode)&Node.DOCUMENT_POSITION_FOLLOWING) && !e.contains(recipientNode));
    const senders=fields.filter(e=>inSender(e) && !e.closest(subjectSelector) && !e.querySelector(subjectSelector) &&
        !e.closest('[data-app-section="Recipients" i]') && !e.contains(recipientNode));
    const names=e=>[accessibleName(e),e.getAttribute('title'),e.matches('a[href^="mailto:" i]')?e.getAttribute('href').split('?')[0]:'',!e.childElementCount?e.innerText:''].filter(Boolean).join(' ');
    const addresses=new Set(senders.flatMap(e=>email(names(e))).map(s=>s.toLowerCase()));
    const dateText=t=>/\d{1,2}:\d{2}/.test(t) && /\d{4}/.test(t) && (/\d{1,4}[-/.]\d{1,2}[-/.]\d{1,4}/.test(t) || /\d{1,2}\s+(?:de\s+)?[a-záéíóú.]{3,}\s+(?:de\s+)?\d{4}/i.test(t));
    const dated=fields.filter(e=>e.matches('time,[datetime]') || !e.childElementCount)
        .flatMap(e=>[e.getAttribute('datetime'),e.getAttribute('title'),e.getAttribute('aria-label'),e.innerText].map(clean))
        .filter(t=>t.length<110 && dateText(t) && !/seguimiento|completad|follow.up|completed/i.test(t));
    const dates=[...new Set(dated)];
    const contact=senders.find(e=>e.matches('[aria-haspopup="dialog"],[data-app-section="Sender" i] [role="button"],[data-app-section="Sender" i] button') ||
        e.matches('button,[role="button"],[tabindex]') && /perfil|contacto|profile|contact card/i.test(accessibleName(e) || e.title)) ||
        unique(senders.filter(e=>e.matches('button,[role="button"]') && !e.closest('a[href]') && !e.querySelector('button,[role="button"]') && explicit.some(s=>s===e || s.contains(e)) && !replyAction.test(label(e)) && clean(e.innerText).length>2));
    const person=contact?clean(contact.innerText) || clean(contact.getAttribute('title')):"";
    if(operation==="metadata") return {language:document.documentElement.lang || "es",sender:addresses.size===1?[...addresses][0]:"",dates,person};
    if(operation==="sender-open" && contact && enabled(contact) && !contact.closest('a[href]') && person===arg.person) {
        for(const p of all(document,nativePopup).filter(trusted)) p.setAttribute(old,token);
        contact.setAttribute(mark,token); return contact;
    }
    return null;
}
