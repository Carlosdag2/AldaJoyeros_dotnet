(() => {
 const panel=document.querySelector('[data-duward-panel]');if(!panel)return;
 const input=panel.querySelector('#duwardFiles'),list=panel.querySelector('[data-file-list]');
 input?.addEventListener('change',()=>{list.replaceChildren(...[...input.files].map(file=>{const li=document.createElement('li');li.textContent=file.name+' · '+Math.ceil(file.size/1024)+' KB';return li;}));});
 const rows=[...panel.querySelectorAll('[data-job-id]')].filter(r=>r.dataset.active==='true');if(!rows.length)return;
 const status=panel.querySelector('[data-duward-status]');let stopped=false;
 async function poll(){try{
  const response=await fetch(panel.dataset.statusUrl,{headers:{'X-Requested-With':'XMLHttpRequest'},cache:'no-store'});if(!response.ok)throw Error('Estado no disponible');
  const jobs=await response.json();let active=false;
  for(const row of rows){const job=jobs.find(x=>x.id===row.dataset.jobId);if(!job)continue;row.querySelector('[data-job-label]').textContent=job.label;row.querySelector('[data-job-progress]').textContent=job.progress;active=active||job.active;if(job.active)status.textContent=job.progress;}
  if(!active){stopped=true;status.textContent='Proceso terminado. Cargando los informes…';location.reload();}
 }catch{status.textContent='No se ha podido actualizar el estado. Puedes recargar esta página para consultar el resultado.';}
 if(!stopped)setTimeout(poll,3000);
 }
 setTimeout(poll,2000);
})();
