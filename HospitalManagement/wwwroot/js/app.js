// CareAxis application shell behaviour: navigation drawer, quiet form submission with
// antiforgery, confirmation dialogs, toasts, and client-side table filtering.
// Everything degrades gracefully: with JavaScript disabled, forms still post normally.
(function () {
    'use strict';

    const csrfToken = document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') || '';
    const toastStack = document.getElementById('toast-stack');

    /* ------------------------------------------------------------ sidebar */
    const sidebar = document.getElementById('app-sidebar');
    const sidebarToggle = document.querySelector('[data-sidebar-toggle]');
    if (sidebar && sidebarToggle) {
        sidebarToggle.addEventListener('click', () => {
            const open = sidebar.classList.toggle('is-open');
            sidebarToggle.setAttribute('aria-expanded', String(open));
        });

        document.addEventListener('click', (event) => {
            if (window.innerWidth > 1024 || !sidebar.classList.contains('is-open')) {
                return;
            }
            if (!sidebar.contains(event.target) && !sidebarToggle.contains(event.target)) {
                sidebar.classList.remove('is-open');
                sidebarToggle.setAttribute('aria-expanded', 'false');
            }
        });

        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && sidebar.classList.contains('is-open')) {
                sidebar.classList.remove('is-open');
                sidebarToggle.setAttribute('aria-expanded', 'false');
                sidebarToggle.focus();
            }
        });
    }

    /* ------------------------------------------------------------- toasts */
    function toast(message, tone) {
        if (!toastStack) {
            return;
        }
        const element = document.createElement('div');
        element.className = 'toast' + (tone ? ' toast--' + tone : '');
        element.setAttribute('role', 'status');
        element.textContent = message;
        toastStack.appendChild(element);
        window.setTimeout(() => element.remove(), 6000);
    }

    window.careaxisToast = toast;

    /* --------------------------------------------------- confirmation flow */
    document.addEventListener('submit', (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        const message = form.getAttribute('data-confirm');
        if (message && !form.dataset.confirmed) {
            event.preventDefault();
            if (window.confirm(message)) {
                form.dataset.confirmed = 'true';
                form.requestSubmit ? form.requestSubmit() : form.submit();
            }
            return;
        }

        const submitButton = form.querySelector('[data-submitting-label]');
        if (submitButton) {
            submitButton.disabled = true;
            submitButton.textContent = submitButton.getAttribute('data-submitting-label');
        }
    });

    /* ------------------------------------------- quiet action (fetch) flow */
    document.addEventListener('click', async (event) => {
        const trigger = event.target.closest('[data-quiet-action]');
        if (!trigger) {
            return;
        }
        event.preventDefault();

        const url = trigger.getAttribute('data-quiet-action');
        const method = (trigger.getAttribute('data-method') || 'POST').toUpperCase();
        const confirmMessage = trigger.getAttribute('data-confirm');
        const successMessage = trigger.getAttribute('data-success');

        if (confirmMessage && !window.confirm(confirmMessage)) {
            return;
        }

        trigger.setAttribute('aria-busy', 'true');
        trigger.classList.add('is-disabled');

        try {
            const response = await fetch(url, {
                method,
                headers: {
                    'RequestVerificationToken': csrfToken,
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                },
                credentials: 'same-origin'
            });

            const payload = await safeJson(response);
            if (!response.ok) {
                toast(payload?.message || 'The action could not be completed.', 'danger');
                return;
            }

            toast(payload?.message || successMessage || 'Action completed.', 'success');
            if (payload?.redirect) {
                window.location.assign(payload.redirect);
            } else if (trigger.hasAttribute('data-reload')) {
                window.location.reload();
            }
        } catch {
            toast('Network error. Check your connection and try again.', 'danger');
        } finally {
            trigger.removeAttribute('aria-busy');
            trigger.classList.remove('is-disabled');
        }
    });

    async function safeJson(response) {
        try {
            return await response.json();
        } catch {
            return null;
        }
    }

    /* ------------------------------------------------------- client tables */
    document.querySelectorAll('[data-table-filter]').forEach((input) => {
        const table = document.querySelector(input.getAttribute('data-table-filter'));
        if (!table) {
            return;
        }
        const rows = Array.from(table.querySelectorAll('tbody tr'));
        input.addEventListener('input', () => {
            const needle = input.value.trim().toLowerCase();
            rows.forEach((row) => {
                row.hidden = needle.length > 0 && !row.textContent.toLowerCase().includes(needle);
            });
        });
    });

    /* ------------------------------------------------------ print triggers */
    document.querySelectorAll('[data-print]').forEach((button) => {
        button.addEventListener('click', () => window.print());
    });

    /* ---------------------------------------------------- dialogs & drawers */
    document.querySelectorAll('[data-dialog-open]').forEach((button) => {
        button.addEventListener('click', () => {
            const dialog = document.getElementById(button.getAttribute('data-dialog-open'));
            if (dialog && typeof dialog.showModal === 'function') {
                dialog.showModal();
            }
        });
    });

    document.querySelectorAll('[data-dialog-close]').forEach((button) => {
        button.addEventListener('click', () => button.closest('dialog')?.close());
    });

    window.careaxisDrawer = function (id, open) {
        const drawer = document.getElementById(id);
        if (!drawer) {
            return;
        }
        drawer.classList.toggle('is-open', open);
        if (open) {
            drawer.querySelector('[data-drawer-close]')?.focus();
        }
    };

    document.addEventListener('click', (event) => {
        if (event.target.matches('[data-drawer-close], .drawer__backdrop')) {
            event.target.closest('.drawer')?.classList.remove('is-open');
        }
    });

    /* ---------------------------------------------- auto-submit filter bar */
    document.querySelectorAll('[data-auto-submit] select').forEach((select) => {
        select.addEventListener('change', () => select.form?.requestSubmit());
    });
})();
