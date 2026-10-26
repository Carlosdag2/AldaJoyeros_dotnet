(() => {
    const root = document.querySelector('[data-live-traffic]');
    if (!root) return;
    const find = name => root.querySelector(`[data-live-${name}]`);
    const formatTime = value => new Intl.DateTimeFormat('es-ES', { dateStyle: 'short', timeStyle: 'medium', timeZone: 'Europe/Madrid' }).format(new Date(value));
    let paused = false, timer, controller, stopped = false, failures = 0, lastPages = '';
    function status(text) {
        const badge = find('status'); badge.textContent = text;
        const colors = text === 'Conectado' ? 'bg-emerald-50 text-emerald-800' : text === 'Sin conexión' || text === 'Sesión caducada' ? 'bg-amber-50 text-amber-900' : 'bg-slate-100 text-slate-600';
        badge.className = 'rounded-full px-3 py-1 text-xs font-medium ' + colors;
    }
    function text(name, value) { find(name).textContent = String(value); }
    function render(data) {
        text('active', data.visitantesActivos); text('views', data.vistasUltimos30Minutos); text('visitors', data.visitantesUltimos30Minutos);
        text('time', formatTime(data.actualizadoEn)); text('start', formatTime(data.inicioMedicion));
        const signature = JSON.stringify(data.visitantesPorPagina);
        if (signature !== lastPages) {
            lastPages = signature;
            const list = find('pages'); list.replaceChildren();
            const entries = Object.entries(data.visitantesPorPagina);
            if (!entries.length) {
                const row = document.createElement('li'); row.className = 'rounded-xl border border-dashed border-slate-200 px-4 py-7 text-center text-sm text-slate-500';
                row.textContent = 'No hay visitantes activos en este momento.'; list.append(row);
            }
            for (const [label, count] of entries) {
                const row = document.createElement('li'); row.className = 'flex items-center justify-between gap-3 text-sm';
                const name = document.createElement('span'); name.className = 'text-slate-600'; name.textContent = label;
                const value = document.createElement('strong'); value.className = 'text-slate-900'; value.textContent = count;
                row.append(name, value); list.append(row);
            }
        }
        const chart = root.querySelector('[data-number-chart="live"]');
        const axisTime = date => new Intl.DateTimeFormat('es-ES', { timeZone: 'Europe/Madrid', hour: '2-digit', minute: '2-digit' }).format(new Date(date));
        window.aldaMetricCharts?.update(chart, data.historial.map(item => ({ Label: formatTime(item.fecha), AxisLabel: axisTime(item.fecha), Value: item.visitantes })));
        find('error').hidden = true; root.dataset.stale = 'false'; status('Conectado');
    }
    async function refresh() {
        clearTimeout(timer);
        if (paused || stopped || document.hidden || controller) return;
        const request = new AbortController(); controller = request;
        const timeout = setTimeout(() => request.abort(), 8000);
        try {
            const response = await fetch(root.dataset.endpoint, { cache: 'no-store', credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' }, signal: request.signal });
            if (response.redirected || response.status === 401 || response.status === 403) {
                stopped = true; find('refresh').disabled = true; find('pause').disabled = true; status('Sesión caducada'); find('login').hidden = false;
                throw new Error('Vuelve a iniciar sesión para consultar el tráfico. Se conservan los últimos datos recibidos.');
            }
            if (!response.ok || !response.headers.get('Content-Type')?.includes('application/json')) throw new Error('No se ha podido actualizar el tráfico. Se muestran los últimos datos recibidos.');
            render(await response.json()); failures = 0;
        } catch (error) {
            if (!paused && !document.hidden) {
                root.dataset.stale = 'true';
                if (!stopped) status('Sin conexión');
                find('error').hidden = false;
                find('error').textContent = error.name === 'AbortError' ? 'La actualización ha tardado demasiado. Se conservan los últimos datos y volveremos a intentarlo.' : error instanceof TypeError ? 'No hay conexión con el servidor. Se conservan los últimos datos y se volverá a intentar automáticamente.' : error.message;
                failures++;
            }
        } finally {
            clearTimeout(timeout); controller = null;
            if (!paused && !stopped && !document.hidden) timer = setTimeout(refresh, Math.min(30000, 5000 * Math.max(1, failures)));
        }
    }
    find('pause').addEventListener('click', () => {
        paused = !paused; find('pause').textContent = paused ? 'Reanudar' : 'Pausar'; find('pause').setAttribute('aria-pressed', String(paused));
        if (paused) { clearTimeout(timer); controller?.abort(); status('En pausa'); } else refresh();
    });
    find('refresh').addEventListener('click', () => { if (!paused && !stopped) refresh(); });
    document.addEventListener('visibilitychange', () => {
        if (document.hidden) { clearTimeout(timer); controller?.abort(); if (!paused && !stopped) status('En espera'); }
        else if (!paused && !stopped) refresh();
    });
    window.addEventListener('online', refresh);
    window.addEventListener('pagehide', () => { clearTimeout(timer); controller?.abort(); });
    refresh();
})();
