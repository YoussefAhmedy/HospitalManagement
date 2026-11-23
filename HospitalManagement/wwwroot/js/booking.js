/* Booking form behaviour.
   Selecting a slot also selects the clinician that owns it, so the two fields can never
   disagree. Kept in an external file so the page works under a strict CSP. */
(function () {
    'use strict';

    var slots = document.querySelectorAll('input[name="slotMinutes"]');
    var doctorField = document.getElementById('doctorId');
    if (!slots.length || !doctorField) {
        return;
    }

    slots.forEach(function (radio) {
        radio.addEventListener('change', function () {
            if (this.dataset.doctor) {
                doctorField.value = this.dataset.doctor;
            }
        });
    });
})();
