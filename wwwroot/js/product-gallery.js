(() => {
    const initialized = new WeakSet();
    function initialize(gallery) {
        if (initialized.has(gallery)) return;
        initialized.add(gallery);
        if (Number(gallery.dataset.imageCount) < 2) return;
        let gesture = null;
        let ignoreClickUntil = 0;
        let photos = null;
        if (gallery.hasAttribute('data-gallery-images')) {
            try { photos = JSON.parse(gallery.dataset.galleryImages); } catch { return; }
            if (!Array.isArray(photos) || photos.length < 2) return;
        }
        let active = 0;
        const step = direction => {
            if (!photos) { gallery.dispatchEvent(new CustomEvent('alda:image-step', { detail: direction })); return; }
            const image = gallery.querySelector('[data-gallery-image]');
            if (!image) return;
            active = (active + direction + photos.length) % photos.length;
            const fallback = gallery.querySelector('[data-gallery-error]');
            image.hidden = false;
            if (fallback) fallback.hidden = true;
            image.src = photos[active];
            const counter = gallery.querySelector('[data-gallery-counter]');
            if (counter) counter.textContent = `${active + 1} / ${photos.length}`;
        };
        gallery.addEventListener('pointerdown', event => {
            // Ignore controls and cancel the gesture when another finger starts a pinch.
            if (!event.isPrimary) { gesture = null; return; }
            const control = event.target.closest('button, a, input, select');
            if (event.button !== 0 || (control && control !== gallery)) return;
            gesture = { id: event.pointerId, x: event.clientX, y: event.clientY };
            gallery.setPointerCapture(event.pointerId);
        });
        gallery.addEventListener('pointermove', event => {
            if (!gesture || gesture.id !== event.pointerId) return;
            const dx = event.clientX - gesture.x;
            const dy = event.clientY - gesture.y;
            if (Math.abs(dy) > 12 && Math.abs(dy) > Math.abs(dx)) gesture = null;
        });
        gallery.addEventListener('pointerup', event => {
            if (!gesture || gesture.id !== event.pointerId) return;
            const dx = event.clientX - gesture.x;
            const dy = event.clientY - gesture.y;
            gesture = null;
            const threshold = Math.max(35, Math.min(70, gallery.clientWidth * 0.12));
            if (Math.abs(dx) < threshold || Math.abs(dx) <= Math.abs(dy) * 1.4) return;
            ignoreClickUntil = Date.now() + 400;
            step(dx < 0 ? 1 : -1);
        });
        const cancel = () => { gesture = null; };
        gallery.addEventListener('pointercancel', cancel);
        gallery.addEventListener('lostpointercapture', cancel);
        gallery.addEventListener('dragstart', event => event.preventDefault());
        gallery.addEventListener('click', event => {
            if (Date.now() < ignoreClickUntil) { event.preventDefault(); event.stopPropagation(); }
        }, true);
        gallery.addEventListener('keydown', event => {
            if (event.target !== gallery || !['ArrowLeft', 'ArrowRight'].includes(event.key)) return;
            event.preventDefault(); step(event.key === 'ArrowLeft' ? -1 : 1);
        });
    }
    let pending = false;
    function refresh() {
        if (pending) return;
        pending = true;
        requestAnimationFrame(() => {
            pending = false;
            document.querySelectorAll('[data-product-gallery]').forEach(initialize);
        });
    }
    refresh();
    new MutationObserver(refresh).observe(document.body, { childList: true, subtree: true });
})();
