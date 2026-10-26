(() => {
    const meta = document.querySelector('meta[name="traffic-pulse-url"]');
    if (!meta) return;
    const endpoint = new URL(meta.content, document.baseURI).href;
    const initialPath = location.pathname;
    const bytes = crypto.getRandomValues(new Uint8Array(16));
    const tab = Array.from(bytes, value => value.toString(16).padStart(2, '0')).join('');
    const token = document.querySelector('meta[name="cart-csrf"]')?.content;
    if (!token) return;
    let enabled = meta.dataset.analytics !== 'false';
    let timer, pending = false, closed = false, sequence = 0, resume = false;
    const data = closing => new URLSearchParams({ pestana: tab, pagina: location.pathname.startsWith('/') ? location.pathname : initialPath,
        visible: String(!document.hidden), cerrar: String(closing), secuencia: String(++sequence), __RequestVerificationToken: token });
    function leave() {
        clearTimeout(timer);
        if (!enabled || closed) return;
        closed = true;
        const body = data(true);
        if (!navigator.sendBeacon?.(endpoint, body)) fetch(endpoint, { method: 'POST', body, keepalive: true }).catch(() => {});
    }
    async function pulse() {
        clearTimeout(timer);
        if (!enabled) return;
        if (pending) { resume = true; return; }
        closed = false;
        pending = true;
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 8000);
        try { await fetch(endpoint, { method: 'POST', body: data(false), cache: 'no-store', credentials: 'same-origin', signal: controller.signal }); }
        catch { /* Absence expires on the server even if the final signal is lost. */ }
        finally { clearTimeout(timeout); pending = false; if (enabled && !closed) { const delay = resume ? 0 : (document.hidden ? 40000 : 20000); resume = false; timer = setTimeout(pulse, delay); } }
    }
    window.addEventListener('alda:consent-changed', event => {
        enabled = event.detail.analytics === true && event.detail.trafficEnabled === true;
        meta.dataset.analytics = String(enabled);
        if (enabled) pulse();
        else { closed = true; resume = false; clearTimeout(timer); }
    });
    document.addEventListener('visibilitychange', pulse);
    window.addEventListener('pagehide', leave);
    window.addEventListener('beforeunload', leave);
    window.addEventListener('pageshow', event => { if (event.persisted) pulse(); });
    window.addEventListener('online', pulse);
    window.addEventListener('popstate', pulse);
    pulse();
})();
