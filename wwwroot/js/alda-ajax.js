/**
 * AldaJoyeros - Utilidades AJAX compartidas
 * Este archivo contiene funciones reutilizables para peticiones AJAX en toda la aplicación
 */

// ========================================
// UTILIDADES AJAX
// ========================================
const AldaAjax = {
    /**
     * Realizar petición GET con manejo de errores
     */
    async get(url, options = {}) {
        try {
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    ...options.headers
                }
            });
            
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            return await response.json();
        } catch (error) {
            console.error('Error en GET:', error);
            throw error;
        }
    },

    /**
     * Realizar petición POST con manejo de errores
     */
    async post(url, data = {}, options = {}) {
        try {
            let body;
            let contentType;

            if (data instanceof FormData) {
                body = data;
            } else if (typeof data === 'object') {
                body = new URLSearchParams(data).toString();
                contentType = 'application/x-www-form-urlencoded';
            } else {
                body = data;
            }

            const headers = {
                'X-Requested-With': 'XMLHttpRequest',
                ...options.headers
            };

            if (contentType) headers['Content-Type'] = contentType;

            const response = await fetch(url, {
                method: 'POST',
                headers,
                body
            });

            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            return await response.json();
        } catch (error) {
            console.error('Error en POST:', error);
            throw error;
        }
    }
};

// ========================================
// NOTIFICACIONES TOAST
// ========================================
const AldaToast = {
    container: null,

    init() {
        if (!this.container) {
            this.container = document.createElement('div');
            this.container.id = 'alda-toast-container';
            this.container.className = 'fixed bottom-6 right-6 z-50 flex flex-col gap-3';
            document.body.appendChild(this.container);
        }
    },

    show(message, type = 'info', duration = 4000) {
        this.init();

        const toast = document.createElement('div');
        toast.className = `
            flex items-center gap-3 px-5 py-4 rounded-lg shadow-xl transform translate-x-full
            transition-all duration-300 ease-out max-w-sm
            ${type === 'success' ? 'bg-emerald-900 text-white' : ''}
            ${type === 'error' ? 'bg-red-600 text-white' : ''}
            ${type === 'warning' ? 'bg-amber-500 text-white' : ''}
            ${type === 'info' ? 'bg-slate-800 text-white' : ''}
        `;

        const icons = {
            success: 'fa-check-circle',
            error: 'fa-times-circle',
            warning: 'fa-exclamation-triangle',
            info: 'fa-info-circle'
        };

        toast.innerHTML = `
            <i class="fas ${icons[type]} text-lg"></i>
            <span class="flex-1 text-sm">${message}</span>
            <button onclick="this.parentElement.remove()" class="opacity-70 hover:opacity-100">
                <i class="fas fa-times"></i>
            </button>
        `;

        this.container.appendChild(toast);

        // Animar entrada
        requestAnimationFrame(() => {
            toast.classList.remove('translate-x-full');
            toast.classList.add('translate-x-0');
        });

        // Auto-eliminar
        setTimeout(() => {
            toast.classList.add('translate-x-full', 'opacity-0');
            setTimeout(() => toast.remove(), 300);
        }, duration);
    },

    success(message) { this.show(message, 'success'); },
    error(message) { this.show(message, 'error'); },
    warning(message) { this.show(message, 'warning'); },
    info(message) { this.show(message, 'info'); }
};

// ========================================
// LOADING OVERLAY
// ========================================
const AldaLoading = {
    overlay: null,

    show(message = 'Cargando...') {
        if (this.overlay) return;

        this.overlay = document.createElement('div');
        this.overlay.id = 'alda-loading-overlay';
        this.overlay.className = 'fixed inset-0 bg-white/80 z-50 flex items-center justify-center';
        this.overlay.innerHTML = `
            <div class="text-center">
                <i class="fas fa-circle-notch fa-spin text-4xl text-emerald-900 mb-4"></i>
                <p class="text-slate-600">${message}</p>
            </div>
        `;
        document.body.appendChild(this.overlay);
    },

    hide() {
        if (this.overlay) {
            this.overlay.remove();
            this.overlay = null;
        }
    }
};

// ========================================
// CONFIRMACIÓN DE ACCIONES
// ========================================
const AldaConfirm = {
    /**
     * Mostrar diálogo de confirmación
     */
    show(options = {}) {
        return new Promise((resolve) => {
            const {
                title = '¿Estás seguro?',
                message = '',
                confirmText = 'Confirmar',
                cancelText = 'Cancelar',
                type = 'warning'
            } = options;

            const colors = {
                warning: 'bg-amber-500 hover:bg-amber-600',
                danger: 'bg-red-500 hover:bg-red-600',
                info: 'bg-blue-500 hover:bg-blue-600'
            };

            const modal = document.createElement('div');
            modal.className = 'fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4';
            modal.innerHTML = `
                <div class="bg-white rounded-2xl shadow-2xl max-w-md w-full transform scale-95 opacity-0 transition-all duration-200">
                    <div class="p-6">
                        <h3 class="text-lg font-semibold text-slate-800 mb-2">${title}</h3>
                        <p class="text-slate-600">${message}</p>
                    </div>
                    <div class="px-6 pb-6 flex gap-3 justify-end">
                        <button class="btn-cancel px-4 py-2 text-slate-600 hover:text-slate-800 font-medium">
                            ${cancelText}
                        </button>
                        <button class="btn-confirm px-4 py-2 text-white rounded-lg font-medium ${colors[type]}">
                            ${confirmText}
                        </button>
                    </div>
                </div>
            `;

            document.body.appendChild(modal);

            // Animar entrada
            requestAnimationFrame(() => {
                const content = modal.querySelector('div > div');
                content.classList.remove('scale-95', 'opacity-0');
                content.classList.add('scale-100', 'opacity-100');
            });

            const close = (result) => {
                const content = modal.querySelector('div > div');
                content.classList.add('scale-95', 'opacity-0');
                setTimeout(() => {
                    modal.remove();
                    resolve(result);
                }, 200);
            };

            modal.querySelector('.btn-cancel').onclick = () => close(false);
            modal.querySelector('.btn-confirm').onclick = () => close(true);
            modal.onclick = (e) => { if (e.target === modal) close(false); };
        });
    }
};

// ========================================
// ACTUALIZACIÓN DE CONTADORES
// ========================================
const AldaCounter = {
    /**
     * Actualizar contador del carrito con animación
     */
    updateCart(count) {
        const counters = document.querySelectorAll('[data-carrito-count], [data-carrito-count-value]');
        counters.forEach(counter => {
            counter.textContent = count;
            counter.closest('[data-carrito-count-menu]')?.classList.toggle('hidden', count === 0);
            
            // Animación de rebote
            counter.classList.add('animate-bounce');
            setTimeout(() => counter.classList.remove('animate-bounce'), 500);
        });
    },

    /**
     * Animar un número incrementalmente
     */
    animateNumber(element, targetValue, duration = 500) {
        const startValue = parseInt(element.textContent) || 0;
        const startTime = performance.now();

        const animate = (currentTime) => {
            const elapsed = currentTime - startTime;
            const progress = Math.min(elapsed / duration, 1);
            
            const easeOut = 1 - Math.pow(1 - progress, 3);
            const currentValue = Math.round(startValue + (targetValue - startValue) * easeOut);
            
            element.textContent = currentValue.toLocaleString('es-ES');

            if (progress < 1) {
                requestAnimationFrame(animate);
            }
        };

        requestAnimationFrame(animate);
    }
};

// ========================================
// TABLAS CON AJAX
// ========================================
const AldaTable = {
    /**
     * Configurar una tabla con paginación y filtros AJAX
     */
    init(config) {
        const {
            containerId,
            url,
            onLoad,
            pageSize = 15
        } = config;

        const container = document.getElementById(containerId);
        if (!container) return;

        return {
            currentPage: 1,
            filters: {},

            async load(page = 1) {
                this.currentPage = page;
                
                const params = new URLSearchParams({
                    page: this.currentPage,
                    ...this.filters
                });

                try {
                    const data = await AldaAjax.get(`${url}?${params}`);
                    if (data.success && onLoad) {
                        onLoad(data, container);
                    }
                } catch (error) {
                    AldaToast.error('Error al cargar los datos');
                }
            },

            setFilter(key, value) {
                if (value) {
                    this.filters[key] = value;
                } else {
                    delete this.filters[key];
                }
                this.load(1);
            },

            nextPage() {
                this.load(this.currentPage + 1);
            },

            prevPage() {
                if (this.currentPage > 1) {
                    this.load(this.currentPage - 1);
                }
            }
        };
    }
};

// Exponer globalmente
window.AldaAjax = AldaAjax;
window.AldaToast = AldaToast;
window.AldaLoading = AldaLoading;
window.AldaConfirm = AldaConfirm;
window.AldaCounter = AldaCounter;
window.AldaTable = AldaTable;

// Inicializar al cargar
document.addEventListener('DOMContentLoaded', () => {
    AldaToast.init();
});
