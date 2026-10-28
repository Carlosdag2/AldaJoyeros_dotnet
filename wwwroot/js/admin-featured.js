(() => {
    const root = document.getElementById('featuredPanel');
    if (!root || !window.AldaFeatured) return;
    let list = document.getElementById('seleccionDestacados');
    let form = document.getElementById('ordenDestacados');
    const filter = root.querySelector('[data-featured-filter]');
    let page = Number(new URL(location.href).searchParams.get('page')) || 1, reader = null, debounce, disabled = [], queued = null;
    const api = window.AldaFeatured;
    const status = document.getElementById('estadoOrdenDestacados');
    const reload = document.getElementById('recargarDestacados');
    let busy = false, drag = null, scrolling = 0;
    const rows = () => [...list.querySelectorAll('[data-featured-id]')];
    const ids = () => rows().map(row => row.dataset.featuredId);
    const same = (a, b) => a.join(',') === b.join(',');
    function restore(order) {
        const lookup = new Map(rows().map(row => [row.dataset.featuredId, row]));
        order.forEach(id => list.append(lookup.get(id)));
        positions();
    }
    function positions() {
        const items = rows();
        items.forEach((row, index) => {
            row.querySelector('[data-featured-position]').textContent = index + 1;
            row.querySelector('[value="subir"]').disabled = busy || index === 0;
            row.querySelector('[value="bajar"]').disabled = busy || index === items.length - 1;
            const handle = row.querySelector('[data-featured-drag]');
            handle.hidden = items.length < 2;
            handle.disabled = busy;
        });
    }
    function setBusy(value) {
        busy = value;
        if (value) {
            reader?.abort(); reader = null;
            root.setAttribute('aria-busy', 'true');
            disabled = [...root.querySelectorAll('[data-featured-change] button')].map(button => [button, button.disabled]);
            disabled.forEach(([button]) => button.disabled = true);
        } else {
            disabled.forEach(([button, wasDisabled]) => button.disabled = wasDisabled); disabled = [];
            if (!reader) root.removeAttribute('aria-busy');
            if (queued) { const next = queued; queued = null; setTimeout(() => load(next.page, next.push), 0); }
        }
        positions();
    }
    async function load(nextPage = 1, push = true, afterChange = false) {
        if (busy && !afterChange) { queued = { page: nextPage, push }; return; }
        if (afterChange) queued = null;
        if (drag) finish(true);
        reader?.abort(); const controller = new AbortController(); reader = controller;
        root.setAttribute('aria-busy', 'true');
        if (!busy) status.textContent = 'Actualizando resultados…';
        const url = new URL(root.dataset.indexUrl, location.href);
        const search = filter.elements.busqueda.value.trim(), category = filter.elements.categoriaId.value;
        if (search) url.searchParams.set('busqueda', search);
        if (category) url.searchParams.set('categoriaId', category);
        if (nextPage > 1) url.searchParams.set('page', nextPage);
        try {
            const result = await api.request(url, { signal: controller.signal });
            if (reader !== controller) return;
            root.querySelector('#featuredSelection').innerHTML = result.selectionHtml;
            root.querySelector('#featuredCandidates').innerHTML = result.candidatesHtml;
            list = root.querySelector('#seleccionDestacados'); form = root.querySelector('#ordenDestacados'); positions();
            page = result.page;
            if (page > 1) url.searchParams.set('page', page); else url.searchParams.delete('page');
            if (push && url.href !== location.href) history.pushState(null, '', url);
            if (!busy) status.textContent = 'Resultados actualizados.';
            reload.hidden = true;
        } catch (error) {
            if (error.name !== 'AbortError' && reader === controller) {
                status.textContent = (afterChange ? 'El cambio se ha guardado, pero no se ha podido actualizar el panel. ' : '') + error.message;
                reload.hidden = false;
            }
        } finally { if (reader === controller) { reader = null; if (!busy) root.removeAttribute('aria-busy'); } }
    }
    async function change(id, action) {
        setBusy(true); status.textContent = 'Actualizando selección…'; reload.hidden = true;
        try {
            const result = await api.change(id, action);
            if (result?.success) { status.textContent = result.message; await load(page, false, true); }
            else status.textContent = 'Operación cancelada. Se conserva la selección.';
        } catch (error) { status.textContent = error.message; reload.hidden = false; }
        finally { setBusy(false); }
    }
    async function save(previous) {
        const order = ids(); if (same(order, previous)) return;
        setBusy(true); status.textContent = 'Guardando orden…'; reload.hidden = true;
        const body = new FormData(form);
        order.forEach(id => body.append('orden', id)); previous.forEach(id => body.append('anterior', id));
        try { const result = await api.request(form.action, { method: 'POST', body }); status.textContent = result.message; }
        catch (error) { restore(previous); status.textContent = error.message; reload.hidden = false; }
        finally { setBusy(false); }
    }
    root.addEventListener('submit', event => {
        if (event.target === filter) { event.preventDefault(); clearTimeout(debounce); load(1); return; }
        const button = event.submitter;
        if (event.target.matches('[data-featured-change]') && ['añadir', 'quitar'].includes(button?.value)) {
            event.preventDefault(); if (!busy && !drag) change(event.target.elements.id.value, button.value); return;
        }
        if (!button || !['subir', 'bajar'].includes(button.value)) return;
        event.preventDefault();
        if (busy || drag) return;
        const previous = ids(), row = button.closest('[data-featured-id]');
        if (button.value === 'subir' && row.previousElementSibling) list.insertBefore(row, row.previousElementSibling);
        else if (button.value === 'bajar' && row.nextElementSibling) list.insertBefore(row.nextElementSibling, row);
        positions(); save(previous);
    });
    function move(clientX, clientY) {
        const bounds = list.getBoundingClientRect();
        if (clientX < bounds.left || clientX > bounds.right) return;
        const next = rows().find(row => {
            const box = row.getBoundingClientRect();
            return row !== drag.row && clientY < box.top + box.height / 2;
        }) || null;
        if (next === drag.row.nextElementSibling) return;
        list.insertBefore(drag.row, next);
        drag.handle.setPointerCapture(drag.pointer);
        positions();
    }
    function scroll() {
        if (!drag?.moved) return;
        const edge = 90;
        const speed = drag.y < edge ? -12 : drag.y > innerHeight - edge ? 12 : 0;
        const bounds = list.getBoundingClientRect();
        if ((speed < 0 && bounds.top < drag.y) || (speed > 0 && bounds.bottom > drag.y)) { window.scrollBy(0, speed); move(drag.x, drag.y); }
        scrolling = requestAnimationFrame(scroll);
    }
    root.addEventListener('pointerdown', event => {
        const handle = event.target.closest('[data-featured-drag]');
        if (!handle || busy || drag || event.button !== 0 || !event.isPrimary) return;
        reader?.abort(); reader = null;
        const row = handle.closest('[data-featured-id]');
        drag = { row, handle, pointer: event.pointerId, previous: ids(), startX: event.clientX, startY: event.clientY, x: event.clientX, y: event.clientY, moved: false };
        handle.focus({ preventScroll: true }); handle.setPointerCapture(event.pointerId);
        event.preventDefault();
    });
    root.addEventListener('pointermove', event => {
        if (!drag || drag.pointer !== event.pointerId) return;
        drag.x = event.clientX; drag.y = event.clientY;
        if (!drag.moved && Math.hypot(drag.x - drag.startX, drag.y - drag.startY) < 6) return;
        if (!drag.moved) { drag.moved = true; drag.row.classList.add('is-dragging'); scrolling = requestAnimationFrame(scroll); }
        event.preventDefault(); move(drag.x, drag.y);
    });
    function finish(cancelled) {
        if (!drag) return;
        const current = drag; drag = null; cancelAnimationFrame(scrolling);
        current.row.classList.remove('is-dragging');
        if (current.handle.hasPointerCapture(current.pointer)) current.handle.releasePointerCapture(current.pointer);
        if (cancelled) { restore(current.previous); status.textContent = 'Movimiento cancelado.'; }
        else save(current.previous);
    }
    root.addEventListener('pointerup', event => { if (drag?.pointer === event.pointerId) finish(false); });
    root.addEventListener('pointercancel', event => { if (drag?.pointer === event.pointerId) finish(true); });
    root.addEventListener('lostpointercapture', event => { if (drag?.pointer === event.pointerId) finish(true); });
    document.addEventListener('keydown', event => { if (drag && event.key === 'Escape') { event.preventDefault(); finish(true); } });
    filter.elements.busqueda.addEventListener('input', () => { clearTimeout(debounce); debounce = setTimeout(() => load(1), 350); });
    filter.elements.categoriaId.addEventListener('change', () => { clearTimeout(debounce); load(1); });
    root.addEventListener('click', event => {
        if (event.target.closest('[data-featured-clear]')) { event.preventDefault(); filter.elements.busqueda.value = ''; filter.elements.categoriaId.value = ''; clearTimeout(debounce); load(1); }
        if (event.target.closest('#recargarDestacados')) { event.preventDefault(); load(page); }
    });
    window.destacadosPagina = async nextPage => { await load(nextPage); root.querySelector('#catalogoTitulo').scrollIntoView({ behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'start' }); };
    window.addEventListener('popstate', () => {
        const url = new URL(location.href); filter.elements.busqueda.value = url.searchParams.get('busqueda') || ''; filter.elements.categoriaId.value = url.searchParams.get('categoriaId') || ''; load(Number(url.searchParams.get('page')) || 1, false);
    });
    positions();
})();
