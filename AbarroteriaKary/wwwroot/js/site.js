// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


(function (w) {
    'use strict';
    const KF = w.KaryForms = w.KaryForms || {};

    // Usa jQuery unobtrusive si existe; si no, HTML5
    KF.isValid = function (form) {
        if (w.jQuery && typeof jQuery.fn.valid === 'function') {
            return jQuery(form).valid();
        }
        return form.checkValidity();
    };

    /**
     * Vincula envío seguro a un formulario
     * @param {string|HTMLFormElement} formSelector - selector o form
     * @param {string|HTMLElement} btnSelector - selector o botón submit
     * @param {object} opts - { spinnerHtml?: string, disableOnSubmit?: boolean }
     */
    KF.bindSafeSubmit = function (formSelector, btnSelector, opts) {
        const form = typeof formSelector === 'string' ? document.querySelector(formSelector) : formSelector;
        if (!form) return;

        const btn = typeof btnSelector === 'string' ? document.querySelector(btnSelector) : btnSelector;
        const spinnerHtml = opts && opts.spinnerHtml;
        const disableOnSubmit = (opts && opts.disableOnSubmit) !== false; // por defecto true

        form.addEventListener('submit', function (e) {
            const ok = KF.isValid(form);
            if (!ok) {
                e.preventDefault();
                e.stopImmediatePropagation();
                if (btn) {
                    btn.disabled = false;
                    // si habías cambiado el HTML del botón, podrías restaurarlo aquí
                    if (btn.dataset.originalHtml) {
                        btn.innerHTML = btn.dataset.originalHtml;
                        delete btn.dataset.originalHtml;
                    }
                }
                return;
            }

            if (disableOnSubmit && btn) {
                btn.disabled = true;
                if (spinnerHtml) {
                    btn.dataset.originalHtml = btn.innerHTML;
                    btn.innerHTML = spinnerHtml;
                }
            }
        });

        // Rehabilitar al hacer reset
        form.addEventListener('reset', function () {
            if (btn) {
                btn.disabled = false;
                if (btn.dataset.originalHtml) {
                    btn.innerHTML = btn.dataset.originalHtml;
                    delete btn.dataset.originalHtml;
                }
            }
        });
    };

    // Modo auto: busca forms con data-safe-submit
    KF.auto = function () {
        document.querySelectorAll('form[data-safe-submit]').forEach(form => {
            const btnSel = form.getAttribute('data-safe-submit-button') || 'button[type="submit"]';
            const spinnerHtml = form.getAttribute('data-safe-submit-spinner');
            KF.bindSafeSubmit(form, form.querySelector(btnSel), { spinnerHtml });
        });
    };

    // Auto-init si hay formularios marcados
    document.addEventListener('DOMContentLoaded', function () {
        if (document.querySelector('form[data-safe-submit]')) {
            KF.auto();
        }
    });
})(window);
