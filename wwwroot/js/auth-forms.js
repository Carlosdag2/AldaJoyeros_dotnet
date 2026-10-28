(() => {
    for (const form of document.querySelectorAll('form[data-auth-form]')) {
        const hidePasswords = [];
        for (const toggle of form.querySelectorAll('[data-password-toggle]')) {
            const input = document.getElementById(toggle.getAttribute('aria-controls'));
            if (!input || !form.contains(input)) continue;
            const showLabel = toggle.getAttribute('aria-label');
            function hide() { input.type = 'password'; toggle.setAttribute('aria-pressed', 'false'); toggle.setAttribute('aria-label', showLabel); toggle.querySelector('i').className = 'fas fa-eye'; }
            hidePasswords.push(hide); toggle.hidden = false;
            toggle.addEventListener('click', () => {
                if (input.type === 'text') hide();
                else { input.type = 'text'; toggle.setAttribute('aria-pressed', 'true'); toggle.setAttribute('aria-label', showLabel.replace(/^Mostrar/, 'Ocultar')); toggle.querySelector('i').className = 'fas fa-eye-slash'; }
            });
        }
        const code = form.querySelector('[data-verification-code]');
        if (code) {
            const clean = value => value.replace(/[^0-9]/g, '').slice(0, 6);
            code.addEventListener('input', () => { code.value = clean(code.value); });
            code.addEventListener('paste', event => { event.preventDefault(); code.value = clean(event.clipboardData.getData('text')); code.dispatchEvent(new Event('input', { bubbles: true })); });
        }
        const confirmation = form.querySelector('[data-password-confirm]');
        const password = form.querySelector('#Password');
        function compare() {
            if (!confirmation) return;
            const message = confirmation.value && password.value !== confirmation.value ? 'Las contraseñas no coinciden.' : '';
            confirmation.setCustomValidity(message);
            confirmation.setAttribute('aria-invalid', String(Boolean(message)));
            form.querySelector('[data-password-match-message]').textContent = message;
        }
        if (confirmation) { confirmation.addEventListener('input', compare); password.addEventListener('input', compare); }
        const submit = form.querySelector('button[type="submit"]');
        const label = submit.querySelector('[data-submit-label]');
        const original = label.textContent;
        form.addEventListener('submit', event => {
            if (confirmation) { compare(); if (!confirmation.checkValidity()) { event.preventDefault(); confirmation.reportValidity(); return; } }
            // Let all validation handlers finish before showing the sending state.
            setTimeout(() => {
                if (event.defaultPrevented || !form.checkValidity()) return;
                submit.disabled = true; form.setAttribute('aria-busy', 'true'); label.textContent = submit.dataset.busyLabel;
            }, 0);
        });
        window.addEventListener('pageshow', () => { submit.disabled = false; form.removeAttribute('aria-busy'); label.textContent = original; });
        window.addEventListener('pagehide', () => hidePasswords.forEach(hide => hide()));
    }
})();
