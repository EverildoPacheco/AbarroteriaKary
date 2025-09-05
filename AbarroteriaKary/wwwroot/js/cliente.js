// wwwroot/js/cliente-create.js
(function () {
    'use strict';

    const frm = document.getElementById('frmCliente');
    const chk = document.getElementById('chkEsEmpleado');

    // Hidden del form principal (IDs generados por TagHelper)
    const hEsEmpleado = document.getElementById('EsEmpleado');
    const hPuestoId = document.getElementById('PuestoId');
    const hFechaIngreso = document.getElementById('FechaIngreso');
    const hGenero = document.getElementById('Genero');
    const hFechaNac = document.getElementById('FechaNacimiento'); // NUEVO

    // Modal y controles internos
    const mdlInstance = new bootstrap.Modal(document.getElementById('mdlEmpleado'), { backdrop: 'static' });
    const inpPuesto = document.getElementById('mdlPuestoId');
    const inpFecha = document.getElementById('mdlFechaIngreso');
    const inpGenero = document.getElementById('mdlGenero');
    const inpFechaN = document.getElementById('mdlFechaNacimiento');

    const errPuesto = document.getElementById('errPuesto');
    const errFecha = document.getElementById('errFecha');
    const errGenero = document.getElementById('errGenero');
    const errFechaN = document.getElementById('errFechaNac');

    // Al marcar "También es empleado" -> abrir modal
    chk?.addEventListener('change', function () {
        if (this.checked) {
            mdlInstance.show();
        } else {
            // Limpiar si lo desmarcan
            hEsEmpleado.value = 'false';
            hPuestoId.value = '';
            hFechaIngreso.value = '';
            hGenero.value = '';
            hFechaNac.value = '';
        }
    });

    // Confirmar "Crear Cliente-Empleado"
    document.getElementById('btnCrearClienteEmpleado')?.addEventListener('click', function () {
        // Validación simple en cliente (modal)
        let ok = true;
        [errPuesto, errFecha, errGenero, errFechaN].forEach(e => e.classList.add('d-none'));

        if (!inpPuesto.value) { errPuesto.classList.remove('d-none'); ok = false; }
        if (!inpFecha.value) { errFecha.classList.remove('d-none'); ok = false; }
        if (!inpGenero.value) { errGenero.classList.remove('d-none'); ok = false; }
        if (!inpFechaN.value) { errFechaN.classList.remove('d-none'); ok = false; }

        if (!ok) return;

        // Pasar valores al form principal y enviar
        hEsEmpleado.value = 'true';
        hPuestoId.value = inpPuesto.value;
        hFechaIngreso.value = inpFecha.value;    // yyyy-MM-dd
        hGenero.value = inpGenero.value;
        hFechaNac.value = inpFechaN.value;   // yyyy-MM-dd

        mdlInstance.hide();
        frm.submit();
    });
})();
