(() => {
    const token = document.querySelector('meta[name="csrf-token"]')?.getAttribute('content');
    if (token && window.jQuery) {
        window.jQuery.ajaxSetup({
            headers: { RequestVerificationToken: token }
        });
    }

    document.addEventListener('click', (event) => {
        const target = event.target instanceof Element ? event.target.closest('[data-confirm]') : null;
        if (target && !window.confirm(target.getAttribute('data-confirm') || 'Continue with this action?')) {
            event.preventDefault();
        }
    });
})();
