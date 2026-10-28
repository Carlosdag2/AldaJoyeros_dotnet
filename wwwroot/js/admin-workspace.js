(() => {
    'use strict';
    const dialog = document.querySelector('[data-admin-dialog]');
    if (!dialog) return;
    const content = dialog.querySelector('[data-dialog-content]');
    const heading = dialog.querySelector('h2');
    let pending = false, opener, saved, generation = 0;
    async function request(url, options = {}, json = true) {
        const abort = new AbortController();
        const timeout = setTimeout(() => abort.abort(), 20000);
        try {
            const response = await fetch(url, { credentials:'same-origin', cache:'no-store', ...options, signal:abort.signal,
                headers: { 'X-Requested-With':'XMLHttpRequest', Accept:json ? 'application/json' : 'text/html', ...options.headers } });
            if (response.redirected) throw new Error('Tu sesión puede haber caducado. Actualiza la página e inicia sesión para continuar.');
            const type = response.headers.get('Content-Type') || '';
            if (json && !type.includes('application/json')) throw new Error('No se pudo confirmar el cambio. Actualiza la página antes de repetirlo.');
            const data = json ? await response.json() : await response.text();
            if (!response.ok || data?.success === false) {
                const error = new Error(data?.message || (response.status === 404 ? 'Este elemento ya no está disponible.' : response.status === 400 ? 'No se pudo validar la solicitud. Actualiza la página.' : 'No se ha podido completar la acción.'));
                error.errors = data?.errors; throw error;
            }
            return data;
        } catch (error) {
            if (error.name === 'AbortError') throw new Error('La solicitud ha tardado demasiado. Comprueba la lista antes de repetir la acción.');
            throw error;
        } finally { clearTimeout(timeout); }
    }
    function start(title, onSaved) {
        if (dialog.open) return false;
        opener = document.activeElement; saved = onSaved; heading.textContent = title;
        content.replaceChildren(); dialog.showModal(); return true;
    }
    function setPending(value) {
        pending = value; dialog.setAttribute('aria-busy', String(value));
        dialog.querySelectorAll('[data-dialog-close],button[type=submit],fieldset').forEach(e => e.disabled = value);
    }
    async function open({url, title, onSaved}) {
        if (!start(title, onSaved)) return;
        const version = ++generation;
        content.textContent = 'Cargando…';
        try {
            const html = await request(url, {}, false);
            if (!dialog.open || generation !== version) return;
            content.innerHTML = html;
            (content.querySelector('input:not([type=hidden]),select') || heading).focus();
        } catch (error) {
            if (dialog.open && generation === version) { content.textContent = error.message; heading.focus(); }
        }
    }
    function confirm({title, message, url, id, permanent = false, onSaved, fields = {}}) {
        if (!start(title, onSaved)) return;
        const form = document.createElement('form'); form.action = url; form.dataset.adminEditor = '';
        const hidden = document.createElement('input'); hidden.type = 'hidden'; hidden.name = 'id'; hidden.value = id;
        const explanation = document.createElement('p'); explanation.className = 'mb-5 break-words text-sm leading-relaxed text-slate-600'; explanation.textContent = message;
        const status = document.createElement('div'); status.dataset.editorStatus = ''; status.className = 'mb-4 text-sm text-red-700'; status.setAttribute('role','alert'); status.tabIndex = -1;
        form.append(hidden, explanation, status);
        for (const [name,value] of Object.entries(fields)) {
            const input = document.createElement('input'); input.type='hidden'; input.name=name; input.value=value; form.append(input);
        }
        if (permanent) {
            const label = document.createElement('label'); label.className = 'mb-5 flex items-start gap-3 text-sm';
            const check = document.createElement('input'); check.type = 'checkbox'; check.required = true; check.className = 'mt-1';
            label.append(check, document.createTextNode('Entiendo que el producto se eliminará definitivamente y no se podrá restaurar.')); form.append(label);
        }
        const actions = document.createElement('div'); actions.className = 'flex flex-wrap justify-end gap-3';
        const cancel = document.createElement('button'); cancel.type='button'; cancel.dataset.dialogClose=''; cancel.className='admin-row-action border-slate-300 text-slate-600'; cancel.textContent='Cancelar';
        const submit = document.createElement('button'); submit.type='submit'; submit.className='min-h-11 rounded-lg px-5 py-3 text-sm font-semibold text-white '+(permanent?'bg-red-700':'bg-emerald-800'); submit.textContent=title;
        actions.append(cancel,submit); form.append(actions); content.append(form); cancel.focus();
    }
    dialog.addEventListener('click', event => { if (event.target.closest('[data-dialog-close]') && !pending) dialog.close(); });
    dialog.addEventListener('cancel', event => { if (pending) event.preventDefault(); });
    dialog.addEventListener('close', () => {
        generation++;
        if (opener?.isConnected) opener.focus({preventScroll:true});
        else document.querySelector('[data-product-search] input')?.focus({preventScroll:true});
    });
    dialog.addEventListener('submit', async event => {
        const form = event.target.closest('[data-admin-editor]'); if (!form) return;
        event.preventDefault(); if (pending || !form.reportValidity()) return;
        const body = new FormData(form);
        // Number inputs submit a dot; match the culture used by the server form binder.
        if (form.elements.Precio) body.set('Precio', form.elements.Precio.value.replace('.', form.elements.Precio.dataset.decimalSeparator));
        body.set('__RequestVerificationToken', document.querySelector('meta[name=cart-csrf]').content);
        const status = form.querySelector('[data-editor-status]'); status.textContent='Guardando…';
        form.querySelectorAll('[aria-invalid]').forEach(e=>e.removeAttribute('aria-invalid'));
        setPending(true);
        try {
            const result = await request(form.action, {method:'POST', body});
            // The write succeeded. A refresh failure must never invite repeating the write.
            try { await saved?.(result); }
            catch { window.AldaToast.warning('El cambio se guardó, pero no se pudo actualizar la vista. Recarga la página para comprobarlo.'); }
            dialog.close(); window.AldaToast.success(result.message || 'Cambios guardados.');
        } catch (error) {
            status.textContent = error.message;
            if (error.errors) {
                for (const [key, messages] of Object.entries(error.errors)) {
                    form.elements.namedItem(key)?.setAttribute('aria-invalid','true');
                    const line = document.createElement('p'); line.textContent=messages.join(' '); status.append(line);
                }
            }
            status.focus();
        } finally { setPending(false); }
    });
    window.AldaAdminDialog = {open, confirm, request};
})();
