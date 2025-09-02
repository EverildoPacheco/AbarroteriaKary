// wwwroot/js/kary-swal.js
window.KarySwal = (function () {
    function saveSuccess(opts) {
        const o = Object.assign({
            title: '¡Guardado!',
            text: 'La operación se realizó correctamente.',
            icon: 'success',
            indexUrl: '#',
            createUrl: '#'
        }, opts || {});

        return Swal.fire({
            icon: o.icon,
            title: o.title,
            text: o.text,
            showDenyButton: true,
            confirmButtonText: 'Guardar y cerrar',
            denyButtonText: 'Guardar y nuevo',
            allowOutsideClick: false,
            allowEscapeKey: false
        }).then(r => {
            if (r.isConfirmed) { window.location.href = o.indexUrl; }
            else if (r.isDenied) { window.location.href = o.createUrl; }
        });
    }

    function guardUnsaved(formSelector, leaveSelector) {
        const form = document.querySelector(formSelector);
        if (!form) return;

        let isDirty = false;
        let isSubmitting = false;

        // cualquier cambio marca el formulario como "sucio"
        form.addEventListener('input', () => { isDirty = true; }, { capture: true });
        form.addEventListener('change', () => { isDirty = true; }, { capture: true });

        // al enviar, ya no preguntar
        form.addEventListener('submit', () => { isSubmitting = true; });

        // aviso al salir por recarga/cerrar pestaña/back
        window.addEventListener('beforeunload', (e) => {
            if (isDirty && !isSubmitting) {
                e.preventDefault();
                e.returnValue = '';
            }
        });

        // interceptar clicks en botones/enlaces de salida dentro del área indicada
        const targets = document.querySelectorAll(leaveSelector);
        targets.forEach(el => {
            el.addEventListener('click', (ev) => {
                if (!isDirty) return; // no hay cambios: dejar pasar
                ev.preventDefault();
                const href = el.getAttribute('href');
                Swal.fire({
                    icon: 'warning',
                    title: 'Cambios sin guardar',
                    text: 'Si sales ahora, los cambios no se guardarán.',
                    showCancelButton: true,
                    confirmButtonText: 'Salir sin guardar',
                    cancelButtonText: 'Seguir editando'
                }).then(r => {
                    if (r.isConfirmed && href) window.location.href = href;
                });
            });
        });
    }

    return { saveSuccess, guardUnsaved };
})();
