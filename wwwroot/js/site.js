(() => {
    'use strict';
    const key = 'alda-theme-preference';
    const allowed = ['system', 'light', 'dark', 'high-contrast'];
    const root = document.documentElement;
    const dark = window.matchMedia('(prefers-color-scheme: dark)');
    const contrast = window.matchMedia('(prefers-contrast: more)');
    let preference = 'system';
    try {
        const saved = localStorage.getItem(key);
        const legacy = localStorage.getItem('hc-theme-preference');
        preference = allowed.includes(saved) ? saved
            : legacy === 'high-contrast' ? 'high-contrast' : legacy === 'normal' ? 'light' : 'system';
    } catch { /* El tema sigue funcionando si el navegador bloquea el almacenamiento. */ }

    function apply() {
        const theme = preference === 'system'
            ? contrast.matches ? 'high-contrast' : dark.matches ? 'dark' : 'light'
            : preference;
        root.dataset.theme = theme;
        root.classList.toggle('dark', theme === 'dark');
        root.style.colorScheme = theme === 'light' ? 'light' : 'dark';
        document.querySelectorAll('[data-theme-select]').forEach(select => {
            select.value = preference;
            select.title = `Apariencia: ${select.selectedOptions[0]?.textContent ?? preference}`;
        });
        window.dispatchEvent(new CustomEvent('alda:themechange', { detail: { theme, preference } }));
    }
    function setPreference(value) {
        if (!allowed.includes(value)) return;
        preference = value;
        try { localStorage.setItem(key, value); } catch { /* Preferencia válida durante esta página. */ }
        apply();
    }
    window.AldaTheme = Object.freeze({
        setPreference,
        getPreference: () => preference,
        getTheme: () => root.dataset.theme
    });
    // Se ejecuta en el head, antes de dibujar la página, para evitar el destello de un tema diferente.
    apply();
    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('[data-theme-select]').forEach(select =>
            select.addEventListener('change', () => setPreference(select.value)));
        apply();
    });
    for (const query of [dark, contrast]) query.addEventListener('change', () => {
        if (preference === 'system') apply();
    });
    window.addEventListener('storage', event => {
        if (event.key !== key && event.key !== null) return;
        preference = allowed.includes(event.newValue) ? event.newValue : 'system';
        apply();
    });
})();
