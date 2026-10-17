(() => {
    'use strict';
    const el = id => document.getElementById(id);
    const interval = 5000;
    let repeat = document.body.dataset.repeat !== 'false';
    let deck, timer, hideTimer, paused = false, busy = false, active = false, pending = null, currentUrl = null, wakeLock = null;
    const message = error => error.message === 'session'
        ? 'La sesión ha caducado. Sal de la presentación, inicia sesión y vuelve a abrirla.'
        : 'No se ha podido cargar el catálogo o sus imágenes. Comprueba la conexión y pulsa Reintentar.';

    async function keepAwake() {
        if (!active || document.hidden || wakeLock || !navigator.wakeLock) return;
        try { wakeLock = await navigator.wakeLock.request('screen'); wakeLock.addEventListener('release', () => wakeLock = null); } catch { /* El dispositivo puede no permitirlo. */ }
    }
    function schedule() {
        clearTimeout(timer);
        if (active && !paused && !document.hidden) timer = setTimeout(advance, interval);
    }
    function controls() {
        document.body.classList.remove('controls-hidden'); clearTimeout(hideTimer);
        if (active && !paused && !busy) hideTimer = setTimeout(() => document.body.classList.add('controls-hidden'), 3500);
    }
    function fail(error) {
        active = false; clearTimeout(timer); clearTimeout(hideTimer); document.body.classList.remove('controls-hidden');
        el('slide').hidden = true; el('completed').hidden = true; el('status').hidden = false; el('status-text').textContent = message(error);
        el('start').hidden = false; el('start').disabled = false; el('start').textContent = 'Reintentar';
        el('pause').disabled = true; el('next').disabled = true;
        if (currentUrl) { URL.revokeObjectURL(currentUrl); currentUrl = null; }
        if (wakeLock) wakeLock.release().catch(() => {});
    }
    function complete() {
        active = false; clearTimeout(timer); clearTimeout(hideTimer);
        document.body.classList.remove('controls-hidden');
        el('slide').hidden = true; el('status').hidden = true; el('completed').hidden = false;
        el('pause').disabled = true; el('next').disabled = true;
        el('progress').textContent = 'Presentación finalizada';
        if (currentUrl) { URL.revokeObjectURL(currentUrl); currentUrl = null; }
        if (wakeLock) wakeLock.release().catch(() => {});
        el('completed-title').focus();
    }
    async function loadNext() {
        for (let attempt = 0; attempt < Math.min(deck.items.length, 12); attempt++) {
            const item = deck.next(repeat);
            if (!item) return { completed: true };
            const response = await fetch(item.imagen, { cache: 'no-store' });
            if (response.status === 401 || response.status === 403 || response.redirected) throw new Error('session');
            if (response.status === 404) continue; // Puede haberse ocultado desde que se abrió el catálogo.
            if (!response.ok || !response.headers.get('content-type')?.startsWith('image/')) throw new Error('image');
            const url = URL.createObjectURL(await response.blob());
            try { const image = new Image(); image.src = url; await image.decode(); return { item, url, position: deck.position, round: deck.round }; }
            catch { URL.revokeObjectURL(url); }
        }
        if (!repeat && deck.position >= deck.order.length) return { completed: true };
        throw new Error('image');
    }
    function prepare() { pending = loadNext().catch(error => ({ error })); }
    function display(slide) {
        if (slide.error) throw slide.error;
        if (slide.completed) { complete(); return; }
        const old = currentUrl; currentUrl = slide.url;
        el('stage-image').src = slide.url; el('stage-image').alt = slide.item.nombre;
        el('product-name').textContent = slide.item.nombre; el('reference').textContent = 'Referencia: ' + slide.item.referencia;
        el('category').textContent = slide.item.categoria; el('price').textContent = slide.item.precio;
        el('progress').textContent = `Vuelta ${slide.round} · ${slide.position} de ${deck.items.length} · ${repeat ? 'En bucle' : 'Una vuelta'} · Cada 5 segundos`;
        el('completed').hidden = true; el('status').hidden = true; el('slide').hidden = false;
        if (old) URL.revokeObjectURL(old);
    }
    async function advance() {
        if (busy || !active) return;
        busy = true; clearTimeout(timer); el('next').disabled = true;
        try { const slide = await pending; pending = null; display(slide); if (active) prepare(); }
        catch (error) { fail(error); }
        finally { busy = false; el('next').disabled = !active; schedule(); controls(); }
    }
    el('start').addEventListener('click', async () => {
        if (busy) return;
        busy = true; el('start').disabled = true; el('status-text').textContent = 'Preparando las piezas del catálogo…';
        try {
            // Liberar la única imagen precargada si se está reintentando una ejecución anterior.
            if (pending) { const previous = await pending; if (previous.url) URL.revokeObjectURL(previous.url); pending = null; }
            const response = await fetch(document.body.dataset.catalogUrl, { cache: 'no-store' });
            if (response.status === 401 || response.status === 403 || response.redirected) throw new Error('session');
            if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) throw new Error('catalog');
            deck = new CatalogDeck(await response.json());
            if (!deck.items.length) {
                el('status-text').textContent = 'No hay productos visibles con imagen en esta selección. Puedes elegir otra categoría o revisar el catálogo desde administración.';
                el('start').textContent = 'Volver a comprobar'; el('start').disabled = false; return;
            }
            paused = false; active = true; el('pause').textContent = 'Pausar'; el('pause').setAttribute('aria-pressed', 'false');
            await keepAwake(); display(await loadNext()); if (active) prepare();
            el('start').hidden = true; el('pause').disabled = !active; el('next').disabled = !active;
        } catch (error) { fail(error); }
        finally { busy = false; schedule(); controls(); }
    });
    async function restart(loop) {
        if (busy || active) return;
        busy = true; repeat = loop; active = true; paused = false;
        el('pause').textContent = 'Pausar'; el('pause').setAttribute('aria-pressed', 'false');
        try {
            await keepAwake(); display(await loadNext()); if (active) prepare();
            el('pause').disabled = !active; el('next').disabled = !active;
        } catch (error) { fail(error); }
        finally { busy = false; schedule(); controls(); }
    }
    el('repeat').addEventListener('click', () => {
        if (busy || active) return;
        // Conserva el último producto para evitar repetirlo al empezar la siguiente vuelta.
        deck.order = []; deck.position = 0; deck.round = 0;
        restart(false);
    });
    el('loop').addEventListener('click', () => restart(true));
    el('pause').addEventListener('click', () => {
        paused = !paused; el('pause').textContent = paused ? 'Continuar' : 'Pausar';
        el('pause').setAttribute('aria-pressed', String(paused)); schedule(); controls();
    });
    el('next').addEventListener('click', advance);
    el('fullscreen').addEventListener('click', async () => {
        try {
            if (document.fullscreenElement) await document.exitFullscreen();
            else if (document.documentElement.requestFullscreen) await document.documentElement.requestFullscreen();
            else el('fullscreen').textContent = 'Usa el modo pantalla completa del navegador';
        } catch { el('fullscreen').textContent = 'Pantalla completa no disponible'; }
        controls();
    });
    document.addEventListener('fullscreenchange', () => el('fullscreen').textContent = document.fullscreenElement ? 'Salir de pantalla completa' : 'Pantalla completa');
    document.addEventListener('visibilitychange', () => { schedule(); keepAwake(); });
    for (const event of ['pointermove', 'pointerdown', 'keydown']) document.addEventListener(event, controls, { passive: true });
    document.addEventListener('keydown', event => {
        if (event.target.closest('button, a, input')) return;
        if (event.code === 'Space' && active) { event.preventDefault(); el('pause').click(); }
        if (event.code === 'ArrowRight' && active) { event.preventDefault(); advance(); }
    });
    window.addEventListener('pagehide', () => {
        active = false; clearTimeout(timer); clearTimeout(hideTimer);
        if (currentUrl) URL.revokeObjectURL(currentUrl);
        if (pending) pending.then(slide => { if (slide.url) URL.revokeObjectURL(slide.url); });
        if (wakeLock) wakeLock.release().catch(() => {});
    });
})();
