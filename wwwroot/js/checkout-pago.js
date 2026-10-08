(() => {
    const form = document.getElementById('payment-form');
    if (!form) return;
    const section = document.getElementById('stripe-section');
    const spinner = document.getElementById('payment-loading');
    const container = document.getElementById('payment-element');
    const submit = document.getElementById('submit-btn');
    const retry = document.getElementById('payment-retry');
    let stripe, elements, paymentElement;
    let initializing = false, ready = false, paying = false;
    const cardSelected = () => form.querySelector('input[name="MetodoPago"]:checked')?.value === 'Tarjeta';

    function updateButton() {
        submit.disabled = paying || (cardSelected() && !ready);
        submit.setAttribute('aria-busy', String(paying));
        document.getElementById('btn-text').textContent = cardSelected()
            ? `Pagar ${form.dataset.total} y continuar` : 'Continuar a confirmación';
        document.getElementById('btn-text').classList.toggle('hidden', paying);
        document.getElementById('btn-arrow').classList.toggle('hidden', paying);
        document.getElementById('btn-loading').classList.toggle('hidden', !paying);
    }
    function showError(message, canRetry = false) {
        document.getElementById('error-message').textContent = message;
        document.getElementById('payment-errors').classList.remove('hidden');
        retry.classList.toggle('hidden', !canRetry);
    }
    function hideError() {
        document.getElementById('payment-errors').classList.add('hidden');
    }
    function loadingFailed(message) {
        ready = false;
        initializing = false;
        spinner.classList.add('hidden');
        container.setAttribute('aria-busy', 'false');
        showError(message, true);
        updateButton();
    }
    async function initStripe() {
        if (ready || initializing) return;
        initializing = true;
        hideError();
        spinner.classList.remove('hidden');
        container.setAttribute('aria-busy', 'true');
        updateButton();
        try {
            if (!form.dataset.stripeKey || typeof Stripe !== 'function')
                throw new Error('El pago con tarjeta no está disponible. Inténtalo de nuevo más tarde.');
            stripe ??= Stripe(form.dataset.stripeKey, { locale: 'es' });
            // Reuse the existing Elements instance on retry to avoid creating another intent.
            if (!elements) {
                const response = await fetch(form.dataset.intentUrl, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' }
                });
                if (!response.ok) throw new Error('No se ha podido preparar el pago. Comprueba tu conexión e inténtalo de nuevo.');
                const data = await response.json();
                if (!data.success) throw new Error(data.error || 'No se ha podido preparar el pago.');
                document.getElementById('payment-intent-id').value = data.paymentIntentId;
                elements = stripe.elements({
                    clientSecret: data.clientSecret,
                    appearance: {
                        theme: 'stripe',
                        variables: {
                            colorPrimary: '#16a34a', colorBackground: '#ffffff',
                            colorText: '#111827', colorTextSecondary: '#6b7280',
                            colorDanger: '#b91c1c', fontFamily: 'Inter, system-ui, sans-serif',
                            fontSizeBase: '16px', spacingUnit: '4px', borderRadius: '10px'
                        },
                        rules: {
                            '.Input': { border: '1px solid #d1d5db', boxShadow: 'none', padding: '12px' },
                            '.Input:focus': { border: '1px solid #16a34a', boxShadow: '0 0 0 3px #dcfce7' },
                            '.Label': { fontWeight: '500', marginBottom: '8px' }
                        }
                    }
                });
            }
            paymentElement?.destroy();
            paymentElement = elements.create('payment', { layout: 'accordion' });
            paymentElement.on('ready', () => {
                ready = true;
                initializing = false;
                spinner.classList.add('hidden');
                container.setAttribute('aria-busy', 'false');
                updateButton();
            });
            paymentElement.on('loaderror', () => loadingFailed('El formulario de Stripe no se ha podido cargar. Vuelve a intentarlo.'));
            paymentElement.on('change', event => {
                if (event.error) showError(event.error.message);
                else hideError();
            });
            paymentElement.mount('#payment-element');
        } catch (error) {
            loadingFailed(error.message || 'No se ha podido cargar el formulario de pago.');
        }
    }
    form.querySelectorAll('input[name="MetodoPago"]').forEach(radio => {
        radio.addEventListener('change', () => {
            form.querySelectorAll('.payment-option').forEach(option => {
                const selected = option.querySelector('input').checked;
                option.classList.toggle('border-emerald-600', selected);
                option.classList.toggle('bg-emerald-50', selected);
                option.classList.toggle('border-gray-200', !selected);
            });
            section.classList.toggle('hidden', !cardSelected());
            updateButton();
            if (cardSelected()) initStripe();
        });
    });
    retry.addEventListener('click', initStripe);
    form.addEventListener('submit', async event => {
        if (!cardSelected()) return;
        event.preventDefault();
        if (paying || !ready) return;
        paying = true;
        form.querySelectorAll('input[name="MetodoPago"]').forEach(radio => radio.disabled = true);
        updateButton();
        hideError();
        try {
            const { error, paymentIntent } = await stripe.confirmPayment({
                elements,
                confirmParams: { return_url: window.location.href },
                redirect: 'if_required'
            });
            if (error) throw new Error(error.message);
            if (paymentIntent?.status !== 'succeeded') throw new Error('El pago no se ha completado. Revisa los datos antes de continuar.');
            document.getElementById('payment-intent-id').value = paymentIntent.id;
            form.querySelectorAll('input[name="MetodoPago"]').forEach(radio => radio.disabled = false);
            form.submit();
            return;
        } catch (error) {
            showError(error.message || 'No se ha podido completar el pago.');
            document.getElementById('payment-errors').focus();
        }
        paying = false;
        form.querySelectorAll('input[name="MetodoPago"]').forEach(radio => radio.disabled = false);
        updateButton();
    });
    updateButton();
})();
