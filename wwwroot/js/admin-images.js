(() => {
    'use strict';
    const root=document.querySelector('[data-image-manager]'); if(!root)return;
    const upload=root.querySelector('[data-image-upload]'), input=upload.querySelector('input[type=file]');
    const queueList=root.querySelector('[data-image-queue]'), uploadStatus=root.querySelector('[data-upload-status]');
    const gallery=root.querySelector('[data-image-gallery]'), status=root.querySelector('[data-gallery-status]');
    const viewer=document.querySelector('[data-image-viewer]');
    const queue=[];let busy=false, snapshot=[], dragged, viewerOpener;
    const cards=()=>Array.from(gallery.querySelectorAll('[data-image-id]'));
    const ids=()=>cards().map(card=>card.dataset.imageId);
    const dirty=()=>JSON.stringify(ids())!==JSON.stringify(snapshot);
    const pending=()=>queue.filter(item=>item.state!=='done');
    function controls(){
        root.setAttribute('aria-busy',String(busy));
        input.disabled=busy||dirty();
        root.querySelector('[data-upload-submit]').disabled=busy||dirty()||pending().length===0;
        root.querySelector('[data-upload-submit]').textContent=busy?'Trabajando…':`Subir imágenes${pending().length?' ('+pending().length+')':''}`;
        root.querySelector('[data-queue-clear]').disabled=busy||!queue.length;
        root.querySelector('[data-gallery-refresh]').disabled=busy||dirty();
        root.querySelector('[data-order-toolbar]').hidden=!dirty();
        root.querySelector('[data-order-save]').disabled=busy;root.querySelector('[data-order-discard]').disabled=busy;
        queueList.querySelectorAll('button').forEach(button=>button.disabled=busy);
        cards().forEach((card,index,all)=>{
            card.querySelector('[data-image-position]').textContent=index+1;
            card.querySelector('[data-image-principal]').disabled=busy||dirty()||card.dataset.principal==='true';
            card.querySelector('[data-image-delete]').disabled=busy||dirty();
            card.querySelector('[data-image-move="-1"]').disabled=busy||index===0;
            card.querySelector('[data-image-move="1"]').disabled=busy||index===all.length-1;
            const handle=card.querySelector('[data-image-drag]');handle.disabled=busy;handle.draggable=!busy;
        });
    }
    function renderQueue(){
        queueList.replaceChildren();
        queue.forEach(item=>{
            const row=document.createElement('li');row.className='flex min-w-0 items-start gap-3 rounded-xl border border-slate-200 bg-white p-3';
            const image=document.createElement('img');image.src=item.url;image.alt='';image.className='h-16 w-16 shrink-0 rounded-lg bg-white object-contain';
            const body=document.createElement('div');body.className='min-w-0 flex-1';
            const name=document.createElement('p');name.className='break-words text-sm font-semibold text-slate-800';name.textContent=item.file.name;
            const text=document.createElement('p');text.className='mt-1 break-words text-xs '+(item.state==='error'?'text-red-700':'text-slate-500');text.textContent=item.message;
            const button=document.createElement('button');button.type='button';button.className='mt-1 min-h-11 text-xs font-semibold text-slate-600 underline';button.textContent='Quitar de la selección';
            button.addEventListener('click',()=>{if(busy)return;URL.revokeObjectURL(item.url);queue.splice(queue.indexOf(item),1);renderQueue();});
            body.append(name,text,button);row.append(image,body);queueList.append(row);
        });controls();
    }
    function add(files){
        if(busy||dirty())return;
        const notices=[];
        for(const file of files){
            if(!/\.(jpe?g|png|webp|gif)$/i.test(file.name)||file.size===0||file.size>5*1024*1024){notices.push(`${file.name}: usa JPG, PNG, WEBP o GIF de hasta 5 MB.`);continue;}
            if(queue.some(item=>item.file.name===file.name&&item.file.size===file.size&&item.file.lastModified===file.lastModified)){notices.push(`${file.name}: ya está en la selección.`);continue;}
            if(queue.length>=10){notices.push('Máximo 10 imágenes por tanda. Limpia la selección para añadir más.');break;}
            queue.push({file,url:URL.createObjectURL(file),state:'pending',message:`${Math.ceil(file.size/1024)} KB · Pendiente de subir`});
        }
        input.value='';renderQueue();uploadStatus.textContent=notices.join(' ')||`${pending().length} imágenes listas para subir.`;
    }
    async function loadGallery(){
        const html=await window.AldaAdminDialog.request(root.dataset.galleryUrl,{},false);
        if(!new DOMParser().parseFromString(html,'text/html').querySelector('[data-gallery-content]'))throw new Error('No se pudo actualizar la galería.');
        gallery.innerHTML=html;snapshot=ids();controls();status.textContent=`${cards().length} imágenes en la galería.`;
    }
    function body(fields={}){
        const form=new FormData();form.set('productoId',root.dataset.productId);form.set('__RequestVerificationToken',document.querySelector('meta[name=cart-csrf]').content);
        for(const [name,value] of Object.entries(fields))form.set(name,value);return form;
    }
    async function mutation(url,form,focusId){
        if(busy)return;busy=true;controls();status.textContent='Guardando…';
        try{
            const result=await window.AldaAdminDialog.request(url,{method:'POST',body:form});
            try{await loadGallery();}catch{window.AldaToast.warning('El cambio se guardó. Actualiza la galería para ver el resultado.');}
            window.AldaToast.success(result.message);
            if(focusId)gallery.querySelector(`[data-image-id="${CSS.escape(focusId)}"] [data-image-preview]`)?.focus({preventScroll:true});
        }catch(error){status.textContent=error.message;window.AldaToast.error(error.message);}
        finally{busy=false;controls();}
    }
    input.addEventListener('change',()=>add(input.files));
    const drop=root.querySelector('[data-image-drop]');
    ['dragenter','dragover','dragleave','drop'].forEach(name=>drop.addEventListener(name,event=>{event.preventDefault();event.stopPropagation();drop.classList.toggle('is-over',name==='dragenter'||name==='dragover');if(name==='drop')add(event.dataTransfer.files);}));
    root.querySelector('[data-queue-clear]').addEventListener('click',()=>{if(busy)return;queue.forEach(item=>URL.revokeObjectURL(item.url));queue.length=0;renderQueue();uploadStatus.textContent='Selecciona las imágenes que quieras añadir.';});
    upload.addEventListener('submit',async event=>{
        event.preventDefault();if(busy||dirty()||!pending().length)return;
        busy=true;controls();const batch=pending();let completed=0;
        for(const [index,item] of batch.entries()){
            item.message='Subiendo…';renderQueue();uploadStatus.textContent=`Subiendo imagen ${index+1} de ${batch.length}…`;
            const form=body();form.set('archivo',item.file,item.file.name);
            try{const result=await window.AldaAdminDialog.request(upload.action,{method:'POST',body:form});item.state='done';item.message=result.message;completed++;}
            catch(error){item.state='error';item.message=error.message;}
            renderQueue();
        }
        try{await loadGallery();}catch{window.AldaToast.warning('No se pudo actualizar la galería. Pulsa Actualizar galería para comprobar las imágenes subidas.');}
        finally{busy=false;renderQueue();}
        uploadStatus.textContent=`${completed} de ${batch.length} imágenes completadas.`+(completed<batch.length?' Revisa los errores antes de reintentar las pendientes.':' Puedes limpiar la selección para añadir más.');
        if(completed)window.AldaToast.success(`${completed} imágenes procesadas. Las que ya existían no se han duplicado.`);
    });
    root.querySelector('[data-gallery-refresh]').addEventListener('click',async()=>{
        if(busy||dirty())return;busy=true;controls();try{await loadGallery();}catch(error){status.textContent=error.message;}finally{busy=false;controls();}
    });
    root.querySelector('[data-order-discard]').addEventListener('click',()=>{
        if(busy)return;const grid=gallery.querySelector('[data-image-grid]');snapshot.forEach(id=>grid.append(cards().find(card=>card.dataset.imageId===id)));controls();status.textContent='Cambios de orden descartados.';
    });
    root.querySelector('[data-order-save]').addEventListener('click',()=>{
        const form=body();ids().forEach(id=>form.append('ids',id));snapshot.forEach(id=>form.append('anterior',id));mutation(root.dataset.orderUrl,form);
    });
    gallery.addEventListener('click',event=>{
        const card=event.target.closest('[data-image-id]');if(!card)return;
        if(event.target.closest('[data-image-preview]')){
            viewerOpener=event.target.closest('button');viewer.querySelector('[data-viewer-image]').src=card.querySelector('img').src;
            viewer.querySelector('[data-viewer-image]').alt=card.dataset.imageName;viewer.querySelector('[data-viewer-caption]').textContent=card.dataset.imageName;viewer.showModal();return;
        }
        if(busy)return;
        const move=event.target.closest('[data-image-move]');
        if(move){const all=cards(),index=all.indexOf(card),target=all[index+Number(move.dataset.imageMove)];if(target){card.parentElement.insertBefore(card,Number(move.dataset.imageMove)<0?target:target.nextSibling);controls();move.focus({preventScroll:true});status.textContent=`Imagen movida a la posición ${cards().indexOf(card)+1}. Guarda el orden para aplicarlo.`;}return;}
        if(dirty())return;
        if(event.target.closest('[data-image-principal]'))mutation(root.dataset.principalUrl,body({id:card.dataset.imageId}),card.dataset.imageId);
        if(event.target.closest('[data-image-delete]'))window.AldaAdminDialog.confirm({title:'Eliminar imagen',message:`${card.dataset.imageName}. Se eliminará definitivamente. Si es la principal, se elegirá otra de las restantes.`,url:root.dataset.deleteUrl,id:card.dataset.imageId,fields:{productoId:root.dataset.productId},permanent:true,onSaved:loadGallery});
    });
    gallery.addEventListener('dragstart',event=>{if(busy||!event.target.closest('[data-image-drag]')){event.preventDefault();return;}dragged=event.target.closest('[data-image-id]');event.dataTransfer.effectAllowed='move';event.dataTransfer.setData('text/plain',dragged.dataset.imageId);dragged.classList.add('is-dragging');});
    gallery.addEventListener('dragover',event=>{const card=event.target.closest('[data-image-id]');if(dragged&&card&&card!==dragged){event.preventDefault();card.classList.add('is-drop-target');}});
    gallery.addEventListener('dragleave',event=>event.target.closest('[data-image-id]')?.classList.remove('is-drop-target'));
    gallery.addEventListener('drop',event=>{const target=event.target.closest('[data-image-id]');if(!dragged||!target||dragged===target)return;event.preventDefault();const all=cards();target.parentElement.insertBefore(dragged,all.indexOf(dragged)<all.indexOf(target)?target.nextSibling:target);controls();status.textContent='Orden modificado. Pulsa Guardar orden para aplicarlo.';});
    gallery.addEventListener('dragend',()=>{cards().forEach(card=>card.classList.remove('is-dragging','is-drop-target'));dragged=null;});
    viewer.querySelector('[data-viewer-close]').addEventListener('click',()=>viewer.close());
    viewer.addEventListener('close',()=>{viewer.querySelector('img').removeAttribute('src');if(viewerOpener?.isConnected)viewerOpener.focus({preventScroll:true});});
    window.addEventListener('pagehide',event=>{if(!event.persisted)queue.forEach(item=>URL.revokeObjectURL(item.url));});
    snapshot=ids();controls();
})();
