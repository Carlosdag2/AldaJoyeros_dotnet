(() => {
    const paso = 48;
    window.aldaPaginacion = {
        elegir(event, nav, navegar) {
            const boton = event.target.closest('button[data-page]');
            if (!boton || boton.disabled || nav.dataset.cargando === 'true') return;
            if (nav._arrastreHasta > Date.now()) { event.preventDefault(); return; }
            const pagina = Number(boton.dataset.page);
            if (!Number.isInteger(pagina) || pagina < 1 || pagina > Number(nav.dataset.totalPages) || pagina === Number(nav.dataset.currentPage)) return;
            nav.dataset.cargando = 'true';
            Promise.resolve().then(() => navegar(pagina))
                .catch(error => console.error('No se pudo cambiar de página.', error))
                .finally(() => { nav.dataset.cargando = 'false'; });
        }
    };

    function preparar(nav) {
        if (nav.dataset.preparado) return;
        nav.dataset.preparado = 'true';
        const viewport = nav.querySelector('[data-page-viewport]');
        const rail = nav.querySelector('[data-page-rail]');
        const total = Number(nav.dataset.totalPages);
        const actual = Number(nav.dataset.currentPage);
        let rango = '';
        // Keep a small set of buttons in the DOM even when the catalog has thousands of pages.
        function dibujar() {
            const inicio = Math.max(1, Math.floor(viewport.scrollLeft / paso) - 2);
            const fin = Math.min(total, Math.ceil((viewport.scrollLeft + viewport.clientWidth) / paso) + 3);
            const nuevoRango = inicio + ':' + fin;
            if (nuevoRango === rango) return;
            rango = nuevoRango;
            const fragmento = document.createDocumentFragment();
            for (let pagina = inicio; pagina <= fin; pagina++) {
                const boton = document.createElement('button');
                boton.type = 'button'; boton.dataset.page = pagina; boton.textContent = pagina;
                boton.setAttribute('aria-label', 'Página ' + pagina);
                if (pagina === actual) boton.setAttribute('aria-current', 'page');
                boton.className = 'w-10 h-10 flex items-center justify-center transition-colors ' + (pagina === actual ? nav.dataset.activeClass : nav.dataset.pageClass);
                Object.assign(boton.style, { position: 'absolute', left: ((pagina - 1) * paso) + 'px', top: '0' });
                fragmento.append(boton);
            }
            rail.replaceChildren(fragmento);
        }
        viewport.scrollLeft = Math.max(0, (actual - 1) * paso - (viewport.clientWidth - 40) / 2);
        viewport.addEventListener('scroll', dibujar, { passive: true });
        viewport.addEventListener('keydown', event => {
            if (event.target !== viewport) return;
            if (event.key === 'Home' || event.key === 'End') {
                event.preventDefault(); viewport.scrollLeft = event.key === 'Home' ? 0 : rail.scrollWidth;
            }
        });
        let arrastre;
        viewport.addEventListener('pointerdown', event => {
            if (event.pointerType !== 'mouse' || event.button !== 0) return;
            arrastre = { x: event.clientX, scroll: viewport.scrollLeft, movido: false };
        });
        viewport.addEventListener('pointermove', event => {
            if (!arrastre) return;
            const distancia = event.clientX - arrastre.x;
            if (Math.abs(distancia) > 5) arrastre.movido = true;
            if (arrastre.movido) {
                viewport.setPointerCapture(event.pointerId);
                viewport.scrollLeft = arrastre.scroll - distancia;
                nav._arrastreHasta = Date.now() + 300;
                event.preventDefault();
            }
        });
        const terminar = () => { if (arrastre?.movido) nav._arrastreHasta = Date.now() + 300; arrastre = null; };
        viewport.addEventListener('pointerup', terminar);
        viewport.addEventListener('pointercancel', terminar);
        viewport.addEventListener('pointerleave', event => { if (!viewport.hasPointerCapture(event.pointerId)) terminar(); });
        if (window.ResizeObserver) new ResizeObserver(dibujar).observe(viewport);
        dibujar();
    }
    let pendiente = false;
    function actualizar() {
        if (pendiente) return;
        pendiente = true;
        requestAnimationFrame(() => { pendiente = false; document.querySelectorAll('[data-scroll-pagination]').forEach(preparar); });
    }
    function iniciar() { actualizar(); new MutationObserver(actualizar).observe(document.body, { childList: true, subtree: true }); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar); else iniciar();
})();
