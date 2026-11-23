/* Invoice line behaviour.
   Choosing a catalogue service fills the description so the invoice line and the service
   catalogue cannot drift apart through a typing mistake. */
(function () {
    'use strict';

    var serviceField = document.getElementById('serviceItemId');
    var description = document.getElementById('description');
    if (!serviceField || !description) {
        return;
    }

    serviceField.addEventListener('change', function () {
        var option = this.options[this.selectedIndex];
        if (option && option.dataset.label) {
            description.value = option.dataset.label;
        }
        var price = option && option.dataset.price;
        var priceField = document.getElementById('unitPrice');
        if (price && priceField && !priceField.value) {
            priceField.value = price;
        }
    });
})();
