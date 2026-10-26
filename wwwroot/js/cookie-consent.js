(() => {
    let saving = false;
    function dismiss(notice) {
        const containedFocus = notice.contains(document.activeElement);
        notice.style.pointerEvents = 'none';
        notice.style.opacity = '0'; notice.style.transform = 'translateY(1rem)';
        const remove = () => {
            notice.remove();
            if (containedFocus) {
                const main = document.querySelector('main');
                if (main) {
                    const hadIndex = main.hasAttribute('tabindex');
                    if (!hadIndex) main.setAttribute('tabindex', '-1');
                    main.focus({ preventScroll: true });
                    if (!hadIndex) main.addEventListener('blur', () => main.removeAttribute('tabindex'), { once: true });
                }
            }
        };
        if (matchMedia('(prefers-reduced-motion: reduce)').matches || parseFloat(getComputedStyle(notice).transitionDuration) === 0) remove();
        else setTimeout(remove, 320);
    }
    document.addEventListener('submit', async event => {
        const form = event.target.closest('form[data-cookie-choices]');
        if (!form || !event.submitter?.matches('button[name="analytics"]')) return;
        event.preventDefault();
        if (saving) return;
        saving = true;
        const body = new FormData(form);
        body.set('analytics', event.submitter.value);
        body.set('returnUrl', location.pathname + location.search + location.hash);
        const forms = [...document.querySelectorAll('form[data-cookie-choices]')];
        const buttons = forms.flatMap(item => [...item.querySelectorAll('button')]);
        for (const item of forms) item.setAttribute('aria-busy', 'true');
        buttons.forEach(button => button.disabled = true);
        const message = form.querySelector('[data-cookie-result]');
        message.hidden = true;
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 10000);
        try {
            const response = await fetch(form.action, { method: 'POST', body, credentials: 'same-origin', cache: 'no-store',
                headers: { 'X-Requested-With': 'XMLHttpRequest', Accept: 'application/json' }, signal: controller.signal });
            if (!response.ok || !response.headers.get('Content-Type')?.includes('application/json')) throw new Error('No se ha podido guardar tu elección. Vuelve a intentarlo.');
            const data = await response.json();
            if (!data.success) throw new Error('No se ha podido guardar tu elección. Vuelve a intentarlo.');
            document.querySelectorAll('[data-cookie-status]').forEach(item => item.textContent = data.analytics ? 'Analítica permitida' : 'Solo cookies necesarias');
            window.dispatchEvent(new CustomEvent('alda:consent-changed', { detail: data }));
            message.textContent = data.message; message.hidden = false;
            document.querySelectorAll('[data-cookie-notice]').forEach(dismiss);
        } catch {
            message.textContent = 'No se ha podido guardar tu elección. Vuelve a intentarlo.'; message.hidden = false;
        } finally {
            clearTimeout(timeout); saving = false;
            forms.forEach(item => item.removeAttribute('aria-busy'));
            buttons.forEach(button => button.disabled = false);
        }
    });
})();
