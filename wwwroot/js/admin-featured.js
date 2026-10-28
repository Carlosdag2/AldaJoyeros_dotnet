(() => {
    const list = document.getElementById('seleccionDestacados');
    const form = document.getElementById('ordenDestacados');
    if (!list || !form) return;
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
    async function save(previous) {
        const order = ids();
        if (same(order, previous)) return;
        busy = true; list.setAttribute('aria-busy', 'true');
        status.textContent = 'Guardando orden…'; reload.hidden = true;
        const buttons = [...document.querySelectorAll('form[action] button')];
        const disabled = buttons.map(button => button.disabled);
        buttons.forEach(button => button.disabled = true); positions();
        const body = new FormData(form);
        order.forEach(id => body.append('orden', id));
        previous.forEach(id => body.append('anterior', id));
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 15000);
        try {
            const response = await fetch(form.action, { method: 'POST', body, credentials: 'same-origin', cache: 'no-store',
                headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest' }, signal: controller.signal });
            if (response.redirected || !response.headers.get('Content-Type')?.includes('application/json')) throw new Error('No se ha podido confirmar el guardado. Recarga la selección; puede que tengas que iniciar sesión.');
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(result.message || 'No se ha podido guardar el orden. Recarga para comprobar la selección.');
            status.textContent = result.message;
        } catch (error) {
            restore(previous);
            status.textContent = error.name === 'AbortError' ? 'No se ha podido confirmar el guardado. Recarga para comprobar el orden actual.' : error.message;
            reload.hidden = false;
        } finally {
            clearTimeout(timeout); busy = false; list.removeAttribute('aria-busy');
            buttons.forEach((button, index) => button.disabled = disabled[index]); positions();
        }
    }
    list.addEventListener('submit', event => {
        const button = event.submitter;
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
    list.addEventListener('pointerdown', event => {
        const handle = event.target.closest('[data-featured-drag]');
        if (!handle || busy || drag || event.button !== 0 || !event.isPrimary) return;
        const row = handle.closest('[data-featured-id]');
        drag = { row, handle, pointer: event.pointerId, previous: ids(), startX: event.clientX, startY: event.clientY, x: event.clientX, y: event.clientY, moved: false };
        handle.focus({ preventScroll: true }); handle.setPointerCapture(event.pointerId);
        event.preventDefault();
    });
    list.addEventListener('pointermove', event => {
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
    list.addEventListener('pointerup', event => { if (drag?.pointer === event.pointerId) finish(false); });
    list.addEventListener('pointercancel', event => { if (drag?.pointer === event.pointerId) finish(true); });
    list.addEventListener('lostpointercapture', event => { if (drag?.pointer === event.pointerId) finish(true); });
    document.addEventListener('keydown', event => { if (drag && event.key === 'Escape') { event.preventDefault(); finish(true); } });
    positions();
})();
