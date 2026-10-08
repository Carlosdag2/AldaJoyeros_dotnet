(() => {
    const button = document.getElementById('btnEnviarFactura');
    const feedback = document.getElementById('invoice-feedback');
    if (!button || !feedback) return;
    button.addEventListener('click', async () => {
        if (button.disabled) return;
        const original = button.innerHTML;
        button.disabled = true;
        button.setAttribute('aria-busy', 'true');
        button.innerHTML = '<i class="fas fa-circle-notch fa-spin" aria-hidden="true"></i> Enviando…';
        feedback.classList.add('hidden');
        try {
            const response = await fetch(button.dataset.invoiceUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                }
            });
            if (!response.ok) throw new Error('No se ha podido enviar la factura. Inténtalo de nuevo más tarde.');
            const result = await response.json();
            if (!result.success) throw new Error(result.message || 'No se ha podido enviar la factura.');
            feedback.textContent = 'Factura enviada al email de tu cuenta.';
            feedback.classList.remove('text-red-700');
            feedback.classList.add('text-emerald-700');
        } catch (error) {
            feedback.textContent = error.message || 'Comprueba tu conexión e inténtalo de nuevo.';
            feedback.classList.remove('text-emerald-700');
            feedback.classList.add('text-red-700');
        } finally {
            feedback.classList.remove('hidden');
            button.innerHTML = original;
            button.disabled = false;
            button.setAttribute('aria-busy', 'false');
        }
    });
})();
