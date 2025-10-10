// wwwroot/js/caja-sesion.js
(function () {
    'use strict';

    // Evitar doble init
    if (window.__cajaSesionInit) return;
    window.__cajaSesionInit = true;

    const host = document.getElementById('ak-modal-host');
    const btnNueva = document.getElementById('btnNuevaVenta');
    const btnOpen = document.getElementById('btnAperturarCaja');
    const btnClose = document.getElementById('btnCerrarCaja');

    function getCajaId() {
        return document.getElementById('CajaIdActual')?.value || '';
    }

    function setNuevaVentaEnabled(abierta) {
        if (!btnNueva) return;
        if (abierta) {
            btnNueva.classList.remove('disabled');
            btnNueva.removeAttribute('aria-disabled');
        } else {
            btnNueva.classList.add('disabled');
            btnNueva.setAttribute('aria-disabled', 'true');
        }
    }

    // Sincroniza UI cuando se emite el evento
    document.addEventListener('caja:estadoChanged', (ev) => {
        const abierta = !!(ev.detail && ev.detail.abierta);
        setNuevaVentaEnabled(abierta);
        window.__cajaSesionId = ev.detail?.sesionId || null;
    });

    async function getEstado(cajaId) {
        const rsp = await fetch(`/CajaSesion/Estado?cajaId=${encodeURIComponent(cajaId)}`, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        try { return await rsp.json(); } catch { return { ok: false }; }
    }

    // ========= Helpers =========

    // Ejecuta la lógica del modal de CIERRE (diferencia y nota obligatoria)
    function initCierreModal(modalEl) {
        const form = modalEl.querySelector('#form-cierre-caja');
        if (!form) return;

        const esperado = parseFloat(form.dataset.esperado || '0');
        const ef = form.querySelector('#MontoFinal');
        const dif = form.querySelector('#akDiff');
        const nota = form.querySelector('#NotaCierre');
        const fmtGT = new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' });

        const toNum = v => {
            const n = parseFloat((v ?? '').toString().replace(',', '.'));
            return isNaN(n) ? 0 : n;
        };

        function pintar() {
            const contado = toNum(ef?.value);
            const diff = Math.round((contado - esperado) * 100) / 100;

            let cls = 'text-success';
            if (diff > 0) cls = 'text-primary';
            if (diff < 0) cls = 'text-danger';

            if (dif) {
                dif.textContent = fmtGT.format(diff);
                dif.className = 'fw-semibold ' + cls;
            }

            const lbl = form.querySelector('label[for="NotaCierre"]');
            if (nota) {
                if (diff !== 0) {
                    nota.setAttribute('required', 'required');
                    if (lbl) lbl.textContent = 'Nota de cierre (obligatoria porque no cuadra)';
                } else {
                    nota.removeAttribute('required');
                    if (lbl) lbl.textContent = 'Nota de cierre';
                }
            }

            // guardamos el diff para validación en submit
            form.dataset.diff = String(diff);
        }

        pintar();
        ef?.addEventListener('input', pintar);
    }

    // Inserta modal, 
    function insertAndShowModal(html, modalId) {
        if (!host) return;

        const prev = document.getElementById(modalId);
        if (prev && prev.parentElement) prev.parentElement.removeChild(prev);

        const wrap = document.createElement('div');
        wrap.innerHTML = html;

        const modalEl = wrap.querySelector('#' + modalId);
        if (!modalEl) return;

        host.appendChild(modalEl);

        try {
            const bsModal = new bootstrap.Modal(modalEl, { backdrop: 'static' });
            bsModal.show();

            // Inicializaciones específicas
            if (modalId === 'modalCajaCierre') {
                initCierreModal(modalEl);
            }
        } catch (e) {
            console.error('Bootstrap modal error:', e);
        }
    }

    async function loadPartial(url) {
        const res = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        const ct = (res.headers.get('Content-Type') || '').toLowerCase();
        if (!res.ok) {
            if (ct.includes('application/json')) {
                const js = await res.json().catch(() => ({}));
                throw new Error(js.message || 'Error de servidor.');
            }
            throw new Error('Error de servidor.');
        }
        return await res.text();
    }

    // Helper: lee JSON seguro (para POSTs)
    async function readJsonSafe(res) {
        const ct = (res.headers.get('Content-Type') || '').toLowerCase();
        if (ct.includes('application/json')) {
            try { return await res.json(); } catch { return null; }
        }
        return null;
    }

    // ---------- Botón: APERTURAR (GET /CajaSesion/Abrir?cajaId=...) ----------
    if (btnOpen && !btnOpen.dataset.bound) {
        btnOpen.dataset.bound = '1';
        btnOpen.addEventListener('click', async (ev) => {
            ev.preventDefault();
            const cajaId = getCajaId();
            if (!cajaId) {
                (window.KarySwal?.error || alert)({ title: 'Caja no definida', text: 'No se indicó la caja.' });
                return;
            }

            try {
                const st = await getEstado(cajaId);
                if (st?.ok && st.estado === 'ABIERTA') {
                    (window.KarySwal?.info || alert)({ title: 'Caja ya aperturada', text: 'Cierre la caja actual antes de abrir otra sesión.' });
                    return;
                }
            } catch { /* continúa */ }

            try {
                const html = await loadPartial(`/CajaSesion/Abrir?cajaId=${encodeURIComponent(cajaId)}`);
                insertAndShowModal(html, 'modalCajaApertura');
            } catch (e) {
                (window.KarySwal?.error || alert)({ title: 'No se pudo cargar', text: e.message || '' });
            }
        });
    }

    // ---------- Botón: CERRAR (GET /CajaSesion/Cerrar?cajaId=...) ----------
    if (btnClose && !btnClose.dataset.bound) {
        btnClose.dataset.bound = '1';
        btnClose.addEventListener('click', async (ev) => {
            ev.preventDefault();
            const cajaId = getCajaId();
            if (!cajaId) {
                (window.KarySwal?.error || alert)({ title: 'Caja no definida', text: 'No se indicó la caja.' });
                return;
            }

            try {
                const st = await getEstado(cajaId);
                if (!(st?.ok && st.estado === 'ABIERTA')) {
                    (window.KarySwal?.info || alert)({ title: 'No hay caja aperturada', text: 'Aperture una caja antes de cerrar.' });
                    return;
                }
            } catch {
                (window.KarySwal?.error || alert)({ title: 'Error', text: 'No se pudo consultar el estado de caja.' });
                return;
            }

            try {
                const html = await loadPartial(`/CajaSesion/Cerrar?cajaId=${encodeURIComponent(cajaId)}`);
                insertAndShowModal(html, 'modalCajaCierre');
            } catch (e) {
                (window.KarySwal?.error || alert)({ title: 'No se pudo cargar', text: e.message || '' });
            }
        });
    }

    // ---------- Delegación SUBMIT (POST Abrir / Cerrar) ----------
    document.addEventListener('submit', async function (ev) {
        const form = ev.target;
        //if (!(form instanceof HTMLFormElement)) return;

        //// APERTURA -> POST /CajaSesion/Abrir
        //if (form.id === 'form-apertura-caja') {
        //    ev.preventDefault();
        //    const data = new FormData(form);

        //    try { window.KaryForms?.lockButton?.('#btn-aperturar', '<i class="fa-solid fa-spinner fa-spin me-1"></i> Abriendo...'); } catch { }

        //    try {
        //        const res = await fetch(form.action, {
        //            method: 'POST',
        //            body: data,
        //            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        //        });

        //        const json = await readJsonSafe(res) ?? { ok: false, message: 'Respuesta no válida del servidor.' };
        //        if (!res.ok || !json.ok) {
        //            (window.KarySwal?.error || alert)({ title: 'No se pudo abrir la caja', text: json.message || '' });
        //            return;
        //        }

        //        (window.KarySwal?.saveSuccess || alert)({ title: '¡Caja aperturada!', text: json.message || 'La caja quedó abierta.' });

        //        document.dispatchEvent(new CustomEvent('caja:estadoChanged', { detail: { abierta: true, sesionId: json.sesionId } }));

        //        const modalEl = document.getElementById('modalCajaApertura');
        //        const modal = bootstrap.Modal.getInstance(modalEl);
        //        modal?.hide();
        //        modalEl?.remove();
        //    } catch (e) {
        //        (window.KarySwal?.error || alert)({ title: 'Error', text: e.message || 'No se pudo abrir la caja.' });
        //    } finally {
        //        try { window.KaryForms?.unlockButton?.('#btn-aperturar'); } catch { }
        //    }
        //}


        // APERTURA -> POST /CajaSesion/Abrir
        if (form.id === 'form-apertura-caja') {
            ev.preventDefault();
            const data = new FormData(form);

            try { window.KaryForms?.lockButton?.('#btn-aperturar', '<i class="fa-solid fa-spinner fa-spin me-1"></i> Abriendo...'); } catch { }

            try {
                const res = await fetch(form.action, {
                    method: 'POST',
                    body: data,
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                const json = await (async () => {
                    const ct = (res.headers.get('Content-Type') || '').toLowerCase();
                    if (ct.includes('application/json')) try { return await res.json(); } catch { }
                    return null;
                })() ?? { ok: false, message: 'Respuesta no válida del servidor.' };

                if (!res.ok || !json.ok) {
                    (window.KarySwal?.error || alert)({ title: 'No se pudo abrir la caja', text: json.message || '' });
                    return;
                }

                // Actualiza estado UI (habilita "Nueva venta")
                document.dispatchEvent(new CustomEvent('caja:estadoChanged', { detail: { abierta: true, sesionId: json.sesionId } }));

                // Cierra el modal Bootstrap de apertura
                const modalEl = document.getElementById('modalCajaApertura');
                const modal = bootstrap.Modal.getInstance(modalEl);
                modal?.hide();
                modalEl?.remove();

                // Éxito con UN SOLO botón (sin timer, sin "Guardar y nuevo")
                if (window.Swal?.fire) {
                    await Swal.fire({
                        title: '¡Caja aperturada!',
                        text: json.message || 'La caja quedó abierta.',
                        icon: 'success',
                        confirmButtonText: 'Aceptar',
                        allowOutsideClick: false,
                        allowEscapeKey: false
                    });
                } else {
                    alert('¡Caja aperturada!\n' + (json.message || 'La caja quedó abierta.'));
                }

            } catch (e) {
                (window.KarySwal?.error || alert)({ title: 'Error', text: e.message || 'No se pudo abrir la caja.' });
            } finally {
                try { window.KaryForms?.unlockButton?.('#btn-aperturar'); } catch { }
            }
        }







        // CIERRE -> POST /CajaSesion/Cerrar  (con validación previa)
        // CIERRE -> POST /CajaSesion/Cerrar  (con validación previa)
        if (form.id === 'form-cierre-caja') {
            ev.preventDefault();

            // --- Validaciones cliente ---
            const esperado = parseFloat(form.dataset.esperado || '0');
            const ef = form.querySelector('#MontoFinal');
            const nota = form.querySelector('#NotaCierre');

            const contado = parseFloat((ef?.value || '').toString().replace(',', '.'));
            const diff = Math.round(((isNaN(contado) ? 0 : contado) - esperado) * 100) / 100;

            ef?.classList.remove('is-invalid');
            nota?.classList.remove('is-invalid');

            if (isNaN(contado) || contado < 0) {
                ef?.classList.add('is-invalid');
                (window.KarySwal?.error || alert)({ title: 'Dato inválido', text: 'El total efectivo debe ser un número válido mayor o igual a 0.' });
                return;
            }
            if (diff !== 0 && (!nota || !nota.value.trim())) {
                nota?.classList.add('is-invalid');
                (window.KarySwal?.error || alert)({ title: 'Falta nota', text: 'Hay diferencia de efectivo. Es obligatorio escribir una nota de cierre.' });
                return;
            }
            // --- Fin validaciones ---

            const data = new FormData(form);

            try { window.KaryForms?.lockButton?.('#btn-cerrar', '<i class="fa-solid fa-spinner fa-spin me-1"></i> Cerrando...'); } catch { }

            try {
                const res = await fetch(form.action, {
                    method: 'POST',
                    body: data,
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                const json = await (async () => {
                    const ct = (res.headers.get('Content-Type') || '').toLowerCase();
                    if (ct.includes('application/json')) try { return await res.json(); } catch { }
                    return null;
                })() ?? { ok: false, message: 'Respuesta no válida del servidor.' };

                if (!res.ok || !json.ok) {
                    (window.KarySwal?.error || alert)({ title: 'No se pudo cerrar la caja', text: json.message || '' });
                    return;
                }

                // Actualiza estado UI
                document.dispatchEvent(new CustomEvent('caja:estadoChanged', { detail: { abierta: false, sesionId: null } }));

                // Cierra el modal Bootstrap primero
                const modalEl = document.getElementById('modalCajaCierre');
                const modal = bootstrap.Modal.getInstance(modalEl);
                modal?.hide();
                modalEl?.remove();

                // Éxito SIN timer (espera que el usuario pulse OK)
                if (window.Swal?.fire) {
                    await Swal.fire({
                        title: '¡Caja cerrada!',
                        text: json.message || 'La caja quedó cerrada.',
                        icon: 'success',
                        confirmButtonText: 'OK',
                        allowOutsideClick: false,
                        allowEscapeKey: false
                    });
                } else {
                    alert('¡Caja cerrada!\n' + (json.message || 'La caja quedó cerrada.'));
                }

                // Recarga después de que el usuario confirme
                if (location.pathname.toLowerCase().includes('/ventas')) {
                    location.reload();
                }
            } catch (e) {
                (window.KarySwal?.error || alert)({ title: 'Error', text: e.message || 'No se pudo cerrar la caja.' });
            } finally {
                try { window.KaryForms?.unlockButton?.('#btn-cerrar'); } catch { }
            }
        }






    });


       


    // Estado inicial (habilita/deshabilita "Nueva venta")
    (async function initEstado() {
        const cajaId = getCajaId();
        if (!cajaId) return;
        try {
            const st = await getEstado(cajaId);
            const abierta = st?.ok && st.estado === 'ABIERTA';
            document.dispatchEvent(new CustomEvent('caja:estadoChanged', {
                detail: { abierta, sesionId: abierta ? st.sesionId : null }
            }));
        } catch { /* silencioso */ }
    })();
})();
