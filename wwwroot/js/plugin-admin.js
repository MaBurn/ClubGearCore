(() => {
    const forms = document.querySelectorAll('[data-plugin-admin-action]');
    const zipInput = document.querySelector('[data-plugin-zip-file]');
    const zipInputLabel = document.querySelector('[data-plugin-zip-file-label]');

    if (zipInput && zipInputLabel) {
        zipInput.addEventListener('change', () => {
            const fileName = zipInput.files && zipInput.files.length > 0
                ? zipInput.files[0].name
                : 'Noch keine ZIP-Datei ausgewaehlt.';
            zipInputLabel.textContent = fileName;
        });
    }

    if (forms.length === 0) {
        return;
    }

    forms.forEach(form => {
        form.addEventListener('invalid', event => {
            const details = event.target.closest('details');
            if (details) {
                details.open = true;
            }
        }, true);

        form.addEventListener('submit', event => {
            const htmlForm = event.currentTarget;

            const technicalCategory = htmlForm.getAttribute('data-confirm-if-technical');
            if (technicalCategory === 'technical' && !window.confirm('Dieses Plugin ist ein technisches Plugin. Wirklich aktivieren?')) {
                event.preventDefault();
                return;
            }

            const confirmMessage = htmlForm.getAttribute('data-confirm');
            if (confirmMessage && !window.confirm(confirmMessage)) {
                event.preventDefault();
                return;
            }

            const submitButton = htmlForm.querySelector('button[type="submit"]');
            if (!submitButton) {
                return;
            }

            submitButton.disabled = true;
            submitButton.dataset.originalLabel = submitButton.textContent ?? '';
            submitButton.textContent = 'Wird ausgefuehrt...';
        });
    });
})();
