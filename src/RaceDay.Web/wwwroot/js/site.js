document.querySelectorAll('form[data-api-form]').forEach((form) => {
    form.addEventListener('submit', () => {
        const button = form.querySelector('button[type="submit"]');
        if (!button || !form.checkValidity()) return;

        button.disabled = true;
        button.setAttribute('aria-busy', 'true');
        button.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>'
            + (form.dataset.pendingLabel || 'Please wait…');
    });
});
