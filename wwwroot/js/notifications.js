(() => {
    'use strict';
    const kinds = {
        success: ['Hecho', 'fa-check'], error: ['No se ha podido completar', 'fa-exclamation'],
        warning: ['Revisa este aviso', 'fa-exclamation-triangle'], info: ['Información', 'fa-info']
    };
    const mounted = new WeakSet();
    function container() {
        let element = document.getElementById('notifications-container');
        if (!element) {
            element = document.createElement('div');
            element.id = 'notifications-container';
            document.body.appendChild(element);
        }
        return element;
    }
    function mount(card, duration) {
        if (mounted.has(card)) return;
        mounted.add(card);
        let remaining = duration, timer, started, closed = false;
        function close() {
            if (closed) return;
            closed = true;
            clearTimeout(timer);
            if (card.contains(document.activeElement)) document.getElementById('main-content')?.focus({ preventScroll: true });
            card.classList.add('is-leaving');
            setTimeout(() => card.remove(), matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 180);
            document.removeEventListener('visibilitychange', visibility);
        }
        function pause() { if (timer) { clearTimeout(timer); timer = null; remaining -= Date.now() - started; } }
        function resume() {
            if (closed || duration <= 0 || document.hidden || card.matches(':hover') || card.contains(document.activeElement) || timer) return;
            started = Date.now();
            timer = setTimeout(close, Math.max(remaining, 0));
        }
        function visibility() { if (document.hidden) pause(); else resume(); }
        card.querySelector('[data-notice-close]').addEventListener('click', close);
        card.addEventListener('mouseenter', pause);
        card.addEventListener('mouseleave', resume);
        card.addEventListener('focusin', pause);
        card.addEventListener('focusout', () => setTimeout(resume, 0));
        document.addEventListener('visibilitychange', visibility);
        resume();
    }
    const api = {
        init() {
            container().querySelectorAll('.alda-notice').forEach(card => mount(card, Number(card.dataset.duration) || 0));
        },
        show(message, type = 'info', duration) {
            type = Object.hasOwn(kinds, type) ? type : 'info';
            const [title, icon] = kinds[type];
            const card = document.createElement('div');
            card.className = `alda-notice notification notification-${type}`;
            card.dataset.kind = type;
            card.setAttribute('role', type === 'error' || type === 'warning' ? 'alert' : 'status');
            card.setAttribute('aria-atomic', 'true');
            const badge = document.createElement('span');
            badge.className = 'alda-notice-icon';
            badge.setAttribute('aria-hidden', 'true');
            const symbol = document.createElement('i');
            symbol.className = `fas ${icon}`;
            badge.append(symbol);
            const body = document.createElement('div');
            body.className = 'alda-notice-body';
            const heading = document.createElement('p');
            heading.className = 'alda-notice-title';
            heading.textContent = title;
            const text = document.createElement('p');
            text.className = 'alda-notice-message';
            text.textContent = String(message);
            body.append(heading, text);
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'alda-notice-close';
            button.dataset.noticeClose = '';
            button.setAttribute('aria-label', 'Cerrar notificación');
            const cross = document.createElement('i');
            cross.className = 'fas fa-times';
            cross.setAttribute('aria-hidden', 'true');
            button.append(cross);
            card.append(badge, body, button);
            container().append(card);
            mount(card, duration ?? (type === 'success' || type === 'info' ? 8000 : 0));
            return card;
        },
        success(message) { return this.show(message, 'success'); },
        error(message) { return this.show(message, 'error'); },
        warning(message) { return this.show(message, 'warning'); },
        info(message) { return this.show(message, 'info'); }
    };
    window.AldaNotifications = api;
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => api.init());
    else api.init();
})();
