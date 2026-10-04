(() => {
    'use strict';
    const root = document.querySelector('[data-admin-list]');
    if (!root) return;
    const form = root.querySelector('form[data-admin-search]');
    const search = form.elements.busqueda;
    const status = root.querySelector('[data-admin-status]');
    const kind = root.dataset.adminList;
    if (kind === 'categorias') {
        const group = form.elements.grupo;
        const normalize = value => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('es');
        const filter = () => {
            let count = 0;
            root.querySelectorAll('[data-category-group]').forEach(section => {
                let visible = 0;
                section.querySelectorAll('[data-categoria-id]').forEach(row => {
                    row.hidden = (group.value && section.dataset.categoryGroup !== group.value) || !normalize(row.dataset.nombre).includes(normalize(search.value.trim()));
                    if (!row.hidden) visible++;
                });
                section.hidden = visible === 0;
                if (search.value || group.value) section.open = true;
                section.querySelector('[data-group-count]').textContent = `${visible} categorías`;
                count += visible;
            });
            status.textContent = `${count} categorías encontradas`;
            root.querySelector('[data-no-results]').hidden = count > 0;
        };
        form.addEventListener('submit', event => { event.preventDefault(); filter(); });
        search.addEventListener('input', filter);
        group.addEventListener('change', filter);
        root.querySelector('[data-clear-search]').addEventListener('click', () => { form.reset(); filter(); search.focus(); });
        root.addEventListener('click', event => {
            const button = event.target.closest('[data-delete-category]');
            if (button) window.adminCategorias.eliminar(Number(button.dataset.deleteCategory), button.dataset.nombre, Number(button.dataset.productos));
        });
        filter();
        return;
    }
    const users = kind === 'usuarios';
    const key = users ? 'rol' : 'estado';
    const objectName = users ? 'adminUsuarios' : 'adminPedidos';
    const container = root.querySelector('[data-admin-results]');
    let filterValue = new URL(location.href).searchParams.get(key) || '';
    let page = Number(container.querySelector('[data-current-page]')?.dataset.currentPage) || 1;
    let busy = false;
    const setTabs = () => {
        form.elements[key].value = filterValue;
        root.querySelectorAll('[data-filter]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.filter === filterValue)));
    };
    const url = () => {
        const params = new URLSearchParams();
        if (filterValue) params.set(key, filterValue);
        if (search.value.trim()) params.set('busqueda', search.value.trim());
        if (page > 1) params.set('page', page);
        return `${form.action}${params.size ? '?' + params : ''}`;
    };
    let appliedUrl = location.href;
    async function load(push = true) {
        if (busy) return;
        busy = true;
        container.setAttribute('aria-busy', 'true');
        container.style.opacity = '.5';
        status.textContent = 'Actualizando resultados…';
        try {
            const response = await fetch(url(), { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok || response.redirected) throw new Error('No se pudieron cargar los resultados');
            const html = await response.text();
            container.innerHTML = html;
            page = Number(container.querySelector('[data-current-page]')?.dataset.currentPage) || 1;
            const total = response.headers.get('X-Stats-Total');
            if (total !== null) root.querySelector('#statTotal').textContent = total;
            status.textContent = `${total || 0} resultados encontrados`;
            setTabs();
            if (push) history.pushState(null, '', url());
            appliedUrl = url();
        } catch (error) {
            restore(appliedUrl);
            status.textContent = 'No se pudieron actualizar los resultados. Inténtalo de nuevo.';
            AldaToast.error(status.textContent);
        } finally {
            busy = false;
            container.style.opacity = '';
            container.setAttribute('aria-busy', 'false');
        }
    }
    function restore(address) {
        const params = new URL(address).searchParams;
        search.value = params.get('busqueda') || '';
        filterValue = params.get(key) || '';
        page = Number(params.get('page')) || 1;
        setTabs();
    }
    window[objectName] = {
        async cargarPagina(number) { if (busy) return; page = number; await load(); },
        async eliminar(id, email) {
            const accepted = await AldaConfirm.show({ title: users ? '¿Eliminar usuario?' : '¿Eliminar pedido?', message: users ? `El usuario "${email}" será eliminado permanentemente.` : `El pedido #${id} será eliminado permanentemente.`, type: 'danger', confirmText: 'Eliminar' });
            if (!accepted) return;
            if (!users) {
                const deleteForm = document.createElement('form');
                deleteForm.method = 'POST'; deleteForm.action = `/AdminPedidos/Eliminar/${id}`;
                document.body.appendChild(deleteForm); deleteForm.submit(); return;
            }
            try {
                const response = await fetch('/AdminUsuarios/EliminarAjax', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: new URLSearchParams({ id }) });
                if (!response.ok) throw new Error();
                const data = await response.json();
                if (data.success) { AldaToast.success(data.message); location.reload(); }
                else AldaToast.error(data.message);
            } catch { AldaToast.error('No se pudo eliminar el usuario'); }
        }
    };
    form.addEventListener('submit', event => { event.preventDefault(); if (busy) return; page = 1; load(); });
    root.querySelector('[data-clear-search]').addEventListener('click', () => { if (busy) return; search.value = ''; filterValue = ''; page = 1; load(); });
    root.addEventListener('click', event => {
        const tab = event.target.closest('[data-filter]');
        if (tab && !busy) { filterValue = tab.dataset.filter; page = 1; load(); }
        const button = event.target.closest('[data-delete-record]');
        if (button && !busy) window[objectName].eliminar(Number(button.dataset.deleteRecord), button.dataset.email);
    });
    window.addEventListener('popstate', () => { if (!busy) { restore(location.href); load(false); } });
    setTabs();
})();
