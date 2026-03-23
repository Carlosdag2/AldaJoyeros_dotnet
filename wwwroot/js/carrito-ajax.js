/**
 * AldaJoyeros - Carrito AJAX con animación
 * Función Alpine.js reutilizable para añadir productos al carrito sin redirección.
 * Se usa desde cualquier vista con: x-data="carritoNotificacion()"
 */
function carritoNotificacion() {
return {
    mostrarNotificacion: false,
    mensajeNotificacion: '',
    cargandoProducto: null,
    animando: false,

    init() {
        window.carritoApp = this;
    },

        async agregarAlCarrito(productoId, nombreProducto, event) {
            this.cargandoProducto = productoId;

            try {
                const response = await fetch('/Carrito/AgregarAjax', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: `productoId=${productoId}&cantidad=1`
                });

                const data = await response.json();

                if (data.success) {
                    await this.animarVueloAlCarrito(event);
                    this.actualizarContadorCarrito(data.totalItems);
                    this.activarNotificacion(`'${nombreProducto}' añadido al carrito`);
                } else if (data.requiresLogin) {
                    window.location.href = '/Auth/Login';
                } else {
                    this.activarNotificacion(data.message || 'Error al añadir al carrito');
                }
            } catch (error) {
                console.error('Error al añadir al carrito:', error);
                this.activarNotificacion('Error al añadir al carrito');
            } finally {
                this.cargandoProducto = null;
            }
        },

        activarNotificacion(mensaje) {
            this.mensajeNotificacion = mensaje;
            this.mostrarNotificacion = true;
            setTimeout(() => { this.mostrarNotificacion = false; }, 4000);
        },

        actualizarContadorCarrito(totalItems) {
            // Badge principal del header
            const badge = document.querySelector('[data-carrito-count]');
            if (badge) {
                badge.textContent = totalItems;
                badge.classList.toggle('hidden', totalItems <= 0);
                badge.classList.add('animate-bounce');
                setTimeout(() => badge.classList.remove('animate-bounce'), 500);
            }

            // Contador en el menú dropdown
            const menuWrapper = document.querySelector('[data-carrito-count-menu]');
            const menuValue = document.querySelector('[data-carrito-count-value]');
            if (menuWrapper && menuValue) {
                menuValue.textContent = totalItems;
                menuWrapper.classList.toggle('hidden', totalItems <= 0);
            }

            // Icono del carrito
            const icon = document.querySelector('[data-carrito-icon]');
            if (icon) {
                icon.classList.add('animate-bounce');
                setTimeout(() => icon.classList.remove('animate-bounce'), 600);
            }
        },

        async animarVueloAlCarrito(event) {
            return new Promise((resolve) => {
                const flyingItem = this.$refs.flyingItem;
                if (!flyingItem) { resolve(); return; }

                const button = event.target.closest('button');
                const buttonRect = button.getBoundingClientRect();

                const carritoLink = document.querySelector('[data-carrito-link]');
                let endX, endY;

                if (carritoLink) {
                    const rect = carritoLink.getBoundingClientRect();
                    endX = rect.left + rect.width / 2;
                    endY = rect.top + rect.height / 2;
                } else {
                    endX = window.innerWidth - 60;
                    endY = 30;
                }

                const startX = buttonRect.left + buttonRect.width / 2;
                const startY = buttonRect.top + buttonRect.height / 2;

                flyingItem.style.left = startX + 'px';
                flyingItem.style.top = startY + 'px';
                flyingItem.style.transform = 'translate(-50%, -50%) scale(1)';
                flyingItem.style.opacity = '1';
                flyingItem.style.display = 'block';
                this.animando = true;

                const duration = 600;
                const startTime = performance.now();

                const animate = (currentTime) => {
                    const elapsed = currentTime - startTime;
                    const progress = Math.min(elapsed / duration, 1);
                    const easeOut = 1 - Math.pow(1 - progress, 3);

                    const currentX = startX + (endX - startX) * easeOut;
                    const arcHeight = -100 * Math.sin(progress * Math.PI);
                    const currentY = startY + (endY - startY) * easeOut + arcHeight;
                    const scale = 1 - (progress * 0.5);

                    flyingItem.style.left = currentX + 'px';
                    flyingItem.style.top = currentY + 'px';
                    flyingItem.style.transform = `translate(-50%, -50%) scale(${scale}) rotate(${progress * 360}deg)`;
                    flyingItem.style.opacity = 1 - (progress * 0.3);

                    if (progress < 1) {
                        requestAnimationFrame(animate);
                    } else {
                        flyingItem.style.display = 'none';
                        this.animando = false;
                        resolve();
                    }
                };

                requestAnimationFrame(animate);
            });
        }
    };
}
