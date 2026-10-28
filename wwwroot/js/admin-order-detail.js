(() => {
    'use strict';
    const button = document.querySelector('[data-order-state-editor]'); if (!button) return;
    button.addEventListener('click',event=>{
        event.preventDefault();
        window.AldaAdminDialog.open({url:button.dataset.editorUrl,title:'Cambiar estado del pedido',onSaved:async()=>{
            const html = await window.AldaAdminDialog.request(button.dataset.stateUrl,{},false);
            const parsed = new DOMParser().parseFromString(html,'text/html');
            for (const name of ['badge','delivery','tracking']) {
                const updated=parsed.querySelector(`[data-order-${name}]`);
                if (!updated) throw new Error('No se pudo actualizar el seguimiento.');
                document.querySelector(`[data-order-${name}]`).replaceChildren(...updated.childNodes);
            }
        }});
    });
    document.querySelector('[data-admin-dialog]').addEventListener('change',event=>{
        if(event.target.name !== 'Estado')return;
        const field=event.target.form.querySelector('[data-delivery-field]');
        field.hidden=event.target.value!=='ENTREGADO';field.querySelector('input').disabled=field.hidden;
    });
    if(new URL(location.href).searchParams.get('cambiarEstado')==='true')button.click();
})();
