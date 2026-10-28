(() => {
    'use strict';
    const root = document.querySelector('[data-product-workspace]'); if (!root) return;
    const form = root.querySelector('[data-product-search]'), results = root.querySelector('[data-product-results]');
    const status = root.querySelector('[data-product-status]');
    let filter = new URL(location.href).searchParams.get('filtro') || 'activos';
    let page = Number(results.querySelector('[data-current-page]').dataset.currentPage);
    let request, sequence = 0, debounce, applied = location.href;
    function address() {
        const url = new URL(form.action);
        for (const [key,value] of new FormData(form)) if (value && key !== 'filtro') url.searchParams.set(key,value);
        url.searchParams.set('filtro',filter); url.searchParams.set('page',page); return url;
    }
    function restore(url) {
        const params = new URL(url).searchParams;
        filter = params.get('filtro') || 'activos'; page = Number(params.get('page')) || 1;
        for (const name of ['busqueda','categoriaId','orden','tamano']) form.elements[name].value = params.get(name) || ({orden:'recientes',tamano:'20'}[name] || '');
        tabs();
    }
    function tabs() { root.querySelectorAll('[data-product-filter]').forEach(e=>e.setAttribute('aria-pressed',String(e.dataset.productFilter === filter))); }
    async function load(historyMode = 'push') {
        clearTimeout(debounce); request?.abort(); request = new AbortController(); const current = ++sequence;
        results.setAttribute('aria-busy','true'); status.textContent='Actualizando productos…';
        try {
            const response = await fetch(address(), {signal:request.signal, headers:{'X-Requested-With':'XMLHttpRequest'}});
            if (!response.ok || response.redirected) throw new Error();
            const html = await response.text(); if (current !== sequence) return;
            const parsed = new DOMParser().parseFromString(html,'text/html');
            if (!parsed.querySelector('[data-product-list]')) throw new Error();
            results.innerHTML = html;
            const list = results.querySelector('[data-product-list]'); page=Number(list.dataset.currentPage);
            status.textContent=`${list.dataset.total} productos encontrados`;
            for (const key of ['activos','eliminados']) {
                const value = response.headers.get(key === 'activos' ? 'X-Stats-Activos' : 'X-Stats-Eliminados');
                if (value !== null) root.querySelector(`[data-count="${key}"]`).textContent=value;
            }
            tabs(); if (historyMode !== 'none') history[historyMode === 'replace' ? 'replaceState' : 'pushState'](null,'',address());
            applied=address().href;
        } catch (error) {
            if (error.name === 'AbortError') return;
            if (current === sequence) { restore(applied); status.textContent='No se pudieron cargar los productos. Inténtalo de nuevo.'; }
            throw error;
        } finally { if (current === sequence) results.setAttribute('aria-busy','false'); }
    }
    const run = (mode) => load(mode).catch(()=>window.AldaToast.error('No se pudieron actualizar los productos.'));
    const refresh = () => load('replace');
    window.adminProductos={cargarPagina(number){page=number;return run('push');}};
    form.addEventListener('submit',event=>{event.preventDefault(); if(event.submitter?.name==='filtro')filter=event.submitter.value;page=1;run('push');});
    form.elements.busqueda.addEventListener('input',()=>{clearTimeout(debounce);debounce=setTimeout(()=>{page=1;run('replace');},350);});
    form.addEventListener('change',event=>{if(event.target.tagName==='SELECT'){page=1;run('push');}});
    root.addEventListener('click',event=>{
        const clear=event.target.closest('[data-product-clear]'); if(clear){event.preventDefault();restore(form.action);page=1;run('push');return;}
        const editor=event.target.closest('[data-product-editor]');
        if(editor){event.preventDefault();const id=editor.dataset.productEditor;window.AldaAdminDialog.open({url:`/AdminProductos/Editor?id=${id}`,title:id==='0'?'Nuevo producto':'Editar producto',onSaved:refresh});return;}
        const action=event.target.closest('[data-product-action]'); if(!action)return;
        const row=action.closest('[data-product-id]'), kind=action.dataset.productAction;
        const config={eliminar:['Mover a la papelera','EliminarAjax','Podrás restaurarlo cuando lo necesites.'],restaurar:['Restaurar producto','RestaurarAjax','Volverá a estar activo en el catálogo.'],permanente:['Eliminar definitivamente','EliminarPermanenteAjax','Esta acción no se puede deshacer.']}[kind];
        window.AldaAdminDialog.confirm({title:config[0],message:`${row.dataset.name}. ${config[2]}`,url:`/AdminProductos/${config[1]}`,id:row.dataset.productId,permanent:kind==='permanente',onSaved:refresh});
    });
    window.addEventListener('popstate',()=>{restore(location.href);run('none');}); tabs();
})();
