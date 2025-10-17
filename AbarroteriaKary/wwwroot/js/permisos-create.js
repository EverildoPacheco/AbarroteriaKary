
//// wwwroot/js/permisos-create.js
//(function () {
//    'use strict';
//    if (!document.querySelector('.page-create')) return;

//    // ---------- refs ----------
//    const ddlRol = document.getElementById('ddlRol');
//    const ddlModulo = document.getElementById('ddlModulo');
//    const modList = document.getElementById('modList');
//    const accList = document.getElementById('submodsList');
//    const lbl = document.getElementById('lblContexto');
//    const frm = document.getElementById('frmBulk');
//    const hidRol = document.getElementById('hidRol');
//    const hidModulo = document.getElementById('hidModulo'); // sin name
//    const btnGuardar = document.getElementById('btn-guardar');
//    const errBox = document.getElementById('errorBox');
//    const loadState = document.getElementById('loadState');
//    const multiBox = document.getElementById('multiPayload');

//    // ---------- store (estado acumulado por módulo) ----------
//    // store[modId] = { items: [{ id, text, exists, esModulo, ver, crear, editar, eliminar }] }
//    const store = Object.create(null);

//    // ---------- utils ----------
//    function enforceVerRow(row) {
//        const ver = row.querySelector('.chk-ver');
//        if (!ver) return;
//        const any = row.querySelector('.chk-crear')?.checked
//            || row.querySelector('.chk-editar')?.checked
//            || row.querySelector('.chk-eliminar')?.checked;
//        if (any) ver.checked = true;
//    }

//    const stopGlobalProcessing = () => {
//        if (loadState) loadState.hidden = true;
//        if (btnGuardar) {
//            btnGuardar.classList.remove('is-loading');
//            btnGuardar.removeAttribute('aria-busy');
//            btnGuardar.disabled = false;
//        }
//        try { window.KarySwal?.unblock?.(); } catch { }
//        try { window.KarySwal?.stopProcessing?.(); } catch { }
//        try { if (window.Swal?.isVisible?.()) window.Swal.close(); } catch { }
//    };

//    const showErr = (msg) => {
//        stopGlobalProcessing();
//        if (errBox) {
//            errBox.textContent = msg;
//            errBox.classList.remove('d-none');
//            try { errBox.scrollIntoView({ behavior: 'smooth', block: 'center' }); } catch { }
//        }
//        if (window.KarySwal?.toastWarn) window.KarySwal.toastWarn(msg);
//    };

//    function countSelections() {
//        let total = 0;
//        for (const modId in store) {
//            const st = store[modId];
//            if (!st?.items) continue;
//            total += st.items.filter(it =>
//                !it.exists && (it.ver || it.crear || it.editar || it.eliminar)
//            ).length;
//        }
//        return total;
//    }

//    function isValid() {
//        if (errBox) errBox.classList.add('d-none');
//        if (!ddlRol.value) {
//            showErr('Seleccione un Rol.');
//            return false;
//        }
//        if (Object.keys(store).length === 0) {
//            showErr('Seleccione al menos un módulo y marque alguna acción.');
//            return false;
//        }
//        if (countSelections() === 0) {
//            showErr('Marque al menos una acción en uno o más submódulos.');
//            return false;
//        }
//        return true;
//    }

//    // ---------- seed ----------
//    function seedModuleFromServer(modId, list) {
//        // si ya existe, no lo sobreescribas (conservar selección previa)
//        if (!store[modId]) {
//            store[modId] = {
//                items: list.map(it => ({
//                    id: (it.value ?? ''),         // "" => nivel módulo
//                    text: it.text || '',
//                    exists: !!it.exists,
//                    esModulo: !it.value || it.value === '',
//                    ver: !!it.ver,
//                    crear: !!it.crear,
//                    editar: !!it.editar,
//                    eliminar: !!it.eliminar
//                }))
//            };
//        } else {
//            // merge suave: si server trae nuevos submódulos, añádelos
//            const map = new Map(store[modId].items.map(x => [x.id, x]));
//            for (const it of list) {
//                const id = it.value ?? '';
//                if (!map.has(id)) {
//                    store[modId].items.push({
//                        id,
//                        text: it.text || '',
//                        exists: !!it.exists,
//                        esModulo: !it.value || it.value === '',
//                        ver: !!it.ver,
//                        crear: !!it.crear,
//                        editar: !!it.editar,
//                        eliminar: !!it.eliminar
//                    });
//                }
//            }
//        }
//    }

//    // ---------- render ----------
//    function renderFromStore(modId) {
//        const st = store[modId];
//        if (!st || !Array.isArray(st.items)) {
//            accList.innerHTML = '<p class="text-muted">No hay submódulos disponibles</p>';
//            return;
//        }

//        accList.innerHTML = '';
//        st.items.forEach((it, idx) => {
//            const wrap = document.createElement('div');
//            wrap.className = 'acc-item';
//            wrap.dataset.idx = String(idx);

//            const hd = document.createElement('button');
//            hd.type = 'button';
//            hd.className = 'acc-hd';
//            hd.innerHTML = `
//        <span class="acc-title">${it.text}</span>
//        ${it.exists ? '<span class="badge text-bg-secondary ms-2">Ya asignado</span>' : ''}
//        <i class="bi bi-chevron-down ms-auto acc-ic"></i>
//      `;
//            wrap.appendChild(hd);

//            const body = document.createElement('div');
//            body.className = 'acc-body';
//            body.hidden = true;

//            const dis = it.exists ? 'disabled' : '';
//            body.innerHTML = `
//        <div class="ops-grid">
//          <label class="form-check d-flex align-items-center gap-2">
//            <input type="checkbox" class="form-check-input chk-ver" ${it.ver ? 'checked' : ''} ${dis}>
//            <span>Ver</span>
//          </label>
//          <label class="form-check d-flex align-items-center gap-2">
//            <input type="checkbox" class="form-check-input chk-crear" ${it.crear ? 'checked' : ''} ${dis}>
//            <span>Crear</span>
//          </label>
//          <label class="form-check d-flex align-items-center gap-2">
//            <input type="checkbox" class="form-check-input chk-editar" ${it.editar ? 'checked' : ''} ${dis}>
//            <span>Editar</span>
//          </label>
//          <label class="form-check d-flex align-items-center gap-2">
//            <input type="checkbox" class="form-check-input chk-eliminar" ${it.eliminar ? 'checked' : ''} ${dis}>
//            <span>Eliminar</span>
//          </label>
//        </div>
//      `;
//            wrap.appendChild(body);
//            accList.appendChild(wrap);

//            // Toggle acordeón
//            hd.addEventListener('click', () => {
//                const isHidden = body.hidden;
//                body.hidden = !isHidden;
//                wrap.classList.toggle('open', isHidden);
//            });

//            // Actualizar store al cambiar
//            const setAndSync = () => {
//                enforceVerRow(body);
//                const v = !!body.querySelector('.chk-ver')?.checked;
//                const c = !!body.querySelector('.chk-crear')?.checked;
//                const e = !!body.querySelector('.chk-editar')?.checked;
//                const d = !!body.querySelector('.chk-eliminar')?.checked;
//                const cur = store[modId].items[idx];
//                cur.ver = v; cur.crear = c; cur.editar = e; cur.eliminar = d;
//            };
//            body.querySelector('.chk-ver')?.addEventListener('change', setAndSync);
//            body.querySelector('.chk-crear')?.addEventListener('change', setAndSync);
//            body.querySelector('.chk-editar')?.addEventListener('change', setAndSync);
//            body.querySelector('.chk-eliminar')?.addEventListener('change', setAndSync);

//            // Réplica si es “Permiso a TODO el módulo”
//            if (it.esModulo && !it.exists) {
//                const replicate = () => {
//                    enforceVerRow(body);
//                    const v = !!body.querySelector('.chk-ver')?.checked;
//                    const c = !!body.querySelector('.chk-crear')?.checked;
//                    const e = !!body.querySelector('.chk-editar')?.checked;
//                    const d = !!body.querySelector('.chk-eliminar')?.checked;

//                    store[modId].items.forEach((row, jdx) => {
//                        if (jdx === idx) return;
//                        if (row.exists) return; // no tocar ya asignados
//                        row.ver = v; row.crear = c; row.editar = e; row.eliminar = d;

//                        // reflejar en DOM si esa fila está renderizada
//                        const otherBody = accList.querySelector(`.acc-item[data-idx="${jdx}"] .acc-body`);
//                        if (otherBody) {
//                            const set = (sel, val) => { const cb = otherBody.querySelector(sel); if (cb) cb.checked = val; };
//                            set('.chk-ver', v); set('.chk-crear', c); set('.chk-editar', e); set('.chk-eliminar', d);
//                        }
//                    });
//                };
//                body.querySelectorAll('.chk-ver,.chk-crear,.chk-editar,.chk-eliminar')
//                    .forEach(cb => cb.addEventListener('change', replicate));
//            }
//        });
//    }

//    // ---------- carga submódulos de un módulo ----------
//    async function loadSubmods(modId) {
//        const rolId = ddlRol.value;
//        if (!rolId || !modId) return;

//        lbl.textContent = 'Cargando…';
//        accList.innerHTML = '';
//        hidRol.value = rolId;
//        hidModulo.value = modId; // solo contextual

//        // Si ya tenemos estado del módulo => render inmediato
//        if (store[modId]) {
//            renderFromStore(modId);
//            const modText = ddlModulo?.selectedOptions?.[0]?.text
//                || (modList?.querySelector(`[data-id="${CSS.escape(modId)}"] span`)?.textContent ?? '');
//            lbl.textContent = `${modText} • ${ddlRol.selectedOptions[0]?.text ?? ''}`;
//            return;
//        }

//        try {
//            const url = `/Permisos/SubmodsEstado?moduloId=${encodeURIComponent(modId)}&rolId=${encodeURIComponent(rolId)}`;
//            const resp = await fetch(url);
//            if (!resp.ok) { lbl.textContent = 'Error al cargar submódulos'; return; }
//            const data = await resp.json(); // [{value,text,exists,ver,crear,editar,eliminar}]
//            seedModuleFromServer(modId, data);
//            renderFromStore(modId);
//            const modText = ddlModulo?.selectedOptions?.[0]?.text
//                || (modList?.querySelector(`[data-id="${CSS.escape(modId)}"] span`)?.textContent ?? '');
//            lbl.textContent = `${modText} • ${ddlRol.selectedOptions[0]?.text ?? ''}`;
//        } catch {
//            lbl.textContent = 'Error al cargar submódulos';
//        }
//    }

//    // ---------- construir payload MULTI para el POST ----------
//    function buildMultiPayload() {
//        multiBox.innerHTML = ''; // limpiar

//        // armar Modules[i] para cada módulo en el store que tenga algo marcado
//        const selectedModules = [];
//        for (const modId in store) {
//            const st = store[modId];
//            if (!st?.items) continue;
//            const items = st.items
//                .filter(it => !it.exists && (it.ver || it.crear || it.editar || it.eliminar))
//                .map(it => ({
//                    subId: it.id || '',
//                    ver: !!it.ver, crear: !!it.crear, editar: !!it.editar, eliminar: !!it.eliminar
//                }));
//            if (items.length > 0) selectedModules.push({ modId, items });
//        }

//        // generar inputs hidden con la forma que espera el binder
//        selectedModules.forEach((m, i) => {
//            const add = (name, val) => {
//                const input = document.createElement('input');
//                input.type = 'hidden';
//                input.name = name;
//                input.value = String(val);
//                multiBox.appendChild(input);
//            };

//            add(`Modules[${i}].ModuloId`, m.modId);
//            // Opcional: Index de Items (no es obligatorio si usas Items[0..n] contiguos)
//            m.items.forEach((it, j) => {
//                add(`Modules[${i}].Items.Index`, j);
//                add(`Modules[${i}].Items[${j}].SubmoduloId`, it.subId);
//                add(`Modules[${i}].Items[${j}].Ver`, it.ver ? 'true' : 'false');
//                add(`Modules[${i}].Items[${j}].Crear`, it.crear ? 'true' : 'false');
//                add(`Modules[${i}].Items[${j}].Editar`, it.editar ? 'true' : 'false');
//                add(`Modules[${i}].Items[${j}].Eliminar`, it.eliminar ? 'true' : 'false');
//                // YaAsignado no hace falta (ya filtramos exists), pero si tu VM lo requiere:
//                // add(`Modules[${i}].Items[${j}].YaAsignado`, 'false');
//            });
//        });
//    }

//    // ---------- eventos selección ----------
//    modList?.querySelectorAll('.mod-item').forEach(li => {
//        li.addEventListener('click', () => {
//            modList.querySelectorAll('.mod-item').forEach(x => x.classList.remove('active'));
//            li.classList.add('active');
//            ddlModulo.value = li.dataset.id;
//            loadSubmods(li.dataset.id);
//        });
//    });

//    ddlRol?.addEventListener('change', () => {
//        // reset si cambia rol
//        for (const k in store) delete store[k];
//        if (ddlModulo) ddlModulo.disabled = !ddlRol.value;

//        if (!ddlRol.value) {
//            if (ddlModulo) ddlModulo.value = '';
//            accList.innerHTML = '';
//            lbl.textContent = 'Seleccione rol y módulo…';
//            return;
//        }
//        if (ddlModulo?.value) loadSubmods(ddlModulo.value);
//    });

//    ddlModulo?.addEventListener('change', () => {
//        if (ddlModulo.value) loadSubmods(ddlModulo.value);
//    });

//    document.addEventListener('DOMContentLoaded', () => {
//        if (ddlModulo) ddlModulo.disabled = !ddlRol.value;
//        if (!ddlRol.value || !ddlModulo.value) {
//            accList.innerHTML = '';
//            lbl.textContent = 'Seleccione rol y módulo…';
//        }
//    });

//    // ---------- validación + submit ----------
//    btnGuardar?.addEventListener('click', (e) => {
//        if (!isValid()) {
//            e.preventDefault();
//            e.stopPropagation();
//            e.stopImmediatePropagation();
//            return false;
//        }
//        // construir payload multi-módulo ANTES de enviar
//        buildMultiPayload();

//        if (loadState) loadState.hidden = false;
//        btnGuardar.setAttribute('aria-busy', 'true');
//        btnGuardar.classList.add('is-loading');
//    }, { capture: true });

//    frm?.addEventListener('submit', (e) => {
//        if (!isValid()) {
//            e.preventDefault();
//            e.stopPropagation();
//            e.stopImmediatePropagation();
//            return false;
//        }
//        // evitar doble-submit
//        btnGuardar.disabled = true;
//    }, { capture: true });
//})();


// wwwroot/js/permisos-create.js
(function () {
    'use strict';
    if (!document.querySelector('.page-create')) return;

    // ---------- refs ----------
    const ddlRol = document.getElementById('ddlRol');
    const ddlModulo = document.getElementById('ddlModulo');
    const modList = document.getElementById('modList');
    const accList = document.getElementById('submodsList');
    const lbl = document.getElementById('lblContexto');
    const frm = document.getElementById('frmBulk');
    const hidRol = document.getElementById('hidRol');
    const hidModulo = document.getElementById('hidModulo'); // sin name
    const btnGuardar = document.getElementById('btn-guardar');
    const errBox = document.getElementById('errorBox');
    const loadState = document.getElementById('loadState');
    const multiBox = document.getElementById('multiPayload');

    // ---------- store ----------
    // store[modId] = { items: [{ id, text, exists, esModulo, ver, crear, editar, eliminar }] }
    const store = Object.create(null);

    // módulos ya asignados (servidor) para el rol actual
    let modsAssigned = new Set();

    // ---------- helpers visuales ----------
    function ensureModBadge(li, show) {
        if (!li) return;
        let badge = li.querySelector('.mod-badge');
        if (show) {
            if (!badge) {
                const title = li.querySelector('span');
                badge = document.createElement('span');
                badge.className = 'mod-badge';
                badge.textContent = 'Asignado';
                (title || li).insertAdjacentElement('afterend', badge);
            }
            li.classList.add('assigned');
        } else {
            if (badge) badge.remove();
            li.classList.remove('assigned');
        }
    }

    function paintAssignedModulesOnList() {
        if (!modList) return;
        modList.querySelectorAll('.mod-item').forEach(li => {
            const id = li.dataset.id || '';
            ensureModBadge(li, modsAssigned.has(id));
        });
    }

    function refreshModuleBadge(modId) {
        const st = store[modId];
        const anySelected = !!st?.items?.some(it =>
            (!it.exists) && (it.ver || it.crear || it.editar || it.eliminar)
        );
        const li = modList?.querySelector(`.mod-item[data-id="${CSS.escape(modId)}"]`);
        // Mostrar si: ya estaba asignado en BD o si hay selección pendiente en este módulo
        const show = (modsAssigned.has(modId) || anySelected);
        ensureModBadge(li, show);
    }

    function ensureRowBadge(hd, assigned) {
        if (!hd) return;
        hd.classList.toggle('assigned', !!assigned);
        let b = hd.querySelector('.badge');
        if (assigned) {
            if (!b) {
                b = document.createElement('span');
                b.className = 'badge ms-2';
                b.textContent = 'Asignado';
                hd.querySelector('.acc-title')?.insertAdjacentElement('afterend', b);
            } else {
                b.textContent = 'Asignado';
            }
        } else if (b) {
            b.remove();
        }
    }

    // ---------- utils ----------
    function enforceVerRow(row) {
        const ver = row.querySelector('.chk-ver');
        if (!ver) return;
        const any = row.querySelector('.chk-crear')?.checked
            || row.querySelector('.chk-editar')?.checked
            || row.querySelector('.chk-eliminar')?.checked;
        if (any) ver.checked = true;
    }

    const stopGlobalProcessing = () => {
        if (loadState) loadState.hidden = true;
        if (btnGuardar) {
            btnGuardar.classList.remove('is-loading');
            btnGuardar.removeAttribute('aria-busy');
            btnGuardar.disabled = false;
        }
        try { window.KarySwal?.unblock?.(); } catch { }
        try { window.KarySwal?.stopProcessing?.(); } catch { }
        try { if (window.Swal?.isVisible?.()) window.Swal.close(); } catch { }
    };

    const showErr = (msg) => {
        stopGlobalProcessing();
        if (errBox) {
            errBox.textContent = msg;
            errBox.classList.remove('d-none');
            try { errBox.scrollIntoView({ behavior: 'smooth', block: 'center' }); } catch { }
        }
        if (window.KarySwal?.toastWarn) window.KarySwal.toastWarn(msg);
    };

    function countSelections() {
        let total = 0;
        for (const modId in store) {
            const st = store[modId];
            if (!st?.items) continue;
            total += st.items.filter(it =>
                !it.exists && (it.ver || it.crear || it.editar || it.eliminar)
            ).length;
        }
        return total;
    }

    function isValid() {
        if (errBox) errBox.classList.add('d-none');
        if (!ddlRol.value) {
            showErr('Seleccione un Rol.');
            return false;
        }
        if (Object.keys(store).length === 0) {
            showErr('Seleccione al menos un módulo y marque alguna acción.');
            return false;
        }
        if (countSelections() === 0) {
            showErr('Marque al menos una acción en uno o más submódulos.');
            return false;
        }
        return true;
    }

    // ---------- seed ----------
    function seedModuleFromServer(modId, list) {
        if (!store[modId]) {
            store[modId] = {
                items: list.map(it => ({
                    id: (it.value ?? ''), // "" => nivel módulo
                    text: it.text || '',
                    exists: !!it.exists,
                    esModulo: !it.value || it.value === '',
                    ver: !!it.ver,
                    crear: !!it.crear,
                    editar: !!it.editar,
                    eliminar: !!it.eliminar
                }))
            };
        } else {
            const map = new Map(store[modId].items.map(x => [x.id, x]));
            for (const it of list) {
                const id = it.value ?? '';
                if (!map.has(id)) {
                    store[modId].items.push({
                        id,
                        text: it.text || '',
                        exists: !!it.exists,
                        esModulo: !it.value || it.value === '',
                        ver: !!it.ver,
                        crear: !!it.crear,
                        editar: !!it.editar,
                        eliminar: !!it.eliminar
                    });
                }
            }
        }
    }

    // ---------- render ----------
    function renderFromStore(modId) {
        const st = store[modId];
        if (!st || !Array.isArray(st.items)) {
            accList.innerHTML = '<p class="text-muted">No hay submódulos disponibles</p>';
            return;
        }

        accList.innerHTML = '';
        st.items.forEach((it, idx) => {
            const wrap = document.createElement('div');
            wrap.className = 'acc-item';
            wrap.dataset.idx = String(idx);

            const hd = document.createElement('button');
            hd.type = 'button';
            hd.className = 'acc-hd';

            const initiallyAssigned = !!(it.exists || it.ver || it.crear || it.editar || it.eliminar);
            hd.innerHTML = `
        <span class="acc-title">${it.text}</span>
        ${initiallyAssigned ? '<span class="badge ms-2">Asignado</span>' : ''}
        <i class="bi bi-chevron-down ms-auto acc-ic"></i>
      `;
            if (initiallyAssigned) hd.classList.add('assigned');
            wrap.appendChild(hd);

            const body = document.createElement('div');
            body.className = 'acc-body';
            body.hidden = true;

            const dis = it.exists ? 'disabled' : '';
            body.innerHTML = `
        <div class="ops-grid">
          <label class="form-check d-flex align-items-center gap-2">
            <input type="checkbox" class="form-check-input chk-ver" ${it.ver ? 'checked' : ''} ${dis}>
            <span>Ver</span>
          </label>
          <label class="form-check d-flex align-items-center gap-2">
            <input type="checkbox" class="form-check-input chk-crear" ${it.crear ? 'checked' : ''} ${dis}>
            <span>Crear</span>
          </label>
          <label class="form-check d-flex align-items-center gap-2">
            <input type="checkbox" class="form-check-input chk-editar" ${it.editar ? 'checked' : ''} ${dis}>
            <span>Editar</span>
          </label>
          <label class="form-check d-flex align-items-center gap-2">
            <input type="checkbox" class="form-check-input chk-eliminar" ${it.eliminar ? 'checked' : ''} ${dis}>
            <span>Eliminar</span>
          </label>
        </div>
      `;
            wrap.appendChild(body);
            accList.appendChild(wrap);

            // Toggle acordeón
            hd.addEventListener('click', () => {
                const isHidden = body.hidden;
                body.hidden = !isHidden;
                wrap.classList.toggle('open', isHidden);
            });

            // Actualizar store + chapa submódulo + chapa módulo
            const setAndSync = () => {
                enforceVerRow(body);
                const v = !!body.querySelector('.chk-ver')?.checked;
                const c = !!body.querySelector('.chk-crear')?.checked;
                const e = !!body.querySelector('.chk-editar')?.checked;
                const d = !!body.querySelector('.chk-eliminar')?.checked;

                const cur = store[modId].items[idx];
                cur.ver = v; cur.crear = c; cur.editar = e; cur.eliminar = d;

                const assigned = !!(cur.exists || v || c || e || d);
                ensureRowBadge(hd, assigned);
                refreshModuleBadge(modId);
            };

            body.querySelector('.chk-ver')?.addEventListener('change', setAndSync);
            body.querySelector('.chk-crear')?.addEventListener('change', setAndSync);
            body.querySelector('.chk-editar')?.addEventListener('change', setAndSync);
            body.querySelector('.chk-eliminar')?.addEventListener('change', setAndSync);

            // Réplica “Permiso a TODO el módulo”
            if (it.esModulo && !it.exists) {
                const replicate = () => {
                    enforceVerRow(body);
                    const v = !!body.querySelector('.chk-ver')?.checked;
                    const c = !!body.querySelector('.chk-crear')?.checked;
                    const e = !!body.querySelector('.chk-editar')?.checked;
                    const d = !!body.querySelector('.chk-eliminar')?.checked;

                    st.items.forEach((row, jdx) => {
                        if (jdx === idx) return;
                        if (row.exists) return; // no tocar ya asignados
                        row.ver = v; row.crear = c; row.editar = e; row.eliminar = d;

                        // reflejar en DOM si esa fila está renderizada
                        const other = accList.querySelector(`.acc-item[data-idx="${jdx}"]`);
                        if (other) {
                            const ob = other.querySelector('.acc-body');
                            if (ob) {
                                const set = (sel, val) => { const cb = ob.querySelector(sel); if (cb) cb.checked = val; };
                                set('.chk-ver', v); set('.chk-crear', c); set('.chk-editar', e); set('.chk-eliminar', d);
                            }
                            // chapa del submódulo replicado
                            const ohd = other.querySelector('.acc-hd');
                            ensureRowBadge(ohd, v || c || e || d || row.exists);
                        }
                    });

                    // Chapa del maestro y del módulo
                    ensureRowBadge(hd, v || c || e || d || it.exists);
                    refreshModuleBadge(modId);
                };
                body.querySelectorAll('.chk-ver,.chk-crear,.chk-editar,.chk-eliminar')
                    .forEach(cb => cb.addEventListener('change', replicate));
            }
        });

        // tras render, asegúrate de la chapa del módulo (por si vino todo "exists")
        refreshModuleBadge(modId);
    }

    // ---------- datos servidor ----------
    async function loadAssignedModulesForRole(rolId, { autoOpen = false } = {}) {
        modsAssigned = new Set();
        if (!rolId) { paintAssignedModulesOnList(); return []; }

        try {
            const resp = await fetch(`/Permisos/ModsAsignados?rolId=${encodeURIComponent(rolId)}`);
            if (resp.ok) {
                const data = await resp.json(); // ["CLIENTE","EMPLEADO",...]
                modsAssigned = new Set(Array.isArray(data) ? data : []);
            }
        } catch { /* noop */ }

        paintAssignedModulesOnList();

        if (autoOpen) {
            const firstAssigned = modList?.querySelector('.mod-item.assigned');
            if (firstAssigned) {
                modList.querySelectorAll('.mod-item').forEach(x => x.classList.remove('active'));
                firstAssigned.classList.add('active');
                if (ddlModulo) ddlModulo.value = firstAssigned.dataset.id || '';
                if (ddlModulo?.value) loadSubmods(ddlModulo.value);
            }
        }

        return Array.from(modsAssigned);
    }

    // ---------- carga submódulos ----------
    async function loadSubmods(modId) {
        const rolId = ddlRol.value;
        if (!rolId || !modId) return;

        lbl.textContent = 'Cargando…';
        accList.innerHTML = '';
        hidRol.value = rolId;
        hidModulo.value = modId;

        // si ya hay estado => render inmediato
        if (store[modId]) {
            renderFromStore(modId);
            const modText = ddlModulo?.selectedOptions?.[0]?.text
                || (modList?.querySelector(`[data-id="${CSS.escape(modId)}"] span`)?.textContent ?? '');
            lbl.textContent = `${modText} • ${ddlRol.selectedOptions[0]?.text ?? ''}`;
            return;
        }

        try {
            const url = `/Permisos/SubmodsEstado?moduloId=${encodeURIComponent(modId)}&rolId=${encodeURIComponent(rolId)}`;
            const resp = await fetch(url);
            if (!resp.ok) { lbl.textContent = 'Error al cargar submódulos'; return; }
            const data = await resp.json();
            seedModuleFromServer(modId, data);
            renderFromStore(modId);
            const modText = ddlModulo?.selectedOptions?.[0]?.text
                || (modList?.querySelector(`[data-id="${CSS.escape(modId)}"] span`)?.textContent ?? '');
            lbl.textContent = `${modText} • ${ddlRol.selectedOptions[0]?.text ?? ''}`;
        } catch {
            lbl.textContent = 'Error al cargar submódulos';
        }
    }

    // ---------- payload MULTI ----------
    function buildMultiPayload() {
        multiBox.innerHTML = ''; // limpiar

        const selectedModules = [];
        for (const modId in store) {
            const st = store[modId];
            if (!st?.items) continue;
            const items = st.items
                .filter(it => !it.exists && (it.ver || it.crear || it.editar || it.eliminar))
                .map(it => ({
                    subId: it.id || '',
                    ver: !!it.ver, crear: !!it.crear, editar: !!it.editar, eliminar: !!it.eliminar
                }));
            if (items.length > 0) selectedModules.push({ modId, items });
        }

        selectedModules.forEach((m, i) => {
            const add = (name, val) => {
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = name;
                input.value = String(val);
                multiBox.appendChild(input);
            };
            add(`Modules[${i}].ModuloId`, m.modId);
            m.items.forEach((it, j) => {
                add(`Modules[${i}].Items.Index`, j);
                add(`Modules[${i}].Items[${j}].SubmoduloId`, it.subId);
                add(`Modules[${i}].Items[${j}].Ver`, it.ver ? 'true' : 'false');
                add(`Modules[${i}].Items[${j}].Crear`, it.crear ? 'true' : 'false');
                add(`Modules[${i}].Items[${j}].Editar`, it.editar ? 'true' : 'false');
                add(`Modules[${i}].Items[${j}].Eliminar`, it.eliminar ? 'true' : 'false');
            });
        });
    }

    // ---------- eventos selección ----------
    modList?.querySelectorAll('.mod-item').forEach(li => {
        li.addEventListener('click', () => {
            modList.querySelectorAll('.mod-item').forEach(x => x.classList.remove('active'));
            li.classList.add('active');
            ddlModulo.value = li.dataset.id;
            loadSubmods(li.dataset.id);
        });
    });

    ddlRol?.addEventListener('change', async () => {
        // reset si cambia rol
        for (const k in store) delete store[k];
        if (ddlModulo) ddlModulo.disabled = !ddlRol.value;

        accList.innerHTML = '';
        lbl.textContent = ddlRol.value ? 'Seleccione un módulo…' : 'Seleccione rol y módulo…';

        if (!ddlRol.value) {
            if (ddlModulo) ddlModulo.value = '';
            modsAssigned = new Set();
            paintAssignedModulesOnList();
            return;
        }

        await loadAssignedModulesForRole(ddlRol.value, { autoOpen: true });
    });

    ddlModulo?.addEventListener('change', () => {
        if (ddlModulo.value) loadSubmods(ddlModulo.value);
    });

    document.addEventListener('DOMContentLoaded', () => {
        if (ddlModulo) ddlModulo.disabled = !ddlRol.value;

        // Si por PRG ya viene el rol, pinta chapas y abre el 1ro asignado
        if (ddlRol?.value) {
            loadAssignedModulesForRole(ddlRol.value, { autoOpen: true });
        }

        if (!ddlRol.value || !ddlModulo.value) {
            accList.innerHTML = '';
            lbl.textContent = 'Seleccione rol y módulo…';
        }
    });

    // ---------- validación + submit ----------
    btnGuardar?.addEventListener('click', (e) => {
        if (!isValid()) {
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            return false;
        }
        buildMultiPayload();
        if (loadState) loadState.hidden = false;
        btnGuardar.setAttribute('aria-busy', 'true');
        btnGuardar.classList.add('is-loading');
    }, { capture: true });

    frm?.addEventListener('submit', (e) => {
        if (!isValid()) {
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            return false;
        }
        btnGuardar.disabled = true;
    }, { capture: true });

})();
