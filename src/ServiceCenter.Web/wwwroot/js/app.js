import { api, ApiError } from "./api.js";
import { ROLE_LABELS, STATUS_LABELS, escapeHtml, formatMoney, formatDate, renderStatusBadge, formatApiError } from "./ui.js";

const state = {
  user: null,
  currentView: "auth",
  filters: { search: "", status: "", sortBy: "updated_at", page: 1, pageSize: 10 },
  currentRequest: null,
  technicians: [],
  clients: [],
  parts: [],
  busy: false
};

const el = (id) => document.getElementById(id);

function showGlobalAlert(message, type = "info") {
  const box = el("global-alert");
  if (!message) {
    box.classList.add("d-none");
    return;
  }
  box.className = `alert alert-${type}`;
  box.textContent = message;
  box.classList.remove("d-none");
}

function showView(name) {
  state.currentView = name;
  for (const id of ["view-auth", "view-requests", "view-create", "view-details", "view-parts"]) {
    el(id).classList.toggle("d-none", id !== `view-${name}`);
  }
  showGlobalAlert("");
}

function updateHeader() {
  const loggedIn = Boolean(state.user);
  el("nav-links").classList.toggle("d-none", !loggedIn);
  el("user-panel").classList.toggle("d-none", !loggedIn);

  if (loggedIn) {
    const roleLabel = ROLE_LABELS[state.user.role] || state.user.role;
    el("user-badge").textContent = `${state.user.fullName} (${roleLabel})`;
    el("nav-create-wrap").classList.toggle("d-none", state.user.role === "TECHNICIAN");
    el("btn-new-request").classList.toggle("d-none", state.user.role === "TECHNICIAN");
    el("nav-parts-wrap").classList.toggle("d-none", state.user.role === "CLIENT");
  }
}

function populateStatusFilter() {
  const select = el("filter-status");
  select.innerHTML = `<option value="">Усі статуси</option>`;
  for (const [code, label] of Object.entries(STATUS_LABELS)) {
    const opt = document.createElement("option");
    opt.value = code;
    opt.textContent = label;
    select.appendChild(opt);
  }
}

async function initSession() {
  try {
    state.user = await api.me();
    updateHeader();
    await preloadReferenceData();
    await openRequestsView();
  } catch {
    state.user = null;
    updateHeader();
    showView("auth");
  }
}

async function preloadReferenceData() {
  if (!state.user) return;
  if (state.user.role === "RECEIVER") {
    const [techs, clients] = await Promise.all([api.getTechnicians(), api.getClients()]);
    state.technicians = techs;
    state.clients = clients;
  }
  if (state.user.role !== "CLIENT") {
    state.parts = await api.getParts();
  }
}

async function openRequestsView() {
  showView("requests");
  el("requests-loading").classList.remove("d-none");
  el("requests-empty").classList.add("d-none");
  el("requests-table-wrap").classList.add("d-none");

  try {
    const res = await api.getRequests(state.filters);
    el("requests-loading").classList.add("d-none");

    const roleSubtitle =
      state.user.role === "CLIENT" ? "Показано лише ваші звернення" :
      state.user.role === "TECHNICIAN" ? "Показано призначені вам роботи" :
      "Усі звернення сервісного центру (керування розподілом)";
    el("requests-subtitle").textContent = roleSubtitle;

    if (res.items.length === 0) {
      el("requests-empty").classList.remove("d-none");
      return;
    }

    el("requests-table-wrap").classList.remove("d-none");
    const tbody = el("requests-tbody");
    tbody.innerHTML = res.items.map((item) => `
      <tr>
        <td class="fw-semibold">${escapeHtml(item.requestNumber)}</td>
        <td>
          <div>${escapeHtml(item.equipmentTitle)}</div>
          <div class="small text-muted">${escapeHtml(item.serialNumber)}</div>
        </td>
        <td>${escapeHtml(item.clientName)}</td>
        <td>${item.technicianName ? escapeHtml(item.technicianName) : '<span class="text-muted">Не призначено</span>'}</td>
        <td>${renderStatusBadge(item.status)}</td>
        <td class="text-end text-nowrap">${escapeHtml(formatMoney(item.totalCost))}</td>
        <td class="small text-muted text-nowrap">${escapeHtml(formatDate(item.updatedAt))}</td>
        <td class="text-end">
          <button type="button" class="btn btn-sm btn-outline-primary open-req-btn" data-id="${item.id}">Відкрити</button>
        </td>
      </tr>
    `).join("");

    const totalPages = Math.max(1, Math.ceil(res.totalCount / res.pageSize));
    el("pagination-info").textContent = `Сторінка ${res.page} з ${totalPages} (усього записів: ${res.totalCount})`;
    el("prev-page-btn").disabled = res.page <= 1;
    el("next-page-btn").disabled = res.page >= totalPages;
  } catch (err) {
    el("requests-loading").classList.add("d-none");
    showGlobalAlert(formatApiError(err), "danger");
  }
}

function openCreateView() {
  el("create-error").classList.add("d-none");
  const isReceiver = state.user.role === "RECEIVER";
  el("create-client-wrap").classList.toggle("d-none", !isReceiver);
  if (isReceiver) {
    const sel = el("create-client-id");
    sel.innerHTML = state.clients.map((c) => `<option value="${c.id}">${escapeHtml(c.fullName)} (${escapeHtml(c.phone)})</option>`).join("");
  }
  showView("create");
}

async function openDetailsView(id) {
  showView("details");
  el("detail-conflict-banner").classList.add("d-none");
  el("detail-error").classList.add("d-none");

  try {
    const req = await api.getRequest(id);
    renderRequestDetails(req);
  } catch (err) {
    el("detail-error").textContent = formatApiError(err);
    el("detail-error").classList.remove("d-none");
  }
}

function renderRequestDetails(req) {
  state.currentRequest = req;
  el("detail-title").textContent = `Заявка ${req.requestNumber}`;
  el("detail-status-badge").innerHTML = renderStatusBadge(req.status);
  el("detail-version-badge").textContent = `Версія: v${req.version}`;

  el("detail-client").textContent = req.clientName;
  el("detail-phone").textContent = req.clientPhone;
  el("detail-equipment").textContent = `${req.equipmentCategory} ${req.equipmentBrand} ${req.equipmentModel}`;
  el("detail-serial").textContent = req.serialNumber;
  el("detail-technician").textContent = req.technicianName || "Не призначено";
  el("detail-created").textContent = formatDate(req.createdAt);
  el("detail-issue").textContent = req.issueDescription;

  const hasResult = Boolean(req.completionResult && req.completionResult.trim());
  el("detail-result-box").classList.toggle("d-none", !hasResult);
  el("detail-completion-result").textContent = req.completionResult || "";

  const canAssign = state.user.role === "RECEIVER" && (req.status === "NEW" || req.status === "ASSIGNED");
  el("panel-assign").classList.toggle("d-none", !canAssign);
  if (canAssign) {
    const sel = el("assign-tech-select");
    sel.innerHTML = state.technicians.map((t) =>
      `<option value="${t.id}" ${t.id === req.technicianId ? "selected" : ""}>${escapeHtml(t.fullName)}</option>`
    ).join("");
  }

  const canEditWork =
    (state.user.role === "TECHNICIAN" && req.technicianId === state.user.id && ["ASSIGNED", "DIAGNOSING", "AWAITING_APPROVAL"].includes(req.status)) ||
    (state.user.role === "RECEIVER" && ["NEW", "ASSIGNED", "DIAGNOSING", "AWAITING_APPROVAL"].includes(req.status));

  el("detail-diagnostics-text").textContent = req.diagnosticsNotes
    ? req.diagnosticsNotes
    : "Результати діагностики ще не зафіксовані.";
  el("diagnostics-form").classList.toggle("d-none", !canEditWork);
  if (canEditWork) {
    el("diagnostics-input").value = req.diagnosticsNotes || "";
  }

  const worksBody = el("works-tbody");
  if (req.workItems.length === 0) {
    worksBody.innerHTML = `<tr><td colspan="4" class="text-muted text-center py-2">Роботи ще не додані</td></tr>`;
  } else {
    worksBody.innerHTML = req.workItems.map((w) => `
      <tr>
        <td>${escapeHtml(w.title)}</td>
        <td>${w.isApproved ? '<span class="badge text-bg-success">Погоджено</span>' : '<span class="badge text-bg-secondary">Очікує</span>'}</td>
        <td class="text-end text-nowrap">${escapeHtml(formatMoney(w.price))}</td>
        <td class="text-end">
          ${canEditWork && !w.isApproved ? `<button type="button" class="btn btn-outline-danger btn-sm py-0 px-2 remove-work-btn" data-wid="${w.id}">×</button>` : ""}
        </td>
      </tr>
    `).join("");
  }
  el("add-work-form").classList.toggle("d-none", !canEditWork);
  el("works-total-label").textContent = formatMoney(req.worksTotal);

  const partsBody = el("req-parts-tbody");
  if (req.parts.length === 0) {
    partsBody.innerHTML = `<tr><td colspan="5" class="text-muted text-center py-2">Деталі не використовувались</td></tr>`;
  } else {
    partsBody.innerHTML = req.parts.map((p) => `
      <tr>
        <td>${escapeHtml(p.partName)}</td>
        <td class="text-end">${p.quantity}</td>
        <td class="text-end text-nowrap">${escapeHtml(formatMoney(p.unitPrice))}</td>
        <td class="text-end text-nowrap">${escapeHtml(formatMoney(p.subtotal))}</td>
        <td class="text-end">
          ${canEditWork ? `<button type="button" class="btn btn-outline-danger btn-sm py-0 px-2 remove-part-btn" data-rpid="${p.id}">×</button>` : ""}
        </td>
      </tr>
    `).join("");
  }
  el("add-part-form").classList.toggle("d-none", !canEditWork);
  if (canEditWork) {
    el("part-select").innerHTML = state.parts.map((p) =>
      `<option value="${p.id}">${escapeHtml(p.name)} — ${escapeHtml(formatMoney(p.unitPrice))} (залишок: ${p.stockQty})</option>`
    ).join("");
  }
  el("parts-total-label").textContent = formatMoney(req.partsTotal);

  el("summary-works").textContent = formatMoney(req.worksTotal);
  el("summary-parts").textContent = formatMoney(req.partsTotal);
  el("summary-total").textContent = formatMoney(req.totalCost);

  renderStatusActions(req);

  const histBox = el("history-container");
  histBox.innerHTML = req.history.map((h) => `
    <div class="timeline-item">
      <div class="small fw-semibold">${h.fromStatus ? `${escapeHtml(STATUS_LABELS[h.fromStatus] || h.fromStatus)} → ` : ""}${escapeHtml(STATUS_LABELS[h.toStatus] || h.toStatus)}</div>
      <div class="small text-muted">${escapeHtml(h.changedByName)} • ${escapeHtml(formatDate(h.changedAt))}</div>
      ${h.comment ? `<div class="small mt-1">${escapeHtml(h.comment)}</div>` : ""}
    </div>
  `).join("");
}

function renderStatusActions(req) {
  const container = el("actions-container");
  const role = state.user.role;
  const uid = state.user.id;
  const buttons = [];

  if (role === "TECHNICIAN" && req.technicianId === uid) {
    if (req.status === "ASSIGNED") {
      buttons.push({ status: "DIAGNOSING", label: "Розпочати діагностику", cls: "btn-primary" });
    }
    if (req.status === "DIAGNOSING") {
      buttons.push({ status: "AWAITING_APPROVAL", label: "Надіслати на погодження клієнту", cls: "btn-primary" });
    }
    if (req.status === "APPROVED") {
      buttons.push({ status: "IN_PROGRESS", label: "Взяти в роботу", cls: "btn-primary" });
    }
    if (req.status === "IN_PROGRESS") {
      buttons.push({ status: "WORK_DONE", label: "Зафіксувати виконання роботи", cls: "btn-success", needsResult: true });
    }
  }

  if (role === "CLIENT" && req.clientId === uid) {
    if (req.status === "AWAITING_APPROVAL") {
      buttons.push({ status: "APPROVED", label: "Погодити роботи та вартість", cls: "btn-success" });
      buttons.push({ status: "REJECTED", label: "Відхилити кошторис", cls: "btn-outline-danger", needsComment: true });
    }
    if (["NEW", "ASSIGNED", "AWAITING_APPROVAL", "APPROVED"].includes(req.status)) {
      buttons.push({ status: "CANCELLED", label: "Скасувати заявку", cls: "btn-outline-secondary", needsComment: true });
    }
  }

  if (role === "RECEIVER") {
    if (req.status === "DIAGNOSING") {
      buttons.push({ status: "AWAITING_APPROVAL", label: "Надіслати на погодження", cls: "btn-primary" });
    }
    if (req.status === "WORK_DONE") {
      buttons.push({ status: "COMPLETED", label: "Завершити та закрити заявку", cls: "btn-success" });
    }
    if (["NEW", "ASSIGNED", "DIAGNOSING", "AWAITING_APPROVAL"].includes(req.status)) {
      buttons.push({ status: "REJECTED", label: "Відхилити заявку", cls: "btn-outline-danger", needsComment: true });
    }
  }

  if (buttons.length === 0) {
    container.innerHTML = `<p class="small text-muted mb-0">На поточному етапі доступні дії для вашої ролі відсутні.</p>`;
    return;
  }

  container.innerHTML = `
    <div id="action-extra-fields" class="mb-2">
      ${buttons.some((b) => b.needsResult) ? `
        <div class="mb-2">
          <label for="action-result-input" class="form-label small mb-1">Результат виконання *</label>
          <textarea id="action-result-input" class="form-control form-control-sm" rows="2" placeholder="Опишіть виконані роботи та перевірку..."></textarea>
        </div>` : ""}
      <div class="mb-2">
        <label for="action-comment-input" class="form-label small mb-1">Коментар (обов'язково при відхиленні)</label>
        <input id="action-comment-input" type="text" class="form-control form-control-sm" placeholder="Причина або примітка...">
      </div>
    </div>
    <div class="d-grid gap-2">
      ${buttons.map((b) => `<button type="button" class="btn btn-sm ${b.cls} status-action-btn" data-status="${b.status}">${escapeHtml(b.label)}</button>`).join("")}
    </div>
  `;
}

function handleDetailError(err) {
  if (err instanceof ApiError && err.code === "VERSION_CONFLICT") {
    el("detail-conflict-text").textContent = err.message;
    el("detail-conflict-banner").classList.remove("d-none");
    return;
  }
  el("detail-error").textContent = formatApiError(err);
  el("detail-error").classList.remove("d-none");
}

async function openPartsView() {
  showView("parts");
  try {
    state.parts = await api.getParts();
    el("parts-catalog-tbody").innerHTML = state.parts.map((p) => `
      <tr>
        <td class="fw-semibold">${escapeHtml(p.sku)}</td>
        <td>${escapeHtml(p.name)}</td>
        <td class="text-end">${escapeHtml(formatMoney(p.unitPrice))}</td>
        <td class="text-end">${p.stockQty} шт.</td>
      </tr>
    `).join("");
  } catch (err) {
    showGlobalAlert(formatApiError(err), "danger");
  }
}

document.addEventListener("DOMContentLoaded", () => {
  populateStatusFilter();
  initSession();

  el("tab-login-btn").addEventListener("click", () => {
    el("tab-login-btn").classList.add("active");
    el("tab-register-btn").classList.remove("active");
    el("login-form").classList.remove("d-none");
    el("register-form").classList.add("d-none");
  });

  el("tab-register-btn").addEventListener("click", () => {
    el("tab-register-btn").classList.add("active");
    el("tab-login-btn").classList.remove("active");
    el("register-form").classList.remove("d-none");
    el("login-form").classList.add("d-none");
  });

  document.querySelectorAll(".demo-fill").forEach((btn) => {
    btn.addEventListener("click", () => {
      el("login-email").value = btn.dataset.email;
      el("login-password").value = btn.dataset.pass;
    });
  });

  el("login-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    el("login-error").classList.add("d-none");
    const submit = el("login-submit");
    submit.disabled = true;
    try {
      state.user = await api.login(el("login-email").value, el("login-password").value);
      updateHeader();
      await preloadReferenceData();
      await openRequestsView();
    } catch (err) {
      el("login-error").textContent = formatApiError(err);
      el("login-error").classList.remove("d-none");
    } finally {
      submit.disabled = false;
    }
  });

  el("register-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    el("register-error").classList.add("d-none");
    const submit = el("register-submit");
    submit.disabled = true;
    try {
      state.user = await api.register({
        fullName: el("reg-fullname").value,
        phone: el("reg-phone").value,
        email: el("reg-email").value,
        password: el("reg-password").value
      });
      updateHeader();
      await openRequestsView();
    } catch (err) {
      el("register-error").textContent = formatApiError(err);
      el("register-error").classList.remove("d-none");
    } finally {
      submit.disabled = false;
    }
  });

  el("logout-btn").addEventListener("click", async () => {
    await api.logout();
    state.user = null;
    updateHeader();
    showView("auth");
  });

  document.querySelectorAll("[data-view]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const target = btn.dataset.view;
      if (target === "requests") openRequestsView();
      else if (target === "create") openCreateView();
      else if (target === "parts") openPartsView();
    });
  });

  el("brand-link").addEventListener("click", (e) => {
    e.preventDefault();
    if (state.user) openRequestsView();
  });

  el("btn-new-request").addEventListener("click", openCreateView);
  el("back-from-create").addEventListener("click", openRequestsView);
  el("create-cancel").addEventListener("click", openRequestsView);
  el("back-from-details").addEventListener("click", openRequestsView);
  el("back-from-parts").addEventListener("click", openRequestsView);

  el("filter-form").addEventListener("submit", (e) => {
    e.preventDefault();
    state.filters.search = el("filter-search").value;
    state.filters.status = el("filter-status").value;
    state.filters.sortBy = el("filter-sort").value;
    state.filters.page = 1;
    openRequestsView();
  });

  el("reset-filters-btn").addEventListener("click", () => {
    el("filter-search").value = "";
    el("filter-status").value = "";
    el("filter-sort").value = "updated_at";
    state.filters = { search: "", status: "", sortBy: "updated_at", page: 1, pageSize: 10 };
    openRequestsView();
  });

  el("prev-page-btn").addEventListener("click", () => {
    if (state.filters.page > 1) {
      state.filters.page -= 1;
      openRequestsView();
    }
  });

  el("next-page-btn").addEventListener("click", () => {
    state.filters.page += 1;
    openRequestsView();
  });

  el("requests-tbody").addEventListener("click", (e) => {
    const btn = e.target.closest(".open-req-btn");
    if (btn) openDetailsView(Number(btn.dataset.id));
  });

  el("create-request-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    el("create-error").classList.add("d-none");
    const submit = el("create-submit");
    submit.disabled = true;
    try {
      const payload = {
        clientId: state.user.role === "RECEIVER" ? Number(el("create-client-id").value) : null,
        category: el("create-category").value,
        brand: el("create-brand").value,
        model: el("create-model").value,
        serialNumber: el("create-serial").value,
        issueDescription: el("create-issue").value
      };
      const created = await api.createRequest(payload);
      e.target.reset();
      await openDetailsView(created.id);
    } catch (err) {
      el("create-error").textContent = formatApiError(err);
      el("create-error").classList.remove("d-none");
    } finally {
      submit.disabled = false;
    }
  });

  el("detail-refresh-btn").addEventListener("click", () => {
    if (state.currentRequest) openDetailsView(state.currentRequest.id);
  });

  el("conflict-reload-btn").addEventListener("click", () => {
    if (state.currentRequest) openDetailsView(state.currentRequest.id);
  });

  el("assign-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    el("detail-error").classList.add("d-none");
    try {
      const updated = await api.assignTechnician(
        state.currentRequest.id,
        Number(el("assign-tech-select").value),
        state.currentRequest.version
      );
      renderRequestDetails(updated);
    } catch (err) {
      handleDetailError(err);
    }
  });

  el("diagnostics-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    el("detail-error").classList.add("d-none");
    try {
      const updated = await api.updateDiagnostics(
        state.currentRequest.id,
        el("diagnostics-input").value,
        state.currentRequest.version
      );
      renderRequestDetails(updated);
    } catch (err) {
      handleDetailError(err);
    }
  });

  el("add-work-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    el("detail-error").classList.add("d-none");
    try {
      const updated = await api.addWork(
        state.currentRequest.id,
        el("work-title").value,
        Number(el("work-price").value),
        state.currentRequest.version
      );
      e.target.reset();
      renderRequestDetails(updated);
    } catch (err) {
      handleDetailError(err);
    }
  });

  el("works-tbody").addEventListener("click", async (e) => {
    const btn = e.target.closest(".remove-work-btn");
    if (!btn) return;
    el("detail-error").classList.add("d-none");
    try {
      const updated = await api.removeWork(
        state.currentRequest.id,
        Number(btn.dataset.wid),
        state.currentRequest.version
      );
      renderRequestDetails(updated);
    } catch (err) {
      handleDetailError(err);
    }
  });

  el("add-part-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    el("detail-error").classList.add("d-none");
    try {
      const updated = await api.addPart(
        state.currentRequest.id,
        Number(el("part-select").value),
        Number(el("part-qty").value),
        state.currentRequest.version
      );
      state.parts = await api.getParts();
      renderRequestDetails(updated);
    } catch (err) {
      handleDetailError(err);
    }
  });

  el("req-parts-tbody").addEventListener("click", async (e) => {
    const btn = e.target.closest(".remove-part-btn");
    if (!btn) return;
    el("detail-error").classList.add("d-none");
    try {
      const updated = await api.removePart(
        state.currentRequest.id,
        Number(btn.dataset.rpid),
        state.currentRequest.version
      );
      state.parts = await api.getParts();
      renderRequestDetails(updated);
    } catch (err) {
      handleDetailError(err);
    }
  });

  el("actions-container").addEventListener("click", async (e) => {
    const btn = e.target.closest(".status-action-btn");
    if (!btn) return;
    el("detail-error").classList.add("d-none");
    const newStatus = btn.dataset.status;
    const comment = el("action-comment-input")?.value || "";
    const completionResult = el("action-result-input")?.value || "";

    try {
      const updated = await api.changeStatus(state.currentRequest.id, {
        newStatus,
        comment,
        completionResult,
        version: state.currentRequest.version
      });
      renderRequestDetails(updated);
    } catch (err) {
      handleDetailError(err);
    }
  });
});
