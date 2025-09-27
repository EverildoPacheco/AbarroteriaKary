// wwwroot/js/pedidos.js

// =========== Helpers comunes ===========
const $q = (sel, root = document) => root.querySelector(sel);
const $qa = (sel, root = document) => Array.from(root.querySelectorAll(sel));
const fmt = (s) => (s ?? '').toString().trim();
const html = (s) => (s ?? '').toString()
    .replaceAll('&', '&amp;').replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;').replaceAll('"', '&quot;');
const swalErr = (text) => { if (window.Swal) Swal.fire({ icon: 'error', title: 'Validación', text }); else alert(text); };

// ===================== CREATE =====================
document.addEventListener('DOMContentLoaded', () => {
    if (!window.PAGE || window.PAGE.module !== 'Pedidos' || window.PAGE.view !== 'Create') return;

    const cfg = window.PEDIDOS || {};
    const form = $q('#frmPedido');
    const tbody = $q('#tblDetalle tbody');
    const txt = $q('#txtBuscarProducto');
    const box = $q('#sugerencias');

    // Proteger salida con cambios sin guardar
    if (window.KarySwal && typeof KarySwal.guardUnsaved === 'function') {
        KarySwal.guardUnsaved('#frmPedido', '.js-leave');
    }

    // ----- PRG: éxito → preguntar PDF (abre en pestaña nueva) -----
    (function () {
        const creadoOk = String(cfg.creadoOk) === 'true';
        const pedidoId = (cfg.pedidoId || '').trim();

        if (!creadoOk || !pedidoId) return;

        const afterSuccess = () => {
            const pdfUrl = cfg.pdfUrl || (cfg.urls && cfg.urls.pdfBase ? `${cfg.urls.pdfBase}/${encodeURIComponent(pedidoId)}` : null);
            if (!pdfUrl) return;

            if (window.Swal) {
                Swal.fire({
                    icon: 'question',
                    title: '¿Generar PDF ahora?',
                    text: 'Podrás entregarlo al proveedor para cotización.',
                    showCancelButton: true,
                    confirmButtonText: 'Sí, generar',
                    cancelButtonText: 'No, permanecer aquí'
                }).then(r => {
                    if (r.isConfirmed) {
                        // Solo nueva pestaña (sin fallback a misma pestaña, para evitar doble apertura)
                        window.open(pdfUrl, '_blank', 'noopener,noreferrer');
                    }
                });
            } else {
                if (confirm('¿Desea generar el PDF del pedido?')) {
                    window.open(pdfUrl, '_blank', 'noopener,noreferrer');
                }
            }
        };

        if (window.Swal) {
            Swal.fire({
                icon: 'success',
                title: 'Pedido generado',
                text: `¡El pedido ${pedidoId} se guardó exitosamente!`
            }).then(afterSuccess);
        } else {
            alert(`Pedido ${pedidoId} generado exitosamente.`);
            afterSuccess();
        }
    })();

    // ----- Autocomplete -----
    const buscarUrl = cfg.urls?.buscar || '';
    let lastQ = '';
    if (txt && box && buscarUrl) {
        txt.addEventListener('input', async (e) => {
            const q = fmt(e.target.value);
            if (q.length < 2) { box.classList.add('d-none'); box.innerHTML = ''; return; }
            if (q === lastQ) return; lastQ = q;

            try {
                const res = await fetch(`${buscarUrl}?q=${encodeURIComponent(q)}&top=20`);
                const list = await res.json();
                renderSuggestCreate(list);
            } catch (err) { console.error(err); }
        });
    }

    function renderSuggestCreate(list) {
        if (!list || list.length === 0) { box.classList.add('d-none'); box.innerHTML = ''; return; }
        box.innerHTML = list.map(p => `
      <div class="k-suggest-item" data-id="${p.productoId}" data-cod="${html(p.codigoProducto)}"
           data-nom="${html(p.nombreProducto)}" data-desc="${html(p.descripcionProducto || '')}"
           data-img="${html(p.imagenUrl || '')}">
        <img class="k-img" src="${html(p.imagenUrl || '')}" onerror="this.src='';" alt="">
        <div>
          <div><strong>${html(p.nombreProducto)}</strong></div>
          <div class="text-muted" style="font-size:12px">${html(p.productoId)} — ${html(p.descripcionProducto || '')}</div>
        </div>
      </div>`).join('');
        box.classList.remove('d-none');

        $qa('.k-suggest-item', box).forEach(item => {
            item.addEventListener('click', () => {
                const prod = {
                    id: item.dataset.id,
                    codigo: item.dataset.cod,
                    nombre: item.dataset.nom,
                    descripcion: item.dataset.desc,
                    img: item.dataset.img
                };
                addOrIncrementCreate(prod);
                box.classList.add('d-none'); box.innerHTML = ''; txt.value = '';
            });
        });
    }

    // ----- Detalle (Create) -----
    function addOrIncrementCreate(p) {
        if (!p?.id) { swalErr('Producto inválido'); return; }

        let row = tbody.querySelector(`tr[data-id="${p.id}"]`);
        if (row) {
            const $cant = row.querySelector('input[data-field="cantidad"]');
            $cant.value = (parseInt($cant.value || '0', 10) || 0) + 1;
            return;
        }

        const tr = document.createElement('tr');
        tr.dataset.id = p.id;
        tr.innerHTML = `
      <td><span data-field="codigo">${html(p.id)}</span></td>
      <td><span data-field="nombre">${html(p.nombre)}</span></td>
      <td><span data-field="desc">${html(p.descripcion)}</span></td>
      <td><img class="k-img" src="${html(p.img || '')}" onerror="this.src='';" alt=""></td>
      <td><input type="number" class="form-control form-control-sm" value="1" min="1" step="1" data-field="cantidad"></td>
      <td class="text-center"><button type="button" class="k-del" title="Quitar">&times;</button></td>`;
        tbody.appendChild(tr);

        tr.querySelector('.k-del').addEventListener('click', () => { tr.remove(); reindexHiddenInputsCreate(); });

        const $cant = tr.querySelector('input[data-field="cantidad"]');
        $cant.addEventListener('change', () => {
            let v = parseInt($cant.value || '0', 10);
            if (isNaN(v) || v < 1) v = 1;
            $cant.value = v;
        });
    }




    // ----- Submit (Create) -----
    if (form) {
        form.addEventListener('submit', (ev) => {
            const spanLinea = document.querySelector('[data-valmsg-for="Lineas"]');
            if (spanLinea) spanLinea.textContent = '';

            const rows = $qa('#tblDetalle tbody tr');
            if (rows.length === 0) {
                ev.preventDefault();
                if (spanLinea) spanLinea.textContent = 'Debe agregar al menos un producto al pedido.';
                swalErr('Debe agregar al menos un producto al pedido.');
                unlockSave('#btn-guardar');     // Cambio
                return false;
            }

            for (const r of rows) {
                const $cant = r.querySelector('input[data-field="cantidad"]');
                let v = parseInt($cant.value || '0', 10);
                if (isNaN(v) || v < 1) {
                    ev.preventDefault();
                    swalErr('Todas las cantidades deben ser enteros positivos.');
                    unlockSave('#btn-guardar');  // Cambio
                    return false;
                }
            }

            reindexHiddenInputsCreate();
            return true;
        });
    }

    function reindexHiddenInputsCreate() {
        $qa('input[type="hidden"][data-dynamic="1"]').forEach(x => x.remove());

        const rows = $qa('#tblDetalle tbody tr');
        rows.forEach((r, i) => {
            const id = r.dataset.id;
            const cantidad = parseInt(r.querySelector('input[data-field="cantidad"]').value || '0', 10);

            const h = (n, v) => {
                const inp = document.createElement('input');
                inp.type = 'hidden'; inp.name = n; inp.value = v; inp.setAttribute('data-dynamic', '1'); form.appendChild(inp);
            };
            h(`Lineas[${i}].ProductoId`, id);
            h(`Lineas[${i}].Cantidad`, cantidad.toString());
        });
    }
});

// ====== EDIT ======
// ====== EDIT ======
document.addEventListener('DOMContentLoaded', () => {
    if (!window.PAGE || window.PAGE.module !== 'Pedidos' || window.PAGE.view !== 'Edit') return;

    // Helpers locales
    const $q = (sel, root = document) => root.querySelector(sel);
    const $qa = (sel, root = document) => Array.from(root.querySelectorAll(sel));
    const ht = (s) => (s ?? '').toString()
        .replaceAll('&', '&amp;').replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;').replaceAll('"', '&quot;');

    // Limpia overlay/spinner sin importar quién lo creó
    function cleanupOverlay() {
        try {
            if (window.KaryForms?.unlock) window.KaryForms.unlock('#btn-guardar');
            if (window.KaryForms?.hideOverlay) window.KaryForms.hideOverlay();
        } catch { /* noop */ }

        document.querySelectorAll(
            '.kary-forms-overlay, #kary-forms-overlay, .ak-overlay, .ak-loading-backdrop'
        ).forEach(el => el.remove());

        document.body.classList.remove('ak-busy', 'is-submitting');

        const b = document.querySelector('#btn-guardar');
        if (b) { b.disabled = false; b.removeAttribute('aria-busy'); b.classList.remove('is-loading'); }
    }

    // SweetAlert que devuelve promesa (para poder .then(cleanupOverlay))
    const swalErr = (text) => {
        if (window.Swal) return Swal.fire({ icon: 'error', title: 'Validación', text });
        alert(text);
        return Promise.resolve();
    };

    // DOM base
    const cfg = window.PEDIDOS || {};
    const form = $q('#frmPedidoEdit');
    const btn = $q('#btn-guardar');
    const tbody = $q('#tblDetalle tbody');
    const txtBuscar = $q('#txtBuscarProducto');
    const boxSug = $q('#sugerencias');

    // Guardado seguro / bloqueo doble submit / proteger salida
    if (form) {
        if (window.KaryForms?.bindSafeSubmit) {
            KaryForms.bindSafeSubmit('#frmPedidoEdit', '#btn-guardar', {
                spinnerHtml: '<i class="fa-solid fa-spinner fa-spin ak-icon"></i> Guardando...'
            });
        } else {
            form.addEventListener('submit', () => { if (btn) btn.disabled = true; });
        }
        if (window.KarySwal?.guardUnsaved) {
            KarySwal.guardUnsaved('#frmPedidoEdit', '.js-leave');
        }
    }

    // Autocomplete (agregar nuevas líneas)
    if (txtBuscar && boxSug && cfg?.urls?.buscar) {
        let lastQ = '';
        txtBuscar.addEventListener('input', async (e) => {
            const q = (e.target.value || '').trim();
            if (q.length < 2) { boxSug.classList.add('d-none'); boxSug.innerHTML = ''; return; }
            if (q === lastQ) return; lastQ = q;

            try {
                const res = await fetch(`${cfg.urls.buscar}?q=${encodeURIComponent(q)}&top=20`);
                const list = await res.json();
                renderSuggest(list);
            } catch (err) { console.error(err); }
        });

        document.addEventListener('click', (ev) => {
            if (!boxSug.contains(ev.target) && ev.target !== txtBuscar) boxSug.classList.add('d-none');
        });
    }

    function renderSuggest(list) {
        if (!list || list.length === 0) { boxSug.classList.add('d-none'); boxSug.innerHTML = ''; return; }
        boxSug.innerHTML = list.map(p => `
      <div class="k-suggest-item"
           data-id="${ht(p.productoId)}"
           data-cod="${ht(p.codigoProducto)}"
           data-nom="${ht(p.nombreProducto)}"
           data-desc="${ht(p.descripcionProducto || '')}"
           data-img="${ht(p.imagenUrl || '')}">
        <img class="k-img" src="${ht(p.imagenUrl || '')}" onerror="this.src='';" alt="">
        <div>
          <div><strong>${ht(p.nombreProducto)}</strong></div>
          <div class="text-muted" style="font-size:12px">${ht(p.productoId)} — ${ht(p.descripcionProducto || '')}</div>
        </div>
      </div>`).join('');
        boxSug.classList.remove('d-none');

        $qa('.k-suggest-item', boxSug).forEach(item => {
            item.addEventListener('click', () => {
                const prod = {
                    id: item.dataset.id,
                    codigo: item.dataset.cod,
                    nombre: item.dataset.nom,
                    descripcion: item.dataset.desc,
                    img: item.dataset.img
                };
                addLineOrIncrementEdit(prod);
                boxSug.classList.add('d-none'); boxSug.innerHTML = ''; txtBuscar.value = '';
            });
        });
    }

    // Agregar/sumar línea
    function addLineOrIncrementEdit(p) {
        if (!p?.id) { swalErr('Producto inválido'); return; }

        let row = tbody.querySelector(`tr[data-id="${p.id}"]`);
        if (row) {
            const $cant = row.querySelector('input[data-field="cantidad"]');
            $cant.value = (parseInt($cant.value || '0', 10) || 0) + 1;
            return;
        }

        const tr = document.createElement('tr');
        tr.dataset.id = p.id;
        tr.dataset.detid = '';
        tr.innerHTML = `
      <td><span data-field="codigo">${ht(p.id)}</span></td>
      <td><span data-field="nombre">${ht(p.nombre)}</span></td>
      <td><span data-field="desc">${ht(p.descripcion)}</span></td>
      <td><img class="k-img" src="${ht(p.img || '')}" onerror="this.src='';" alt=""></td>
      <td><input type="number" class="form-control form-control-sm" value="1" min="1" step="1" data-field="cantidad"></td>
      <td><input type="number" class="form-control form-control-sm" value="0" min="0" step="0.01" data-field="precio-pedido"></td>
      <td><input type="number" class="form-control form-control-sm" value="0" min="0" step="0.01" data-field="precio-venta"></td>
       <!-- NUEVA COLUMNA: Lote -->
        <td><input type="text"   class="form-control form-control-sm" maxlength="50" data-field="lote"></td>
      <td><input type="date"   class="form-control form-control-sm" data-field="vencimiento"></td>
      <td class="text-center"><button type="button" class="k-del" title="Quitar">&times;</button></td>
    `;
        tbody.appendChild(tr);
    }

    // Delegaciones (quitar / validar cantidad)
    tbody.addEventListener('click', (ev) => {
        const btn = ev.target.closest('.k-del');
        if (!btn) return;
        const tr = btn.closest('tr');
        if (tr) { tr.remove(); reindexHiddenInputsEdit(); }
    });

    tbody.addEventListener('change', (ev) => {
        const inp = ev.target.closest('input[data-field="cantidad"]');
        if (!inp) return;
        let v = parseInt(inp.value || '0', 10);
        if (isNaN(v) || v < 1) v = 1;
        inp.value = v;
    });

    // Submit: validar + reinyectar (con capture y stopImmediatePropagation)
    if (form) {
        form.addEventListener('submit', (ev) => {
            const rows = $qa('#tblDetalle tbody tr');
            const spanLinea = document.querySelector('[data-valmsg-for="Lineas"]');
            if (spanLinea) spanLinea.textContent = '';

            // a) Debe existir al menos una línea
            if (rows.length === 0) {
                ev.preventDefault();
                ev.stopImmediatePropagation();
                if (spanLinea) spanLinea.textContent = 'Debe agregar al menos un producto.';
                swalErr('Debe agregar al menos un producto.').then(() => cleanupOverlay());
                return false;
            }

            // b) Validaciones por fila
            for (const r of rows) {
                const c = r.querySelector('input[data-field="cantidad"]');
                let v = parseInt(c.value || '0', 10);
                if (isNaN(v) || v < 1) {
                    ev.preventDefault();
                    ev.stopImmediatePropagation();
                    swalErr('Todas las cantidades deben ser enteros positivos.').then(() => cleanupOverlay());
                    return false;
                }
                const pc = parseFloat(r.querySelector('input[data-field="precio-pedido"]').value || '0');
                const pv = parseFloat(r.querySelector('input[data-field="precio-venta"]').value || '0');
                if (pc < 0 || pv < 0) {
                    ev.preventDefault();
                    ev.stopImmediatePropagation();
                    swalErr('Precios no pueden ser negativos.').then(() => cleanupOverlay());
                    return false;
                }
            }

            // c) Ok -> reinyectar y permitir submit
            reindexHiddenInputsEdit();
            return true;
        }, { capture: true }); // corre antes que el submit handler de KaryForms
    }

    function reindexHiddenInputsEdit() {
        $qa('input[type="hidden"][data-dynamic="1"]').forEach(x => x.remove());
        const rows = $qa('#tblDetalle tbody tr');
        rows.forEach((r, i) => {
            const detId = r.dataset.detid || '';
            const prod = r.dataset.id;
            const cant = parseInt(r.querySelector('input[data-field="cantidad"]').value || '0', 10);
            const pPed = r.querySelector('input[data-field="precio-pedido"]').value || '0';
            const pVen = r.querySelector('input[data-field="precio-venta"]').value || '0';

            let lote = (r.querySelector('input[data-field="lote"]')?.value || '').trim().toUpperCase();
            if (lote.length > 50) lote = lote.substring(0, 50);

            const fv = r.querySelector('input[data-field="vencimiento"]').value || '';

            const h = (name, val) => {
                const inp = document.createElement('input');
                inp.type = 'hidden'; inp.name = name; inp.value = val ?? '';
                inp.setAttribute('data-dynamic', '1');
                form.appendChild(inp);
            };

            h(`Lineas[${i}].DetallePedidoId`, detId);
            h(`Lineas[${i}].ProductoId`, prod);
            h(`Lineas[${i}].Cantidad`, String(cant));
            h(`Lineas[${i}].PrecioPedido`, pPed);
            h(`Lineas[${i}].PrecioVenta`, pVen);
            // NUEVO: enviar a servidor
            h(`Lineas[${i}].LoteCodigo`, lote);
            h(`Lineas[${i}].FechaVencimiento`, fv);
        });
    }

    // Modales PRG (éxito / sin cambios)
    if (cfg.updatedOk && window.KarySwal?.saveSuccess) {
        KarySwal.saveSuccess({
            icon: 'success',
            title: '¡Actualizado exitosamente!',
            text: `Los cambios de "${cfg.updatedName || cfg.pedidoId}" se guardaron correctamente.`,
            confirmText: 'Aceptar',
            showDenyButton: false,
            indexUrl: cfg.urls.index
        });
    } else if (cfg.noChanges && window.KarySwal?.info) {
        KarySwal.info({
            title: 'Sin cambios',
            text: 'No se realizó ninguna modificación.',
            confirmText: 'Aceptar',
            redirectUrl: cfg.urls.index
        });
    }





});


// === Guard de CLICK (captura) para frenar el overlay de KaryForms ===
(function () {
    const form = document.querySelector('#frmPedidoEdit');
    const btn = document.querySelector('#btn-guardar');
    if (!form || !btn) return;

    const $qa = (sel, root = document) => Array.from((root || document).querySelectorAll(sel));

    // Muestra el mensaje de "mínimo una línea"
    function showNoLinesError() {
        const span = document.querySelector('[data-valmsg-for="Lineas"]');
        if (span) span.textContent = 'Debe agregar al menos un producto.';
        if (window.Swal) {
            return Swal.fire({ icon: 'error', title: 'Validación', text: 'Debe agregar al menos un producto.' });
        } else {
            alert('Debe agregar al menos un producto.');
            return Promise.resolve();
        }
    }

    // 1) Antes de que KaryForms procese el click del botón, validamos que haya filas
    btn.addEventListener('click', (ev) => {
        const rows = $qa('#tblDetalle tbody tr', document);
        if (rows.length === 0) {
            ev.preventDefault();
            ev.stopImmediatePropagation();   // <- cancela cualquier handler (incluido KaryForms)
            showNoLinesError().then(() => {
                // por si KaryForms ya bloqueó algo, desbloqueamos agresivo
                try {
                    if (window.KaryForms?.unlock) window.KaryForms.unlock('#btn-guardar');
                    if (window.KaryForms?.hideOverlay) window.KaryForms.hideOverlay();
                } catch { }
                document.querySelectorAll(
                    '.kary-forms-overlay, #kary-forms-overlay, .ak-overlay, .ak-loading-backdrop'
                ).forEach(el => el.remove());
                document.body.classList.remove('ak-busy', 'is-submitting');
                btn.disabled = false; btn.removeAttribute('aria-busy'); btn.classList.remove('is-loading');
            });
        }
    }, { capture: true }); // << clave: corre ANTES que KaryForms

    // 2) (opcional) si quieres también frenar otros errores rápidos antes del overlay:
    form.addEventListener('submit', (ev) => {
        const rows = $qa('#tblDetalle tbody tr');
        if (rows.length === 0) {
            ev.preventDefault();
            ev.stopImmediatePropagation();
            showNoLinesError();
            return false;
        }
        return true;
    }, { capture: true });
})();

(function () {
    const ddl = document.getElementById("EstadoPedidoId"); // asegúrese que el asp-for renderizó este id
    const nota = document.getElementById("nota-recibido");
    function toggleNota() {
        if (!ddl || !nota) return;
        const texto = (ddl.options[ddl.selectedIndex]?.text || "").trim().toUpperCase();
        nota.style.display = (texto === "RECIBIDO") ? "inline" : "none";
    }
    toggleNota();
    if (ddl) ddl.addEventListener("change", toggleNota);
})();