(() => {
    'use strict';
    const root = document.querySelector('[data-promotions-admin]');
    const api = window.AldaPromotionPanel;
    if (!root || !api) return;
    const dialog = root.querySelector('[data-offer-picker-dialog]');
    const form = dialog.querySelector('[data-picker-search]');
    const results = dialog.querySelector('[data-picker-results]');
    const error = dialog.querySelector('[data-picker-error]');
    let kind = 'home', choices = new Map(), page = 1, pages = 1, loading = false;
    let controller, generation = 0, timer;
    function showError(text = '') { error.textContent = text; error.hidden = !text; }
    function count() {
        const limit = ['popup', 'rule'].includes(kind) ? 1 : 8;
        dialog.querySelector('[data-picker-count]').textContent = choices.size + ' de ' + limit + ' seleccionados';
        dialog.querySelector('[data-picker-apply]').disabled = loading;
        results.querySelectorAll('[data-picker-product]').forEach(button => {
            const selected = choices.has(button.dataset.pickerProduct);
            button.setAttribute('aria-pressed', String(selected));
            button.classList.toggle('is-selected', selected);
        });
    }
    function card(product) {
        const button = document.createElement('button');
        button.type = 'button'; button.dataset.pickerProduct = String(product.id);
        button.className = 'offer-picker-card'; button.setAttribute('aria-pressed', String(choices.has(String(product.id))));
        if (choices.has(String(product.id))) button.classList.add('is-selected');
        const check = document.createElement('span'); check.className = 'offer-picker-check'; check.setAttribute('aria-hidden', 'true'); check.textContent = '✓'; button.append(check);
        const media = document.createElement('span'); media.className = 'offer-picker-media';
        if (product.image) { const img = document.createElement('img'); img.src = product.image; img.alt = ''; img.loading = 'lazy'; media.append(img); }
        else { media.innerHTML = '<i class="far fa-gem" aria-hidden="true"></i>'; }
        button.append(media);
        for (const [text, className] of [[product.category, 'offer-picker-category'], [product.name, 'offer-picker-name'], [product.price, 'offer-picker-price']]) {
            const span = document.createElement('span'); span.className = className; span.textContent = text || ''; button.append(span);
        }
        button.addEventListener('click', () => {
            if (loading) return;
            const id = String(product.id); showError();
            if (choices.has(id)) choices.delete(id);
            else if (['popup', 'rule'].includes(kind)) { choices.clear(); choices.set(id, product); }
            else if (choices.size >= 8) { showError('Ya has elegido 8 productos. Quita uno para seleccionar otro.'); return; }
            else choices.set(id, product);
            count();
        });
        return button;
    }
    async function load(next = 1) {
        controller?.abort(); controller = new AbortController(); const current = ++generation;
        loading = true; count(); showError(); results.setAttribute('aria-busy', 'true');
        dialog.querySelector('[data-picker-result]').textContent = 'Buscando ofertas…';
        const url = new URL(dialog.dataset.url, location.href);
        new FormData(form).forEach((value, key) => { if (value) url.searchParams.set(key, value); }); url.searchParams.set('page', String(next));
        if (kind === 'rule') url.searchParams.set('onlyDiscounted', 'false');
        try {
            const response = await fetch(url, { signal: controller.signal, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (response.redirected || !response.headers.get('Content-Type')?.includes('application/json')) throw Error('Actualiza el panel: puede que necesites iniciar sesión.');
            const data = await response.json();
            if (!response.ok || !data.success) throw Error(data.message || 'No se pudieron cargar las ofertas.');
            if (current !== generation || !dialog.open) return;
            page = data.page; pages = data.totalPages;
            results.replaceChildren(...data.items.map(card));
            dialog.querySelector('[data-picker-empty]').hidden = data.items.length > 0;
            dialog.querySelector('[data-picker-result]').textContent = data.total + (kind === 'rule' ? ' productos disponibles' : ' productos con descuento');
            dialog.querySelector('[data-picker-page-label]').textContent = page + ' / ' + pages;
            dialog.querySelector('[data-picker-page="-1"]').disabled = page <= 1;
            dialog.querySelector('[data-picker-page="1"]').disabled = page >= pages;
            dialog.querySelector('[data-picker-pages]').hidden = pages <= 1;
        } catch (e) { if (e.name !== 'AbortError' && current === generation) showError(e.message); }
        finally { if (current === generation) { loading = false; results.removeAttribute('aria-busy'); count(); } }
    }
    function open(mode) {
        if (api.busy()) return;
        kind = mode; choices = new Map(api.selected(kind).map(p => [String(p.id), p]));
        dialog.querySelector('#offer-picker-title').textContent = kind === 'rule' ? 'Elige el producto para el descuento' : kind === 'home' ? 'Elige las ofertas del inicio' : kind === 'mail' ? 'Elige los productos del correo' : 'Elige el producto del aviso';
        dialog.querySelector('[data-picker-help]').textContent = kind === 'rule' ? 'Busca por nombre o referencia y filtra por categoría. Pulsa una tarjeta para elegirla.' : kind === 'popup' ? 'Selecciona una pieza. Si dejas la selección vacía se usará el anuncio general.' : 'Pulsa las tarjetas para añadir o quitar productos. Puedes elegir hasta 8.';
        dialog.querySelector('[data-picker-apply]').textContent = kind === 'mail' ? 'Preparar estos productos' : 'Usar esta selección';
        dialog.querySelector('[data-picker-empty] p').textContent = kind === 'rule' ? 'No hay productos para esta búsqueda' : 'No hay ofertas para esta búsqueda';
        dialog.querySelector('[data-picker-create]').hidden = kind === 'rule';
        form.reset(); results.replaceChildren(); dialog.showModal(); count(); load();
    }
    root.addEventListener('click', event => {
        const button = event.target.closest('button'); if (!button) return;
        if (button.matches('[data-offer-picker]')) open(button.dataset.offerPicker);
        if (button.matches('[data-picker-close]')) dialog.close();
        if (button.matches('[data-picker-page]') && !loading) load(page + Number(button.dataset.pickerPage));
        if (button.matches('[data-picker-clear]')) { choices.clear(); showError(); count(); }
        if (button.matches('[data-picker-apply]') && !loading) { api.apply(kind, [...choices.values()]); dialog.close(); }
        if (button.matches('[data-picker-create]')) { dialog.close(); root.querySelector('[data-new-rule]').click(); }
        if (button.matches('[data-popup-general]') && !api.busy()) api.apply('popup', []);
        if (button.matches('[data-popup-preview]') && !api.busy()) {
            const preview = root.querySelector('[data-popup-preview-dialog]');
            const iframe = preview.querySelector('iframe');
            const url = new URL(iframe.dataset.url, location.href); const id = root.querySelector('[name="popupProductId"]').value;
            if (id) url.searchParams.set('productId', id);
            iframe.src = url.href; preview.showModal();
        }
        if (button.matches('[data-popup-preview-close]')) root.querySelector('[data-popup-preview-dialog]').close();
    });
    dialog.addEventListener('close', () => { generation++; controller?.abort(); clearTimeout(timer); loading = false; });
    root.querySelector('[data-popup-preview-dialog]').addEventListener('close', event => { event.target.querySelector('iframe').src = 'about:blank'; });
    form.addEventListener('submit', e => { e.preventDefault(); clearTimeout(timer); load(); });
    form.elements.search.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(() => load(), 250); });
    form.elements.categoryId.addEventListener('change', () => { clearTimeout(timer); load(); });

    // Mouse drag uses native drag and drop. Touch dragging is confined to the grip,
    // so normal page scrolling and product buttons remain available.
    let dragged = null, touch = null;
    const cards = () => [...root.querySelectorAll('[data-offer-card]')];
    function clearMarks() { cards().forEach(c => c.classList.remove('is-dragging', 'is-drop-target')); }
    root.addEventListener('dragstart', e => {
        const grip = e.target.closest('[data-home-drag]');
        if (!grip || api.busy()) { if (grip) e.preventDefault(); return; }
        dragged = grip.dataset.homeDrag; const card = grip.closest('[data-offer-card]'); card.classList.add('is-dragging');
        e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', dragged); e.dataTransfer.setDragImage(card, 25, 25);
    });
    root.addEventListener('dragover', e => {
        const target = e.target.closest('[data-offer-card]'); if (!dragged || !target || target.dataset.offerCard === dragged) return;
        e.preventDefault(); e.dataTransfer.dropEffect = 'move'; cards().forEach(c => c.classList.toggle('is-drop-target', c === target));
    });
    root.addEventListener('drop', e => {
        const target = e.target.closest('[data-offer-card]'); if (!dragged || !target) return;
        e.preventDefault(); const source = dragged; dragged = null; clearMarks(); api.move(source, target.dataset.offerCard);
    });
    root.addEventListener('dragend', () => { dragged = null; clearMarks(); });
    function finishTouch(cancelled) {
        if (!touch) return; const current = touch; touch = null;
        cancelAnimationFrame(current.frame); current.ghost?.remove();
        if (current.grip.hasPointerCapture(current.pointer)) current.grip.releasePointerCapture(current.pointer);
        clearMarks(); if (!cancelled && current.active && current.target) api.move(current.id, current.target);
    }
    function animateTouch() {
        if (!touch?.active) return;
        if (touch.y < 80) window.scrollBy(0, -12); else if (touch.y > innerHeight - 80) window.scrollBy(0, 12);
        touch.ghost.style.transform = `translate(${touch.x - touch.offsetX}px,${touch.y - touch.offsetY}px)`;
        const target = document.elementFromPoint(touch.x, touch.y)?.closest('[data-offer-card]');
        touch.target = target?.dataset.offerCard;
        cards().forEach(c => c.classList.toggle('is-drop-target', c === target && c.dataset.offerCard !== touch.id));
        touch.frame = requestAnimationFrame(animateTouch);
    }
    root.addEventListener('pointerdown', e => {
        const grip = e.target.closest('[data-home-drag]'); if (!grip || e.pointerType === 'mouse' || api.busy()) return;
        e.preventDefault(); const card = grip.closest('[data-offer-card]'), rect = card.getBoundingClientRect();
        touch = { id: grip.dataset.homeDrag, pointer: e.pointerId, grip, card, startX: e.clientX, startY: e.clientY, x: e.clientX, y: e.clientY, offsetX: e.clientX - rect.left, offsetY: e.clientY - rect.top, rect, active: false };
        grip.setPointerCapture(e.pointerId);
    });
    root.addEventListener('pointermove', e => {
        if (!touch || touch.pointer !== e.pointerId) return;
        touch.x = e.clientX; touch.y = e.clientY;
        if (!touch.active && Math.hypot(e.clientX - touch.startX, e.clientY - touch.startY) > 7) {
            touch.active = true; const clone = touch.card.cloneNode(true); clone.className = 'offer-selected-card offer-drag-ghost'; clone.style.width = touch.rect.width + 'px'; clone.setAttribute('aria-hidden', 'true'); clone.inert = true;
            document.body.append(clone); touch.ghost = clone; touch.card.classList.add('is-dragging'); animateTouch();
        }
        if (touch.active) e.preventDefault();
    });
    root.addEventListener('pointerup', e => { if (touch?.pointer === e.pointerId) finishTouch(false); });
    root.addEventListener('pointercancel', e => { if (touch?.pointer === e.pointerId) finishTouch(true); });
})();
