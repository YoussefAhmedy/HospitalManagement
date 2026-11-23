/* Clears a one-shot success banner after it has been read, without inline script.
   The element is marked with data-auto-dismiss="<milliseconds>". */
(function () {
    'use strict';

    document.querySelectorAll('[data-auto-dismiss]').forEach(function (element) {
        var delay = parseInt(element.dataset.autoDismiss, 10) || 5000;
        window.setTimeout(function () {
            element.remove();
        }, delay);
    });
})();
