(() => {
    const root = document.querySelector('[data-featured-product]');
    if (!root || !window.AldaFeatured) return;
    const button = root.querySelector('[data-featured-product-toggle]'), status = root.querySelector('[data-featured-product-status]');
    let selected = null, known = false, busy = false;
    async function state(message = '') {
        known = false; button.disabled = true;
        try {
            const data = await window.AldaFeatured.selection();
            selected = data.items.find(item => String(item.productId) === root.dataset.productId);
            known = true; button.textContent = selected ? 'Quitar de destacados' : 'Añadir a destacados';
            status.textContent = message || (selected ? 'Este producto ya aparece en los destacados del inicio.' : 'Puedes destacarlo directamente desde aquí.');
        } catch (error) { button.textContent = 'Actualizar selección'; status.textContent = error.message; }
        finally { button.disabled = false; }
    }
    button.addEventListener('click', async () => {
        if (busy) return;
        busy = true; button.disabled = true;
        try {
            if (!known) { await state(); return; }
            const result = await window.AldaFeatured.change(selected?.id || root.dataset.productId, selected ? 'quitar' : 'añadir');
            if (result?.success) await state(result.message);
        } catch (error) { known = false; button.textContent = 'Actualizar selección'; status.textContent = error.message; }
        finally { busy = false; button.disabled = false; }
    });
    state();
})();
