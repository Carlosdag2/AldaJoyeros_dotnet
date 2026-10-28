(() => {
    const dialog = document.getElementById('featuredReplacement');
    if (!dialog) return;
    let generation = 0;
    async function request(url, options = {}) {
        const controller = new AbortController();
        const externalAbort = () => controller.abort();
        options.signal?.addEventListener('abort', externalAbort, { once: true });
        if (options.signal?.aborted) controller.abort();
        const timeout = setTimeout(() => controller.abort(), 15000);
        try {
            const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store', ...options,
                headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest', ...options.headers }, signal: controller.signal });
            if (response.redirected || !response.headers.get('Content-Type')?.includes('application/json')) throw new Error('No se ha podido confirmar el cambio. Actualiza la selección; puede que tengas que iniciar sesión.');
            const result = await response.json();
            if (!response.ok) { const error = new Error(result.message || 'No se ha podido completar la operación.'); error.status = response.status; throw error; }
            return result;
        } catch (error) {
            if (error.name === 'AbortError' && !options.signal?.aborted) throw new Error('No se ha podido confirmar el cambio. Actualiza la selección antes de intentarlo de nuevo.');
            throw error;
        } finally { clearTimeout(timeout); options.signal?.removeEventListener('abort', externalAbort); }
    }
    const selection = () => request(dialog.dataset.selectionUrl);
    function post(id, action, replacement, previous) {
        const body = new FormData();
        body.set('id', id); body.set('accion', action);
        body.set('__RequestVerificationToken', document.querySelector('meta[name="cart-csrf"]').content);
        if (replacement) { body.set('reemplazarId', replacement); previous.forEach(item => body.append('anterior', item)); }
        return request(dialog.dataset.changeUrl, { method: 'POST', body });
    }
    function replace(id) {
        return new Promise((resolve, reject) => {
            if (dialog.open) { reject(new Error('Termina primero la selección actual.')); return; }
            const form = dialog.querySelector('form'), options = dialog.querySelector('[data-featured-options]');
            const status = dialog.querySelector('[data-featured-dialog-status]');
            const confirm = dialog.querySelector('[data-featured-confirm]'), cancel = dialog.querySelector('[data-featured-cancel]');
            const disableChoices = disabled => form.querySelectorAll('input').forEach(input => input.disabled = disabled);
            const version = ++generation;
            let pending = false, result = null, snapshot = [];
            options.replaceChildren(); status.textContent = 'Cargando destacados actuales…'; confirm.disabled = true; cancel.disabled = false;
            function changed() { confirm.disabled = pending || !form.querySelector('input:checked'); }
            async function refresh(message = '') {
                confirm.disabled = true;
                try {
                    const data = await selection();
                    if (version !== generation || !dialog.open) return;
                    snapshot = data.items.map(item => String(item.id)); options.replaceChildren();
                    data.items.forEach((item, index) => {
                        const label = document.createElement('label'); label.className = 'featured-option flex min-w-0 cursor-pointer items-center gap-3 rounded-xl border border-slate-200 bg-white p-3';
                        const input = document.createElement('input'); input.type = 'radio'; input.name = 'replacement'; input.value = item.id; input.required = true; input.className = 'shrink-0';
                        label.append(input);
                        if (item.image) { const image = document.createElement('img'); image.src = item.image; image.alt = ''; image.className = 'h-12 w-12 shrink-0 rounded-lg bg-white object-contain'; label.append(image); }
                        const text = document.createElement('span'); text.className = 'min-w-0';
                        const name = document.createElement('span'); name.className = 'block break-words text-sm font-semibold'; name.textContent = `${index + 1}. ${item.name}`;
                        const category = document.createElement('span'); category.className = 'block break-words text-xs text-slate-500'; category.textContent = item.category + (item.hidden ? ' · Oculto' : ''); text.append(name, category); label.append(text); options.append(label);
                    });
                    status.textContent = message || `${data.items.length} de ${data.limit} posiciones ocupadas. Selecciona el producto que quieres sustituir.`;
                } catch (error) { if (version === generation && dialog.open) status.textContent = error.message; }
            }
            function close() { if (!pending) dialog.close(); }
            function cancelEvent(event) { if (pending) event.preventDefault(); }
            async function submit(event) {
                event.preventDefault();
                const choice = form.querySelector('input:checked');
                if (pending || !choice) return;
                pending = true; disableChoices(true); confirm.disabled = true; cancel.disabled = true; status.textContent = 'Guardando reemplazo…';
                try { result = await post(id, 'añadir', choice.value, snapshot); dialog.close(); }
                catch (error) { if (error.status === 409) await refresh(error.message); else status.textContent = error.message; }
                finally { pending = false; disableChoices(false); cancel.disabled = false; changed(); }
            }
            function closed() {
                generation++; cancel.removeEventListener('click', close); dialog.removeEventListener('cancel', cancelEvent); form.removeEventListener('submit', submit); form.removeEventListener('change', changed); dialog.removeEventListener('close', closed); resolve(result);
            }
            cancel.addEventListener('click', close); dialog.addEventListener('cancel', cancelEvent); form.addEventListener('submit', submit); form.addEventListener('change', changed); dialog.addEventListener('close', closed);
            dialog.showModal(); refresh();
        });
    }
    async function change(id, action) { const result = await post(id, action); return result.needsReplacement ? replace(id) : result; }
    window.AldaFeatured = Object.freeze({ request, selection, change });
})();
