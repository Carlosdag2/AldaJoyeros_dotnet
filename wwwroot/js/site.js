// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener('DOMContentLoaded', () => {
    const toggleBtn = document.getElementById('theme-toggle-btn');
    const rootElement = document.documentElement; // Aplica al <html>
    const STORAGE_KEY = 'hc-theme-preference';

    if (!toggleBtn) return;

    // Función que aplica el tema y actualiza ARIA
    const applyTheme = (theme) => {
        const spanText = toggleBtn.querySelector('span');

        if (theme === 'high-contrast') {
            rootElement.setAttribute('data-theme', 'high-contrast');
            toggleBtn.setAttribute('aria-pressed', 'true');
            if(spanText) spanText.textContent = "Modo Normal";
        } else {
            rootElement.setAttribute('data-theme', 'normal');
            toggleBtn.setAttribute('aria-pressed', 'false');
            if(spanText) spanText.textContent = "Alto Contraste";
        }
        // Guardar preferencia explícita del usuario
        localStorage.setItem(STORAGE_KEY, theme);
    };

    // 1. Inicialización: Comprobar LocalStorage
    const savedTheme = localStorage.getItem(STORAGE_KEY);

    // 2. Comprobar preferencia del Sistema Operativo
    const systemPrefersHC = window.matchMedia('(prefers-contrast: more)');

    // Prioridad: 1° Preferencia del usuario, 2° Preferencia del sistema
    if (savedTheme) {
        applyTheme(savedTheme);
    } else if (systemPrefersHC.matches) {
        applyTheme('high-contrast');
    }

    // 3. Manejador de clic: Alternar modos
    toggleBtn.addEventListener('click', () => {
        const currentTheme = rootElement.getAttribute('data-theme');
        const isCurrentlyHC = currentTheme === 'high-contrast';

        applyTheme(isCurrentlyHC ? 'normal' : 'high-contrast');
    });

    // 4. Escuchar dinámicamente si el usuario cambia la configuración de su PC/Mac
    systemPrefersHC.addEventListener('change', (e) => {
        // Solo aplicar si el usuario no ha forzado una preferencia manual en la web
        if (!localStorage.getItem(STORAGE_KEY)) {
            applyTheme(e.matches ? 'high-contrast' : 'normal');
        }
    });
});