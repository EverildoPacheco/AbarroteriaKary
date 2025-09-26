// wwwroot/js/kary-swal.js
// Módulo global con helpers de SweetAlert2 reutilizables.
window.KarySwal = (function () {

    /**
     * Modal de éxito con dos acciones (confirm/deny).
     * Útil para "Guardar y cerrar" / "Guardar y nuevo" o "Volver al listado" / "Seguir editando".
     */
    function saveSuccess(opts) {
        const o = Object.assign({
            title: '¡Guardado!',
            text: 'La operación se realizó correctamente.',
            icon: 'success',
            indexUrl: '#',                 // a dónde ir al confirmar
            createUrl: '#',                // a dónde ir al negar
            confirmText: 'Guardar y cerrar',
            denyText: 'Guardar y nuevo',
            showDenyButton: true
        }, opts || {});

        return Swal.fire({
            icon: o.icon,
            title: o.title,
            text: o.text,
            showDenyButton: o.showDenyButton,
            confirmButtonText: o.confirmText,
            denyButtonText: o.denyText,
            allowOutsideClick: false,
            allowEscapeKey: false
        }).then(r => {
            if (r.isConfirmed && o.indexUrl) window.location.href = o.indexUrl;
            else if (r.isDenied && o.createUrl) window.location.href = o.createUrl;
        });
    }

    /**
     * Protege un formulario de salidas accidentales si hay cambios sin guardar.
     * - formSelector: selector del <form>
     * - leaveSelector: selector de enlaces/botones que "salen" (p.ej. .js-leave)
     */
    function guardUnsaved(formSelector, leaveSelector) {
        const form = document.querySelector(formSelector);
        if (!form) return;

        let isDirty = false;
        let isSubmitting = false;

        // Cualquier input/change marca el form como "sucio"
        form.addEventListener('input', () => { isDirty = true; }, { capture: true });
        form.addEventListener('change', () => { isDirty = true; }, { capture: true });

        // Envío del form: ya no preguntar
        form.addEventListener('submit', () => { isSubmitting = true; });

        // Al recargar/cerrar pestaña/back del navegador
        window.addEventListener('beforeunload', (e) => {
            if (isDirty && !isSubmitting) {
                e.preventDefault();
                e.returnValue = '';
            }
        });

        // Interceptar salidas dentro de la página (links/botones)
        document.querySelectorAll(leaveSelector).forEach(el => {
            el.addEventListener('click', (ev) => {
                if (!isDirty) return; // no hay cambios: dejar pasar
                ev.preventDefault();

                // Soportar <a href> y también data-href en botones
                const href = el.getAttribute('href') || el.getAttribute('data-href') || '#';

                Swal.fire({
                    icon: 'warning',
                    title: 'Cambios sin guardar',
                    text: 'Si sales ahora, los cambios no se guardarán.',
                    showCancelButton: true,
                    confirmButtonText: 'Salir sin guardar',
                    cancelButtonText: 'Seguir editando'
                }).then(r => {
                    if (r.isConfirmed && href && href !== '#') window.location.href = href;
                });
            });
        });
    }



    /**
     * Mensaje informativo simple (para "No se realizó ningún cambio", etc.)
     */
    function info(opts) {
        const o = Object.assign({
            title: 'Información',
            text: '',
            icon: 'info',
            confirmText: 'Aceptar',
            redirectUrl: null
        }, opts || {});
        return Swal.fire({
            icon: o.icon,
            title: o.title,
            text: o.text,
            confirmButtonText: o.confirmText
        }).then(r => {
            if (r.isConfirmed && o.redirectUrl) {
                window.location.href = o.redirectUrl;
            }
        });
    }

    return { saveSuccess, guardUnsaved, info };
})();
