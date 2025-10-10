
//(function () {
//    'use strict';

//    // ---------- Utilidades globales ----------
//    const fmtGT = new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' });
//    const parseNum = v => { const n = parseFloat(v); return isNaN(n) ? 0 : n; };

//    // Contenedores principales
//    const body = document.getElementById('detalleBody');
//    const totalC = document.getElementById('totalCell');

//    // Debounce simple para búsquedas
//    const debounce = (fn, ms = 250) => {
//        let t; return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); };
//    };

//    // ---------- Ayuda visual con SweetAlert ----------
//    function showValidacion(texto) {
//        if (window.Swal?.fire) {
//            return Swal.fire({
//                title: 'Validación',
//                text: texto || 'Revise los datos requeridos.',
//                icon: 'error',
//                confirmButtonText: 'OK'
//            });
//        }
//        // Fallback sin SweetAlert
//        alert(texto || 'Validación requerida');
//    }

//    // ---------- Recalcular importes ----------
//    function recalcRow(tr) {
//        const qty = parseNum(tr.querySelector('.js-cant').value);
//        const pvp = parseNum(tr.querySelector('.js-pvp').value);
//        const sub = Math.round(qty * pvp * 100) / 100;
//        tr.querySelector('.subtotal-cell').textContent = fmtGT.format(sub);
//        return sub;
//    }

//    function recalcTotal() {
//        let total = 0;
//        body.querySelectorAll('tr').forEach(tr => total += recalcRow(tr));
//        total = Math.round(total * 100) / 100;
//        totalC.textContent = fmtGT.format(total);
//        return total;
//    }

//    // Reindexar campos del detalle (tras eliminar filas)
//    function reindexDetalle() {
//        body.querySelectorAll('tr').forEach((tr, i) => {
//            tr.dataset.idx = i;
//            tr.querySelectorAll('[name^="Lineas["]').forEach(inp => {
//                const prop = inp.getAttribute('name').replace(/^Lineas\[\d+\]\./, '');
//                inp.setAttribute('name', `Lineas[${i}].${prop}`);
//            });
//        });
//    }

//    function findRowByProducto(productoId) {
//        return body.querySelector(`tr[data-producto-id="${productoId}"]`);
//    }

//    // ---------- Agregar/Mezclar línea de detalle ----------
//    // Se valida: precio > 0 y cantidad no supere el stock
//    // Helpers para feedback
//    function showQtyError(input, msg) {
//        const fb = input.parentElement.querySelector('.js-fb');
//        if (fb) fb.textContent = msg || 'Cantidad inválida.';
//        input.classList.add('is-invalid');
//    }
//    function clearQtyError(input) {
//        const fb = input.parentElement.querySelector('.js-fb');
//        if (fb) fb.textContent = '';
//        input.classList.remove('is-invalid');
//    }

//    function addOrMergeLinea({ productoId, codigo, nombre, imagenUrl, pvp, cantidad, stock = 0 }) {
//        const exists = findRowByProducto(productoId);
//        if (exists) {
//            const qtyInp = exists.querySelector('.js-cant');
//            const max = parseNum(exists.dataset.stock) || 0;
//            let newQty = parseNum(qtyInp.value) + cantidad;

//            if (max > 0 && newQty > max) {
//                newQty = max;
//                showQtyError(qtyInp, `Stock insuficiente. Máx: ${max}`);
//            } else if (newQty <= 0) {
//                newQty = 1;
//                showQtyError(qtyInp, 'La cantidad debe ser mayor a 0.');
//            } else {
//                clearQtyError(qtyInp);
//            }

//            qtyInp.value = newQty;
//            recalcTotal();
//            return;
//        }

//        const idx = body.querySelectorAll('tr').length;
//        const tr = document.createElement('tr');
//        tr.dataset.idx = idx;
//        tr.dataset.productoId = productoId;
//        tr.dataset.stock = stock; // guardamos el stock en el <tr>

//        tr.innerHTML = `
//    <td class="text-monospace">
//      <input type="hidden" name="Lineas[${idx}].ProductoId" value="${productoId}">
//      <input type="hidden" name="Lineas[${idx}].CodigoProducto" value="${codigo ?? ''}">
//      <input type="hidden" name="Lineas[${idx}].NombreProducto" value="${nombre ?? ''}">
//      <input type="hidden" name="Lineas[${idx}].ImagenUrl" value="${imagenUrl ?? ''}">
//      ${codigo || productoId}
//    </td>
//    <td>${nombre ?? ''}</td>
//    <td class="text-center">
//      ${imagenUrl ? `<img src="${imagenUrl}" onerror="this.src='/img/no-image.png'" class="ak-thumb" alt="ref">` : '-'}
//    </td>
//    <td class="text-end">
//      <div class="position-relative">
//        <input type="number" min="1" ${stock > 0 ? `max="${stock}"` : ''} step="1"
//               class="form-control text-end js-cant"
//               name="Lineas[${idx}].Cantidad" value="${cantidad}">
//        <div class="invalid-feedback js-fb"></div>
//      </div>
//    </td>
//    <td class="text-end">
//      <input type="number" min="0" step="0.01" class="form-control text-end js-pvp ak-readonly"
//             name="Lineas[${idx}].PrecioUnitario" value="${pvp}" readonly>
//    </td>
//    <td class="text-end subtotal-cell"></td>
//    <td class="text-center">
//      <button type="button" class="btn btn-sm btn-danger js-del"><i class="fa-solid fa-trash"></i></button>
//    </td>`;

//        body.appendChild(tr);
//        recalcTotal();
//    }

//    // ---------- Autocompletar genérico (clientes / productos) ----------
//    function bindSuggest(inputId, listId, _fetcher, onPick) {
//        const inp = document.getElementById(inputId);
//        const list = document.getElementById(listId);
//        if (!inp || !list) return;

//        const render = items => {
//            if (!items || !items.length) { list.classList.remove('show'); list.innerHTML = ''; return; }
//            list.innerHTML = items.map(it => it.html).join('');
//            list.classList.add('show');
//            // Clicks en cada item
//            list.querySelectorAll('[data-pick]').forEach(a => {
//                a.addEventListener('click', ev => {
//                    ev.preventDefault();
//                    onPick(JSON.parse(a.getAttribute('data-pick')));
//                    list.classList.remove('show');
//                    list.innerHTML = '';
//                });
//            });
//        };

//        const doSearch = debounce(async () => {
//            const q = (inp.value || '').trim();
//            if (q.length < 2) { render([]); return; }
//            const url = inp.dataset.url + `?q=${encodeURIComponent(q)}`;
//            try {
//                const rsp = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
//                const js = await rsp.json();
//                if (!js.ok) { render([]); return; }
//                render(js.items.map(m => ({ html: m._html, data: m })));
//            } catch { render([]); }
//        }, 250);

//        inp.addEventListener('input', doSearch);
//        inp.addEventListener('focus', doSearch);
//        document.addEventListener('click', e => {
//            if (!list.contains(e.target) && e.target !== inp) { list.classList.remove('show'); }
//        });
//    }

//    // ----- Clientes -----
//    bindSuggest('txtCliente', 'sugClientes', null, (item) => {
//        // item: {clienteId, nombre, nit, info}
//        document.getElementById('ClienteId').value = item.clienteId;
//        document.getElementById('txtCliente').value = item.nombre;
//        document.getElementById('txtNitCliente').value = item.nit || '';
//    });

//    // ----- Productos -----
//    bindSuggest('txtProducto', 'sugProductos', null, async (item) => {
//        // item: {productoId}
//        await abrirModalProducto(item.productoId);
//        document.getElementById('txtProducto').value = '';
//    });

//    // ---------- Modal parcial: Agregar Producto ----------
//    async function abrirModalProducto(productoId) {
//        // Limpieza de restos de modales anteriores
//        document.getElementById('modalAgregarProducto')?.remove();
//        document.querySelectorAll('.modal-backdrop').forEach(e => e.remove());
//        document.body.classList.remove('modal-open');
//        document.body.style.removeProperty('padding-right');

//        // Cargar el parcial
//        let html;
//        try {
//            const rsp = await fetch(`/Ventas/AgregarProductoModal?productoId=${encodeURIComponent(productoId)}`, {
//                headers: { 'X-Requested-With': 'XMLHttpRequest' }
//            });
//            if (!rsp.ok) throw new Error(await rsp.text());
//            html = await rsp.text();
//        } catch (e) {
//            (window.KarySwal?.error ?? alert)({ title: 'Error', text: e.message || 'No se pudo abrir el modal.' });
//            return;
//        }

//        // Inyectar y mostrar
//        const host = document.getElementById('ak-modal-host') || document.body;
//        host.innerHTML = html;

//        const modalEl = document.getElementById('modalAgregarProducto');
//        const modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });
//        modal.show();



//        // Click del botón (NO submit)
//        const btnAdd = modalEl.querySelector('#btnModalAdd');
//        btnAdd.addEventListener('click', () => {
//            const $cant = modalEl.querySelector('#apCantidad');
//            const $fb = modalEl.querySelector('#apCantidadFb');
//            const cant = parseInt($cant.value, 10) || 0;
//            const stock = parseFloat(modalEl.querySelector('#apStock')?.value || '0');

//            // reset feedback
//            $cant.classList.remove('is-invalid');
//            if ($fb) $fb.textContent = '';

//            if (cant <= 0) {
//                $cant.classList.add('is-invalid');
//                if ($fb) $fb.textContent = 'La cantidad debe ser mayor a 0.';
//                return;
//            }
//            if (stock > 0 && cant > stock) {
//                $cant.classList.add('is-invalid');
//                if ($fb) $fb.textContent = `Cantidad supera el stock disponible (${stock}).`;
//                return;
//            }

//            addOrMergeLinea({
//                productoId: modalEl.querySelector('#apProdId').value,
//                codigo: modalEl.querySelector('#apCodigo').value,
//                nombre: modalEl.querySelector('#apNombre').value,
//                imagenUrl: modalEl.querySelector('#apImagen').value || '/img/no-image.png',
//                pvp: parseFloat(modalEl.querySelector('#apPvp').value) || 0,
//                cantidad: cant,
//                  stock       // <-- NUEVO

//            });

//            bootstrap.Modal.getInstance(modalEl)?.hide();
//        });
//        // IMPORTANTE: sin { once:true }
















//        // Limpieza al cerrar
//        modalEl.addEventListener('hidden.bs.modal', () => {
//            modal.dispose();
//            modalEl.remove();
//            document.querySelectorAll('.modal-backdrop').forEach(e => e.remove());
//            document.body.classList.remove('modal-open');
//            document.body.style.removeProperty('padding-right');
//        }, { once: true });
//    }

//    // ---------- Eventos en la tabla de detalle ----------
//    // Validar cantidad editada manualmente (respetar stock y mínimo 1)
//    // Validar cantidades al escribir
//    body.addEventListener('input', e => {
//        if (!e.target.matches('.js-cant')) return;

//        const inp = e.target;
//        const tr = inp.closest('tr');
//        const max = parseNum(tr?.dataset.stock) || 0;
//        let v = parseNum(inp.value);

//        if (v <= 0) {
//            v = 1;
//            showQtyError(inp, 'La cantidad debe ser mayor a 0.');
//        } else if (max > 0 && v > max) {
//            v = max;
//            showQtyError(inp, `Stock insuficiente. Máx: ${max}`);
//        } else {
//            clearQtyError(inp);
//        }

//        inp.value = v;
//        recalcTotal();
//    });


//    // Eliminar fila
//    body.addEventListener('click', e => {
//        const btn = e.target.closest('.js-del'); if (!btn) return;
//        btn.closest('tr')?.remove();
//        reindexDetalle();
//        recalcTotal();
//    });

//    // ---------- Modal parcial: Pago ----------
//    async function abrirModalPago(total) {
//        // Quitar modal previo (evita handlers duplicados)
//        document.getElementById('modalPagoVenta')?.remove();
//        document.querySelectorAll('.modal-backdrop').forEach(e => e.remove());
//        document.body.classList.remove('modal-open');
//        document.body.style.removeProperty('padding-right');

//        // Cargar parcial
//        const url = `/Ventas/PagoModal?total=${encodeURIComponent(total)}`;
//        const rsp = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
//        if (!rsp.ok) { (window.KarySwal?.error ?? alert)({ title: 'Error', text: 'No se pudo abrir el modal de pago.' }); return; }
//        const html = await rsp.text();

//        // Inyectar
//        const host = document.getElementById('ak-modal-host') || document.body;
//        host.insertAdjacentHTML('beforeend', html);

//        // Inicializar modal
//        const modalEl = document.getElementById('modalPagoVenta');
//        const modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });
//        modal.show();

//        // Controles
//        const totalN = parseNum(document.getElementById('pvTotalNumber').value || '0');
//        const $met = document.getElementById('pvMetodo');
//        const $efec = document.getElementById('pvEfectivo');
//        const $camb = document.getElementById('pvCambio');
//        const $okBtn = document.getElementById('pvConfirmar');

//        function calcCambio() {
//            const recibido = parseNum($efec.value);
//            const cambio = Math.max(0, Math.round((recibido - totalN) * 100) / 100);
//            $camb.value = fmtGT.format(cambio);
//        }
//        calcCambio();
//        $efec.addEventListener('input', calcCambio);

//        async function askConfirm({ title, text, confirmText = 'Sí, registrar', cancelText = 'No' }) {
//            if (window.KarySwal?.confirm) {
//                return await KarySwal.confirm({ title, text, confirmText, cancelText, icon: 'question' });
//            }
//            if (window.Swal?.fire) {
//                const r = await Swal.fire({
//                    title, text, icon: 'question',
//                    showCancelButton: true, confirmButtonText: confirmText, cancelButtonText: cancelText, reverseButtons: true
//                });
//                return r.isConfirmed;
//            }
//            return window.confirm(`${title}\n\n${text}`);
//        }

//        let submitting = false;
//        $okBtn.addEventListener('click', async () => {
//            if (submitting) return;

//            if (!$met.value) { $met.classList.add('is-invalid'); return; }
//            $met.classList.remove('is-invalid');

//            const recibido = parseNum($efec.value);
//            if (recibido < totalN) { $efec.classList.add('is-invalid'); return; }
//            $efec.classList.remove('is-invalid');

//            const cambio = Math.max(0, Math.round((recibido - totalN) * 100) / 100);
//            const metodoTx = $met.options[$met.selectedIndex]?.text || $met.value;

//            const ok = await askConfirm({
//                title: '¿Registrar la venta?',
//                text:
//                    `Total: ${fmtGT.format(totalN)}\n` +
//                    `Método: ${metodoTx}\n` +
//                    `Efectivo: ${fmtGT.format(recibido)}\n` +
//                    `Cambio: ${fmtGT.format(cambio)}`
//            });
//            if (!ok) return;

//            // Pasar datos al form principal y enviar
//            document.getElementById('MetodoPagoId').value = $met.value;
//            document.getElementById('EfectivoRecibido').value = recibido.toFixed(2);
//            document.getElementById('CambioCalculado').value = cambio.toFixed(2);

//            submitting = true;
//            bootstrap.Modal.getInstance(modalEl)?.hide();
//            document.getElementById('form-venta').submit();
//        });

//        // Limpieza al cerrar
//        modalEl.addEventListener('hidden.bs.modal', () => {
//            modal.dispose();
//            modalEl.remove();
//            document.querySelectorAll('.modal-backdrop').forEach(e => e.remove());
//            document.body.classList.remove('modal-open');
//            document.body.style.removeProperty('padding-right');
//        }, { once: true });
//    }

//    // ---------- Botón "Confirmar y cobrar" ----------
//    const btnCobrar = document.getElementById('btnCobrar');
//    if (btnCobrar && !btnCobrar.dataset.wired) {
//        btnCobrar.dataset.wired = '1';
//        btnCobrar.addEventListener('click', async () => {
//            // Debe existir al menos una línea
//            if (!document.querySelector('#detalleBody tr')) {
//                showValidacion('Debe agregar al menos un producto a la venta.');
//                return;
//            }


//            // NUEVO: validamos las cantidades de la tabla
//            if (!validarCantidadesTabla()) {
//                (window.KarySwal?.warn ?? alert)({
//                    title: 'Corrija las cantidades',
//                    text: 'Hay líneas con cantidad inválida o superior al stock disponible.'
//                });
//                return;
//            }




//            const total = recalcTotal();
//            if (total <= 0) {
//                showValidacion('El total debe ser mayor a Q0.00.');
//                return;
//            }
//            await abrirModalPago(total);
//        });
//    }

//    // ---------- Init ----------
//    recalcTotal();

//    // Ajusta alto visible del detalle (opcional)
//    function ajustarAlturaDetalle(rows = 5) {
//        const cont = document.querySelector('.ak-table-scroll');
//        if (!cont) return;
//        const thead = cont.querySelector('thead tr');
//        const tfoot = cont.querySelector('tfoot tr');
//        const sample = cont.querySelector('tbody tr');

//        const hHead = thead ? thead.getBoundingClientRect().height : 48;
//        const hFoot = tfoot ? tfoot.getBoundingClientRect().height : 48;
//        const hRow = sample ? sample.getBoundingClientRect().height : 52;

//        cont.style.maxHeight = (hHead + rows * hRow + hFoot) + 'px';
//    }
//    // ajustarAlturaDetalle(); // llámelo si desea limitar alto de la tabla




//    function validarCantidadesTabla() {
//        let ok = true;
//        body.querySelectorAll('.js-cant').forEach(inp => {
//            const tr = inp.closest('tr');
//            const max = parseNum(tr?.dataset.stock) || 0;
//            const v = parseNum(inp.value);

//            if (v <= 0) {
//                showQtyError(inp, 'La cantidad debe ser mayor a 0.');
//                ok = false;
//            } else if (max > 0 && v > max) {
//                showQtyError(inp, `Stock insuficiente. Máx: ${max}`);
//                ok = false;
//            } else {
//                clearQtyError(inp);
//            }
//        });
//        return ok;
//    }

//})();


(function () {
    'use strict';

    // ================== Utilidades ==================
    const fmtGT = new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' });
    const parseNum = v => { const n = parseFloat(v); return isNaN(n) ? 0 : n; };

    const formVenta = document.getElementById('form-venta');
    const body = document.getElementById('detalleBody');
    const totalC = document.getElementById('totalCell');

    const debounce = (fn, ms = 250) => { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; };

    const swal = window.Swal?.fire
        ? (opts) => window.Swal.fire(opts)
        : async (opts) => window.confirm(opts?.title || '¿Continuar?') ? { isConfirmed: true } : { isConfirmed: false };

    const toastError = (msg) => swal({ title: 'Validación', text: msg || 'Revise los datos.', icon: 'error' });

    async function askExit() {
        const r = await swal({
            title: '¿Salir sin guardar?',
            text: 'Tienes cambios en la venta. Se perderán si sales.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Salir',
            cancelButtonText: 'Seguir aquí',
            reverseButtons: true
        });
        return r.isConfirmed;
    }

    // ================== Guard de salida ==================
    let AK_DIRTY = false;
    let AK_ALLOW_UNLOAD = false;
    let AK_BUL_ATTACHED = false;

    const onBeforeUnload = (e) => {
        if (!AK_DIRTY || AK_ALLOW_UNLOAD) return;
        e.preventDefault();
        e.returnValue = '';
    };

    function ensureBeforeUnload(on) {
        if (on && !AK_BUL_ATTACHED) {
            window.addEventListener('beforeunload', onBeforeUnload);
            AK_BUL_ATTACHED = true;
        } else if (!on && AK_BUL_ATTACHED) {
            window.removeEventListener('beforeunload', onBeforeUnload);
            AK_BUL_ATTACHED = false;
        }
    }

    function markDirty() {
        if (!AK_DIRTY) {
            AK_DIRTY = true;
            ensureBeforeUnload(true); // <-- engancha when dirty
        }
    }

    function disableUnloadPrompt() {
        AK_DIRTY = false;
        AK_ALLOW_UNLOAD = true;
        ensureBeforeUnload(false); // <-- desengancha
        window.onbeforeunload = null; // kill switch si otro setOnBeforeUnload quedó
        // Ventana de gracia por si la navegación/submit tarda milisegundos
        setTimeout(() => { AK_ALLOW_UNLOAD = false; }, 3000);
    }

    if (formVenta) {
        formVenta.addEventListener('input', markDirty);
        formVenta.addEventListener('change', markDirty);
        // Por si llega un submit nativo:
        formVenta.addEventListener('submit', disableUnloadPrompt);
    }

    // Interceptar links internos para mostrar tu SweetAlert (no el nativo)
    document.addEventListener('click', async (ev) => {
        const a = ev.target.closest('a[href]');
        if (!a) return;
        const url = new URL(a.href, location.origin);
        if (url.origin !== location.origin) return;
        if (!AK_DIRTY || AK_ALLOW_UNLOAD) return;

        ev.preventDefault(); ev.stopPropagation(); ev.stopImmediatePropagation();
        const go = await askExit();
        if (go) { disableUnloadPrompt(); location.href = a.href; }
    }, true);

    // Interceptar flechas Atrás/Adelante (no aplica a refrescar/cerrar)
    try { history.pushState({ akSentinel: true }, '', location.href); } catch { }
    let popping = false;
    window.addEventListener('popstate', async () => {
        if (popping || !AK_DIRTY || AK_ALLOW_UNLOAD) return;
        popping = true; try { history.pushState({ akSentinel: true }, '', location.href); } catch { } popping = false;
        const go = await askExit();
        if (go) { disableUnloadPrompt(); history.back(); }
    });

    // ================== Cálculos ==================
    function recalcRow(tr) {
        const qty = parseNum(tr.querySelector('.js-cant').value);
        const pvp = parseNum(tr.querySelector('.js-pvp').value);
        const sub = Math.round(qty * pvp * 100) / 100;
        tr.querySelector('.subtotal-cell').textContent = fmtGT.format(sub);
        return sub;
    }
    function recalcTotal() {
        let total = 0;
        body.querySelectorAll('tr').forEach(tr => total += recalcRow(tr));
        total = Math.round(total * 100) / 100;
        totalC.textContent = fmtGT.format(total);
        return total;
    }
    function reindexDetalle() {
        body.querySelectorAll('tr').forEach((tr, i) => {
            tr.dataset.idx = i;
            tr.querySelectorAll('[name^="Lineas["]').forEach(inp => {
                const prop = inp.getAttribute('name').replace(/^Lineas\[\d+\]\./, '');
                inp.setAttribute('name', `Lineas[${i}].${prop}`);
            });
        });
    }

    // ================== Feedback cantidad ==================
    function showQtyError(input, msg) {
        input.classList.add('is-invalid');
        const fb = input.parentElement.querySelector('.js-fb');
        if (fb) fb.textContent = msg || 'Cantidad inválida.';
    }
    function clearQtyError(input) {
        input.classList.remove('is-invalid');
        const fb = input.parentElement.querySelector('.js-fb');
        if (fb) fb.textContent = '';
    }

    // ================== Alta / merge de línea ==================
    function findRowByProducto(productoId) {
        return body.querySelector(`tr[data-producto-id="${productoId}"]`);
    }

    function addOrMergeLinea({ productoId, codigo, nombre, imagenUrl, pvp, cantidad, stock = 0 }) {
        if (stock <= 0) { toastError('Producto sin stock.'); return; }

        const exists = findRowByProducto(productoId);
        if (exists) {
            const qtyInp = exists.querySelector('.js-cant');
            const max = parseNum(exists.dataset.stock) || 0;
            let newQty = parseNum(qtyInp.value) + cantidad;

            if (max > 0 && newQty > max) { newQty = max; showQtyError(qtyInp, `Stock insuficiente. Máx: ${max}`); }
            else if (newQty <= 0) { newQty = 1; showQtyError(qtyInp, 'La cantidad debe ser mayor a 0.'); }
            else { clearQtyError(qtyInp); }

            qtyInp.value = newQty;
            markDirty(); recalcTotal(); return;
        }

        const idx = body.querySelectorAll('tr').length;
        const tr = document.createElement('tr');
        tr.dataset.idx = idx;
        tr.dataset.productoId = productoId;
        tr.dataset.stock = stock;

        tr.innerHTML = `
      <td class="text-monospace">
        <input type="hidden" name="Lineas[${idx}].ProductoId" value="${productoId}">
        <input type="hidden" name="Lineas[${idx}].CodigoProducto" value="${codigo ?? ''}">
        <input type="hidden" name="Lineas[${idx}].NombreProducto" value="${nombre ?? ''}">
        <input type="hidden" name="Lineas[${idx}].ImagenUrl" value="${imagenUrl ?? ''}">
        <input type="hidden" name="Lineas[${idx}].StockDisponible" value="${stock}">
        ${codigo || productoId}
      </td>
      <td>${nombre ?? ''}</td>
      <td class="text-center">
        ${imagenUrl ? `<img src="${imagenUrl}" onerror="this.src='/img/no-image.png'" class="ak-thumb" alt="ref">` : '-'}
      </td>
      <td class="text-end">
        <div class="position-relative">
          <input type="number" min="1" ${stock > 0 ? `max="${stock}"` : ''} step="1"
                 class="form-control text-end js-cant"
                 name="Lineas[${idx}].Cantidad" value="${Math.min(cantidad, stock)}">
          <div class="invalid-feedback js-fb"></div>
        </div>
      </td>
      <td class="text-end">
        <input type="number" min="0" step="0.01" class="form-control text-end js-pvp ak-readonly"
               name="Lineas[${idx}].PrecioUnitario" value="${pvp}" readonly>
      </td>
      <td class="text-end subtotal-cell"></td>
      <td class="text-center">
        <button type="button" class="btn btn-sm btn-danger js-del"><i class="fa-solid fa-trash"></i></button>
      </td>`;
        body.appendChild(tr);
        markDirty(); recalcTotal();
    }

    // ================== Autocomplete (cliente / producto) ==================
    function bindSuggest(inputId, listId, onPick) {
        const inp = document.getElementById(inputId);
        const list = document.getElementById(listId);
        if (!inp || !list) return;

        const render = items => {
            if (!items?.length) { list.classList.remove('show'); list.innerHTML = ''; return; }
            list.innerHTML = items.map(it => it.html).join('');
            list.classList.add('show');
            list.querySelectorAll('[data-pick]').forEach(a => {
                a.addEventListener('click', ev => {
                    ev.preventDefault();
                    onPick(JSON.parse(a.getAttribute('data-pick')));
                    list.classList.remove('show'); list.innerHTML = '';
                });
            });
        };

        const doSearch = debounce(async () => {
            const q = (inp.value || '').trim();
            if (q.length < 2) { render([]); return; }
            const url = inp.dataset.url + `?q=${encodeURIComponent(q)}`;
            try {
                const rsp = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                const js = await rsp.json();
                if (!js.ok) { render([]); return; }
                render(js.items.map(m => ({ html: m._html, data: m })));
            } catch { render([]); }
        }, 250);

        inp.addEventListener('input', doSearch);
        inp.addEventListener('focus', doSearch);
        document.addEventListener('click', e => {
            if (!list.contains(e.target) && e.target !== inp) { list.classList.remove('show'); }
        });
    }

    bindSuggest('txtCliente', 'sugClientes', (item) => {
        document.getElementById('ClienteId').value = item.clienteId;
        document.getElementById('txtCliente').value = item.nombre;
        document.getElementById('txtNitCliente').value = item.nit || '';
        markDirty();
    });

    bindSuggest('txtProducto', 'sugProductos', async (item) => {
        await abrirModalProducto(item.productoId);
        document.getElementById('txtProducto').value = '';
    });

    // ================== Modal: Agregar Producto ==================
    async function abrirModalProducto(productoId) {
        document.getElementById('modalAgregarProducto')?.remove();
        document.querySelectorAll('.modal-backdrop').forEach(e => e.remove());
        document.body.classList.remove('modal-open');
        document.body.style.removeProperty('padding-right');

        let html;
        try {
            const rsp = await fetch(`/Ventas/AgregarProductoModal?productoId=${encodeURIComponent(productoId)}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!rsp.ok) throw new Error(await rsp.text());
            html = await rsp.text();
        } catch (e) { toastError(e.message || 'No se pudo abrir el modal.'); return; }

        const host = document.getElementById('ak-modal-host') || document.body;
        host.innerHTML = html;

        const modalEl = document.getElementById('modalAgregarProducto');
        const modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });
        modal.show();

        modalEl.querySelector('#btnModalAdd').addEventListener('click', () => {
            const $cant = modalEl.querySelector('#apCantidad');
            const $fb = modalEl.querySelector('#apCantidadFb');
            const cant = parseInt($cant.value, 10) || 0;
            const stock = parseFloat(modalEl.querySelector('#apStock')?.value || '0');

            $cant.classList.remove('is-invalid'); if ($fb) $fb.textContent = '';

            if (cant <= 0) { $cant.classList.add('is-invalid'); if ($fb) $fb.textContent = 'La cantidad debe ser mayor a 0.'; return; }
            if (stock <= 0) { $cant.classList.add('is-invalid'); if ($fb) $fb.textContent = 'Producto sin stock.'; return; }
            if (cant > stock) { $cant.classList.add('is-invalid'); if ($fb) $fb.textContent = `Cantidad supera el stock disponible (${stock}).`; return; }

            addOrMergeLinea({
                productoId: modalEl.querySelector('#apProdId').value,
                codigo: modalEl.querySelector('#apCodigo').value,
                nombre: modalEl.querySelector('#apNombre').value,
                imagenUrl: modalEl.querySelector('#apImagen').value || '/img/no-image.png',
                pvp: parseFloat(modalEl.querySelector('#apPvp').value) || 0,
                cantidad: cant,
                stock
            });
            markDirty();
            bootstrap.Modal.getInstance(modalEl)?.hide();
        });

        modalEl.addEventListener('hidden.bs.modal', () => {
            modal.dispose(); modalEl.remove();
            document.querySelectorAll('.modal-backdrop').forEach(e => e.remove());
            document.body.classList.remove('modal-open');
            document.body.style.removeProperty('padding-right');
        }, { once: true });
    }

    // ================== Eventos tabla ==================
    body.addEventListener('input', e => {
        if (!e.target.matches('.js-cant')) return;
        const inp = e.target;
        const tr = inp.closest('tr');
        const max = parseNum(tr?.dataset.stock) || 0;
        let v = parseNum(inp.value);

        if (max <= 0 && v > 0) { v = 0; showQtyError(inp, 'Producto sin stock.'); }
        else if (v <= 0) { v = 1; showQtyError(inp, 'La cantidad debe ser mayor a 0.'); }
        else if (max > 0 && v > max) { v = max; showQtyError(inp, `Stock insuficiente. Máx: ${max}`); }
        else { clearQtyError(inp); }

        inp.value = v; markDirty(); recalcTotal();
    });

    body.addEventListener('click', e => {
        const btn = e.target.closest('.js-del'); if (!btn) return;
        btn.closest('tr')?.remove();
        reindexDetalle(); markDirty(); recalcTotal();
    });

    // ================== Modal: Pago ==================
    async function abrirModalPago(total) {
        document.getElementById('modalPagoVenta')?.remove();
        document.querySelectorAll('.modal-backdrop').forEach(e => e.remove());
        document.body.classList.remove('modal-open');
        document.body.style.removeProperty('padding-right');

        const url = `/Ventas/PagoModal?total=${encodeURIComponent(total)}`;
        const rsp = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (!rsp.ok) { toastError('No se pudo abrir el modal de pago.'); return; }
        const html = await rsp.text();

        const host = document.getElementById('ak-modal-host') || document.body;
        host.insertAdjacentHTML('beforeend', html);

        const modalEl = document.getElementById('modalPagoVenta');
        const modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });
        modal.show();

        const totalN = parseNum(document.getElementById('pvTotalNumber').value || '0');
        const $met = document.getElementById('pvMetodo');
        const $efec = document.getElementById('pvEfectivo');
        const $camb = document.getElementById('pvCambio');
        const $okBtn = document.getElementById('pvConfirmar');

        const calcCambio = () => {
            const recibido = parseNum($efec.value);
            const cambio = Math.max(0, Math.round((recibido - totalN) * 100) / 100);
            $camb.value = fmtGT.format(cambio);
        };
        calcCambio(); $efec.addEventListener('input', calcCambio);

        async function askConfirmVenta({ totalN, metodoTx, recibido, cambio }) {
            const r = await swal({
                title: '¿Registrar la venta?',
                text: `Total: ${fmtGT.format(totalN)}  Método: ${metodoTx}  Efectivo: ${fmtGT.format(recibido)}  Cambio: ${fmtGT.format(cambio)}`,
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: 'Sí, registrar',
                cancelButtonText: 'No',
                reverseButtons: true
            });
            return r.isConfirmed;
        }

        let submitting = false;
        $okBtn.addEventListener('click', async () => {
            if (submitting) return;

            if (!$met.value) { $met.classList.add('is-invalid'); return; }
            $met.classList.remove('is-invalid');

            const recibido = parseNum($efec.value);
            if (recibido < totalN) { $efec.classList.add('is-invalid'); return; }
            $efec.classList.remove('is-invalid');

            const cambio = Math.max(0, Math.round((recibido - totalN) * 100) / 100);
            const metodoTx = $met.options[$met.selectedIndex]?.text || $met.value;

            const ok = await askConfirmVenta({ totalN, metodoTx, recibido, cambio });
            if (!ok) return;

            // Pasar datos al form principal
            document.getElementById('MetodoPagoId').value = $met.value;
            document.getElementById('EfectivoRecibido').value = recibido.toFixed(2);
            document.getElementById('CambioCalculado').value = cambio.toFixed(2);

            submitting = true;

            // *** APAGO CUALQUIER PROMPT DE SALIDA ANTES DE CERRAR EL MODAL ***
            disableUnloadPrompt();

            bootstrap.Modal.getInstance(modalEl)?.hide();
            // Usa requestSubmit para respetar validaciones y evitar side-effects
            if (formVenta.requestSubmit) formVenta.requestSubmit();
            else formVenta.submit();
        });

        modalEl.addEventListener('hidden.bs.modal', () => {
            modal.dispose(); modalEl.remove();
            document.querySelectorAll('.modal-backdrop').forEach(e => e.remove());
            document.body.classList.remove('modal-open');
            document.body.style.removeProperty('padding-right');
        }, { once: true });
    }

    // ================== Botón "Confirmar y cobrar" ==================
    function validarCantidadesTabla() {
        let ok = true;
        body.querySelectorAll('.js-cant').forEach(inp => {
            const tr = inp.closest('tr');
            const max = parseNum(tr?.dataset.stock) || 0;
            const v = parseNum(inp.value);
            if (max <= 0 && v > 0) { showQtyError(inp, 'Producto sin stock.'); ok = false; }
            else if (v <= 0) { showQtyError(inp, 'La cantidad debe ser mayor a 0.'); ok = false; }
            else if (max > 0 && v > max) { showQtyError(inp, `Stock insuficiente. Máx: ${max}`); ok = false; }
            else { clearQtyError(inp); }
        });
        return ok;
    }

    const btnCobrar = document.getElementById('btnCobrar');
    if (btnCobrar && !btnCobrar.dataset.wired) {
        btnCobrar.dataset.wired = '1';
        btnCobrar.addEventListener('click', async () => {
            if (!document.querySelector('#detalleBody tr')) { toastError('Debe agregar al menos un producto a la venta.'); return; }
            if (!validarCantidadesTabla()) { toastError('Corrija las cantidades en el detalle.'); return; }
            const total = recalcTotal();
            if (total <= 0) { toastError('El total debe ser mayor a Q0.00.'); return; }
            await abrirModalPago(total); // <- solo abre el modal, sin navegación
        });
    }

    // ================== Init ==================
    recalcTotal();
})();
