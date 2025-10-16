
//// wwwroot/js/permisos-edit.js
//(function () {
//    'use strict';
//    if (!document.querySelector('.page-edit')) return;


//    const ddlRol = document.getElementById('ddlRol');
//    const ddlModulo = document.getElementById('ddlModulo');
//    const modList = document.getElementById('modList');
//    const accList = document.getElementById('submodsList');
//    const lbl = document.getElementById('lblContexto');
//    const frm = document.getElementById('frmBulk');
//    //const frm = document.getElementById('frmEditBulk');

//    const hidRol = document.getElementById('hidRol');
//    const hidModulo = document.getElementById('hidModulo');

//    let currentCtrl = null;
//    let loadSeq = 0;

//    const toBool = (v) => v === true || v === 'true' || v === 'True' || v === 1 || v === '1';

//    function enforceVer(container) {
//        const ver = container.querySelector('.chk-ver');
//        if (!ver) return;
//        const any = container.querySelector('.chk-crear')?.checked
//            || container.querySelector('.chk-editar')?.checked
//            || container.querySelector('.chk-eliminar')?.checked;
//        if (any) ver.checked = true;
//    }

//    function markTouched(container) {
//        const t = container.querySelector('input[name$=".Touched"]');
//        if (t) t.value = 'true';
//    }

//    function markModuleAssigned(modId, isAssigned) {
//        const li = modList?.querySelector(`.mod-item[data-id="${CSS.escape(modId)}"]`);
//        if (!li) return;
//        li.classList.toggle('assigned', !!isAssigned);
//        let tag = li.querySelector('.tag-assigned');
//        if (isAssigned) {
//            if (!tag) {
//                tag = document.createElement('span');
//                tag.className = 'tag-assigned';
//                tag.textContent = 'Asignado';
//                li.appendChild(tag);
//            }
//        } else {
//            tag?.remove();
//        }
//    }

//    // Recalcula si el MÓDULO queda “Asignado” mirando TODAS las filas visibles
//    function recomputeModuleAssigned() {
//        const any = Array.from(accList.querySelectorAll('.acc-body')).some(b => {
//            const q = (s) => b.querySelector(s)?.checked;
//            return q('.chk-ver') || q('.chk-crear') || q('.chk-editar') || q('.chk-eliminar');
//        });
//        const modId = ddlModulo?.value || hidModulo?.value || '';
//        if (modId) markModuleAssigned(modId, any);
//    }

//    // Actualiza el badge de UNA fila (y el módulo)
//    function updateRowBadgeAndModule(body) {
//        enforceVer(body);
//        markTouched(body);

//        const has = ['.chk-ver', '.chk-crear', '.chk-editar', '.chk-eliminar']
//            .some(sel => body.querySelector(sel)?.checked);

//        const hd = body.previousElementSibling; // el header de esa fila
//        hd.classList.toggle('assigned', has);

//        let badge = hd.querySelector('.badge');
//        if (has) {
//            if (!badge) {
//                badge = document.createElement('span');
//                badge.className = 'badge ms-2';
//                badge.textContent = 'Asignado';
//                hd.insertBefore(badge, hd.querySelector('.acc-ic'));
//            }
//        } else {
//            badge?.remove();
//        }

//        recomputeModuleAssigned();
//    }

//    // Precarga de módulos con permisos efectivos
//    (async function preloadAssignedModules() {
//        const rol = ddlRol?.disabled ? (hidRol?.value || '') : (ddlRol?.value || '');
//        if (!rol) return;
//        try {
//            const resp = await fetch(`/Permisos/ModsAsignados?rolId=${encodeURIComponent(rol)}`);
//            if (!resp.ok) return;
//            const mods = await resp.json();
//            if (Array.isArray(mods)) mods.forEach(id => markModuleAssigned(id, true));
//        } catch { /* noop */ }
//    })();

//    function renderAccordion(list) {
//        accList.innerHTML = '';
//        if (!Array.isArray(list) || list.length === 0) {
//            accList.innerHTML = '<p class="text-muted">No hay submódulos disponibles</p>';
//            return;
//        }

//        let idx = 0;
//        list.forEach(it => {
//            const wrap = document.createElement('div');
//            wrap.className = 'acc-item';
//            wrap.dataset.idx = idx;

//            // ⚠️ Para el badge usamos “tiene permisos” (ver/crear/editar/eliminar), NO “existe fila”.
//            const hasPerm = toBool(it.ver) || toBool(it.crear) || toBool(it.editar) || toBool(it.eliminar);

//            const hd = document.createElement('button');
//            hd.type = 'button';
//            hd.className = 'acc-hd' + (hasPerm ? ' assigned' : '');
//            hd.innerHTML = `
//        <span class="acc-title">${it.text || 'Sin nombre'}</span>
//        ${hasPerm ? '<span class="badge ms-2">Asignado</span>' : ''}
//        <i class="bi bi-chevron-down ms-auto acc-ic"></i>
//      `;

//            const body = document.createElement('div');
//            body.className = 'acc-body';
//            body.hidden = true;

//            // Items.Index + patrón hidden(false) + checkbox(true)
//            body.innerHTML = `
//        <input type="hidden" name="Items.Index" value="${idx}">

//        <div class="ops-grid">
//          <label class="form-check d-flex align-items-center gap-2">
//            <input type="checkbox" class="form-check-input chk-ver"
//                   name="Items[${idx}].Ver" value="true" ${toBool(it.ver) ? 'checked' : ''} />
//            <span>Ver</span>
//            <input type="hidden" name="Items[${idx}].Ver" value="false">
//          </label>

//          <label class="form-check d-flex align-items-center gap-2">
//            <input type="checkbox" class="form-check-input chk-crear"
//                   name="Items[${idx}].Crear" value="true" ${toBool(it.crear) ? 'checked' : ''} />
//            <span>Crear</span>
//            <input type="hidden" name="Items[${idx}].Crear" value="false">
//          </label>

//          <label class="form-check d-flex align-items-center gap-2">
//            <input type="checkbox" class="form-check-input chk-editar"
//                   name="Items[${idx}].Editar" value="true" ${toBool(it.editar) ? 'checked' : ''} />
//            <span>Editar</span>
//            <input type="hidden" name="Items[${idx}].Editar" value="false">
//          </label>

//          <label class="form-check d-flex align-items-center gap-2">
//            <input type="checkbox" class="form-check-input chk-eliminar"
//                   name="Items[${idx}].Eliminar" value="true" ${toBool(it.eliminar) ? 'checked' : ''} />
//            <span>Eliminar</span>
//            <input type="hidden" name="Items[${idx}].Eliminar" value="false">
//          </label>
//        </div>

//        <input type="hidden" name="Items[${idx}].SubmoduloId"     value="${it.value || ''}">
//        <input type="hidden" name="Items[${idx}].SubmoduloNombre" value="${it.text || ''}">
//        <input type="hidden" name="Items[${idx}].YaAsignado"      value="${toBool(it.asignado) ? 'true' : 'false'}">
//        <input type="hidden" name="Items[${idx}].EsModulo"        value="${toBool(it.esModulo) ? 'true' : 'false'}">
//        <input type="hidden" name="Items[${idx}].Touched"         value="false">
//      `;

//            wrap.appendChild(hd);
//            wrap.appendChild(body);
//            accList.appendChild(wrap);

//            hd.addEventListener('click', () => {
//                const isHidden = body.hidden;
//                body.hidden = !isHidden;
//                wrap.classList.toggle('open', isHidden);
//            });

//            body.querySelectorAll('input[type=checkbox]').forEach(cb => {
//                cb.disabled = false;
//                cb.removeAttribute('disabled');
//                cb.style.pointerEvents = 'auto';
//                cb.style.opacity = '1';
//            });

//            const onOps = () => updateRowBadgeAndModule(body);
//            const onOnlyTouch = () => { markTouched(body); updateRowBadgeAndModule(body); };

//            body.querySelector('.chk-crear')?.addEventListener('change', onOps);
//            body.querySelector('.chk-editar')?.addEventListener('change', onOps);
//            body.querySelector('.chk-eliminar')?.addEventListener('change', onOps);
//            body.querySelector('.chk-ver')?.addEventListener('change', onOnlyTouch);

//            idx++;
//        });

//        // Al terminar de renderizar, sincroniza el estado del módulo
//        recomputeModuleAssigned();
//    }

//    async function loadSubmods() {
//        const rolId = ddlRol?.disabled ? (hidRol?.value || '') : (ddlRol?.value || hidRol?.value || '');
//        const modId = ddlModulo?.value || hidModulo?.value || '';

//        lbl.textContent = (!rolId || !modId) ? 'Seleccione rol y módulo…' : 'Cargando…';
//        accList.innerHTML = '';

//        if (hidRol) hidRol.value = rolId || '';
//        if (hidModulo) hidModulo.value = modId || '';
//        if (!rolId || !modId) return;

//        if (currentCtrl) currentCtrl.abort();
//        currentCtrl = new AbortController();
//        const mySeq = ++loadSeq;

//        try {
//            const url = `/Permisos/SubmodsDetalle?moduloId=${encodeURIComponent(modId)}&rolId=${encodeURIComponent(rolId)}`;
//            const resp = await fetch(url, { signal: currentCtrl.signal });
//            if (!resp.ok) { lbl.textContent = `Error (${resp.status}) al cargar submódulos`; return; }
//            const data = await resp.json();
//            if (mySeq !== loadSeq) return;

//            renderAccordion(data);

//            const rolText = ddlRol?.selectedOptions?.[0]?.text || '';
//            const modText = ddlModulo?.selectedOptions?.[0]?.text || '';
//            lbl.textContent = `${modText} • ${rolText}`;
//        } catch (err) {
//            if (err?.name !== 'AbortError') lbl.textContent = 'Error al cargar submódulos';
//        } finally {
//            if (mySeq === loadSeq) currentCtrl = null;
//        }
//    }

//    // Exponer para la carga inicial desde la vista
//    window.__PermisosEdit_load = loadSubmods;

//    // Click en la lista de módulos (izquierda)
//    modList?.querySelectorAll('.mod-item').forEach(li => {
//        li.addEventListener('click', () => {
//            modList.querySelectorAll('.mod-item').forEach(x => x.classList.remove('active'));
//            li.classList.add('active');
//            if (ddlModulo) ddlModulo.value = li.dataset.id;
//            loadSubmods();
//        });
//    });

//    ddlRol?.addEventListener('change', loadSubmods);
//    ddlModulo?.addEventListener('change', loadSubmods);

//    // Validación mínima al enviar
//    frm?.addEventListener('submit', e => {
//        const rol = ddlRol?.disabled ? (hidRol?.value || '') : (ddlRol?.value || '');
//        const mod = ddlModulo?.value || hidModulo?.value || '';
//        if (!rol || !mod) {
//            e.preventDefault();
//            const summary = document.getElementById('valSummary');
//            if (summary) {
//                summary.textContent = 'Seleccione Rol y Módulo.';
//                summary.classList.remove('d-none');
//                try { summary.scrollIntoView({ behavior: 'smooth', block: 'center' }); } catch { }
//            } else {
//                alert('Seleccione Rol y Módulo.');
//            }
//        }
//    });
//})();

// wwwroot/js/permisos-edit.js
(function () {
    'use strict';
    if (!document.querySelector('.page-edit')) return;

    const ddlRol = document.getElementById('ddlRol');
    const ddlModulo = document.getElementById('ddlModulo');
    const modList = document.getElementById('modList');
    const accList = document.getElementById('submodsList');
    const lbl = document.getElementById('lblContexto');
    const frm = document.getElementById('frmBulk');

    const hidRol = document.getElementById('hidRol');
    const hidModulo = document.getElementById('hidModulo');

    let currentCtrl = null;
    let loadSeq = 0;

    const toBool = (v) => v === true || v === 'true' || v === 'True' || v === 1 || v === '1';

    function enforceVer(container) {
        const ver = container.querySelector('.chk-ver');
        if (!ver) return;
        const any = container.querySelector('.chk-crear')?.checked
            || container.querySelector('.chk-editar')?.checked
            || container.querySelector('.chk-eliminar')?.checked;
        if (any) ver.checked = true;
    }

    function markTouched(container) {
        const t = container.querySelector('input[name$=".Touched"]');
        if (t) t.value = 'true';
    }

    function markModuleAssigned(modId, isAssigned) {
        const li = modList?.querySelector(`.mod-item[data-id="${CSS.escape(modId)}"]`);
        if (!li) return;
        li.classList.toggle('assigned', !!isAssigned);
        let tag = li.querySelector('.tag-assigned');
        if (isAssigned) {
            if (!tag) {
                tag = document.createElement('span');
                tag.className = 'tag-assigned';
                tag.textContent = 'Asignado';
                li.appendChild(tag);
            }
        } else {
            tag?.remove();
        }
    }

    // Recalcula si el MÓDULO queda “Asignado” mirando TODAS las filas visibles
    function recomputeModuleAssigned() {
        const any = Array.from(accList.querySelectorAll('.acc-body')).some(b => {
            const q = (s) => b.querySelector(s)?.checked;
            return q('.chk-ver') || q('.chk-crear') || q('.chk-editar') || q('.chk-eliminar');
        });
        const modId = ddlModulo?.value || hidModulo?.value || '';
        if (modId) markModuleAssigned(modId, any);
    }

    // Actualiza el badge de UNA fila (y el módulo)
    function updateRowBadgeAndModule(body) {
        enforceVer(body);
        markTouched(body);

        const has = ['.chk-ver', '.chk-crear', '.chk-editar', '.chk-eliminar']
            .some(sel => body.querySelector(sel)?.checked);

        const hd = body.previousElementSibling; // header de esa fila
        hd.classList.toggle('assigned', has);

        let badge = hd.querySelector('.badge');
        if (has) {
            if (!badge) {
                badge = document.createElement('span');
                badge.className = 'badge ms-2';
                badge.textContent = 'Asignado';
                hd.insertBefore(badge, hd.querySelector('.acc-ic'));
            }
        } else {
            badge?.remove();
        }

        recomputeModuleAssigned();
    }

    // ------- NUEVO: replicar desde la fila "Permiso a TODO el módulo" -------
    function replicateFromMaster(masterBody) {
        // Regla de Ver
        enforceVer(masterBody);

        const v = !!masterBody.querySelector('.chk-ver')?.checked;
        const c = !!masterBody.querySelector('.chk-crear')?.checked;
        const e = !!masterBody.querySelector('.chk-editar')?.checked;
        const d = !!masterBody.querySelector('.chk-eliminar')?.checked;

        Array.from(accList.querySelectorAll('.acc-body')).forEach(b => {
            if (b === masterBody) return;

            const set = (sel, val) => {
                const cb = b.querySelector(sel);
                if (!cb) return;
                cb.checked = val;
            };

            set('.chk-ver', v);
            set('.chk-crear', c);
            set('.chk-editar', e);
            set('.chk-eliminar', d);

            markTouched(b);
            updateRowBadgeAndModule(b);
        });

        // Tocar también la maestra y su badge
        markTouched(masterBody);
        updateRowBadgeAndModule(masterBody);
    }

    function attachMasterReplicationIfNeeded(body) {
        const esModulo = (body.querySelector('input[name$=".EsModulo"]')?.value || '').toLowerCase() === 'true';
        if (!esModulo) return;
        const handler = () => replicateFromMaster(body);
        body.querySelectorAll('.chk-ver,.chk-crear,.chk-editar,.chk-eliminar')
            .forEach(cb => cb.addEventListener('change', handler));
    }
    // -----------------------------------------------------------------------

    // Precarga de módulos con permisos efectivos
    (async function preloadAssignedModules() {
        const rol = ddlRol?.disabled ? (hidRol?.value || '') : (ddlRol?.value || '');
        if (!rol) return;
        try {
            const resp = await fetch(`/Permisos/ModsAsignados?rolId=${encodeURIComponent(rol)}`);
            if (!resp.ok) return;
            const mods = await resp.json();
            if (Array.isArray(mods)) mods.forEach(id => markModuleAssigned(id, true));
        } catch { /* noop */ }
    })();

    function renderAccordion(list) {
        accList.innerHTML = '';
        if (!Array.isArray(list) || list.length === 0) {
            accList.innerHTML = '<p class="text-muted">No hay submódulos disponibles</p>';
            return;
        }

        let idx = 0;
        list.forEach(it => {
            const wrap = document.createElement('div');
            wrap.className = 'acc-item';
            wrap.dataset.idx = idx;

            const hasPerm = toBool(it.ver) || toBool(it.crear) || toBool(it.editar) || toBool(it.eliminar);

            const hd = document.createElement('button');
            hd.type = 'button';
            hd.className = 'acc-hd' + (hasPerm ? ' assigned' : '');
            hd.innerHTML = `
        <span class="acc-title">${it.text || 'Sin nombre'}</span>
        ${hasPerm ? '<span class="badge ms-2">Asignado</span>' : ''}
        <i class="bi bi-chevron-down ms-auto acc-ic"></i>
      `;

            const body = document.createElement('div');
            body.className = 'acc-body';
            body.hidden = true;

            // Items.Index + patrón hidden(false) + checkbox(true)
            body.innerHTML = `
        <input type="hidden" name="Items.Index" value="${idx}">

        <div class="ops-grid">
          <label class="form-check d-flex align-items-center gap-2">
            <input type="checkbox" class="form-check-input chk-ver"
                   name="Items[${idx}].Ver" value="true" ${toBool(it.ver) ? 'checked' : ''} />
            <span>Ver</span>
            <input type="hidden" name="Items[${idx}].Ver" value="false">
          </label>

          <label class="form-check d-flex align-items-center gap-2">
            <input type="checkbox" class="form-check-input chk-crear"
                   name="Items[${idx}].Crear" value="true" ${toBool(it.crear) ? 'checked' : ''} />
            <span>Crear</span>
            <input type="hidden" name="Items[${idx}].Crear" value="false">
          </label>

          <label class="form-check d-flex align-items-center gap-2">
            <input type="checkbox" class="form-check-input chk-editar"
                   name="Items[${idx}].Editar" value="true" ${toBool(it.editar) ? 'checked' : ''} />
            <span>Editar</span>
            <input type="hidden" name="Items[${idx}].Editar" value="false">
          </label>

          <label class="form-check d-flex align-items-center gap-2">
            <input type="checkbox" class="form-check-input chk-eliminar"
                   name="Items[${idx}].Eliminar" value="true" ${toBool(it.eliminar) ? 'checked' : ''} />
            <span>Eliminar</span>
            <input type="hidden" name="Items[${idx}].Eliminar" value="false">
          </label>
        </div>

        <input type="hidden" name="Items[${idx}].SubmoduloId"     value="${it.value || ''}">
        <input type="hidden" name="Items[${idx}].SubmoduloNombre" value="${it.text || ''}">
        <input type="hidden" name="Items[${idx}].YaAsignado"      value="${toBool(it.asignado) ? 'true' : 'false'}">
        <input type="hidden" name="Items[${idx}].EsModulo"        value="${toBool(it.esModulo) ? 'true' : 'false'}">
        <input type="hidden" name="Items[${idx}].Touched"         value="false">
      `;

            wrap.appendChild(hd);
            wrap.appendChild(body);
            accList.appendChild(wrap);

            hd.addEventListener('click', () => {
                const isHidden = body.hidden;
                body.hidden = !isHidden;
                wrap.classList.toggle('open', isHidden);
            });

            body.querySelectorAll('input[type=checkbox]').forEach(cb => {
                cb.disabled = false;
                cb.removeAttribute('disabled');
                cb.style.pointerEvents = 'auto';
                cb.style.opacity = '1';
            });

            const onOps = () => updateRowBadgeAndModule(body);
            const onOnlyTouch = () => { markTouched(body); updateRowBadgeAndModule(body); };

            body.querySelector('.chk-crear')?.addEventListener('change', onOps);
            body.querySelector('.chk-editar')?.addEventListener('change', onOps);
            body.querySelector('.chk-eliminar')?.addEventListener('change', onOps);
            body.querySelector('.chk-ver')?.addEventListener('change', onOnlyTouch);

            // 🔁 Si es "Permiso a TODO el módulo", replicar al resto
            attachMasterReplicationIfNeeded(body);

            idx++;
        });

        // Al terminar de renderizar, sincroniza el estado del módulo
        recomputeModuleAssigned();
    }

    async function loadSubmods() {
        const rolId = ddlRol?.disabled ? (hidRol?.value || '') : (ddlRol?.value || hidRol?.value || '');
        const modId = ddlModulo?.value || hidModulo?.value || '';

        lbl.textContent = (!rolId || !modId) ? 'Seleccione rol y módulo…' : 'Cargando…';
        accList.innerHTML = '';

        if (hidRol) hidRol.value = rolId || '';
        if (hidModulo) hidModulo.value = modId || '';
        if (!rolId || !modId) return;

        if (currentCtrl) currentCtrl.abort();
        currentCtrl = new AbortController();
        const mySeq = ++loadSeq;

        try {
            const url = `/Permisos/SubmodsDetalle?moduloId=${encodeURIComponent(modId)}&rolId=${encodeURIComponent(rolId)}`;
            const resp = await fetch(url, { signal: currentCtrl.signal });
            if (!resp.ok) { lbl.textContent = `Error (${resp.status}) al cargar submódulos`; return; }
            const data = await resp.json();
            if (mySeq !== loadSeq) return;

            renderAccordion(data);

            const rolText = ddlRol?.selectedOptions?.[0]?.text || '';
            const modText = ddlModulo?.selectedOptions?.[0]?.text || '';
            lbl.textContent = `${modText} • ${rolText}`;
        } catch (err) {
            if (err?.name !== 'AbortError') lbl.textContent = 'Error al cargar submódulos';
        } finally {
            if (mySeq === loadSeq) currentCtrl = null;
        }
    }

    // Exponer para la carga inicial desde la vista
    window.__PermisosEdit_load = loadSubmods;

    // Click en la lista de módulos (izquierda)
    modList?.querySelectorAll('.mod-item').forEach(li => {
        li.addEventListener('click', () => {
            modList.querySelectorAll('.mod-item').forEach(x => x.classList.remove('active'));
            li.classList.add('active');
            if (ddlModulo) ddlModulo.value = li.dataset.id;
            loadSubmods();
        });
    });

    ddlRol?.addEventListener('change', loadSubmods);
    ddlModulo?.addEventListener('change', loadSubmods);

    // Validación mínima al enviar
    frm?.addEventListener('submit', e => {
        const rol = ddlRol?.disabled ? (hidRol?.value || '') : (ddlRol?.value || '');
        const mod = ddlModulo?.value || hidModulo?.value || '';
        if (!rol || !mod) {
            e.preventDefault();
            const summary = document.getElementById('valSummary');
            if (summary) {
                summary.textContent = 'Seleccione Rol y Módulo.';
                summary.classList.remove('d-none');
                try { summary.scrollIntoView({ behavior: 'smooth', block: 'center' }); } catch { }
            } else {
                alert('Seleccione Rol y Módulo.');
            }
        }
    });
})();
