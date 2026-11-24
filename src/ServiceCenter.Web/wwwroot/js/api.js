export class ApiError extends Error {
  constructor(status, code, message, fieldErrors = null, currentVersion = null) {
    super(message);
    this.status = status;
    this.code = code;
    this.fieldErrors = fieldErrors;
    this.currentVersion = currentVersion;
  }
}

async function request(url, options = {}) {
  const init = {
    credentials: "same-origin",
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {})
    },
    ...options
  };

  let response;
  try {
    response = await fetch(url, init);
  } catch {
    throw new ApiError(0, "NETWORK_ERROR", "Немає зв'язку із сервером. Перевірте, чи запущено застосунок.");
  }

  if (response.status === 204) {
    return null;
  }

  let data = null;
  const text = await response.text();
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = null;
    }
  }

  if (!response.ok) {
    const code = data?.code || data?.title || `HTTP_${response.status}`;
    const detail = data?.detail || data?.message || `Помилка сервера (${response.status}).`;
    throw new ApiError(response.status, code, detail, data?.fieldErrors || null, data?.currentVersion ?? null);
  }

  return data;
}

export const api = {
  me: () => request("/api/auth/me"),
  login: (email, password) => request("/api/auth/login", { method: "POST", body: JSON.stringify({ email, password }) }),
  register: (payload) => request("/api/auth/register", { method: "POST", body: JSON.stringify(payload) }),
  logout: () => request("/api/auth/logout", { method: "POST" }),
  getTechnicians: () => request("/api/users/technicians"),
  getClients: () => request("/api/users/clients"),
  getParts: () => request("/api/parts"),

  getRequests: ({ status = "", search = "", sortBy = "updated_at", page = 1, pageSize = 10 }) => {
    const params = new URLSearchParams();
    if (status) params.set("status", status);
    if (search) params.set("search", search);
    if (sortBy) params.set("sortBy", sortBy);
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    return request(`/api/requests?${params.toString()}`);
  },

  getRequest: (id) => request(`/api/requests/${id}`),
  createRequest: (payload) => request("/api/requests", { method: "POST", body: JSON.stringify(payload) }),
  assignTechnician: (id, technicianId, version) =>
    request(`/api/requests/${id}/assign`, { method: "POST", body: JSON.stringify({ technicianId, version }) }),
  updateDiagnostics: (id, diagnosticsNotes, version) =>
    request(`/api/requests/${id}/diagnostics`, { method: "PUT", body: JSON.stringify({ diagnosticsNotes, version }) }),
  addWork: (id, title, price, version) =>
    request(`/api/requests/${id}/works`, { method: "POST", body: JSON.stringify({ title, price, version }) }),
  removeWork: (id, workId, version) =>
    request(`/api/requests/${id}/works/${workId}?version=${version}`, { method: "DELETE" }),
  addPart: (id, partId, quantity, version) =>
    request(`/api/requests/${id}/parts`, { method: "POST", body: JSON.stringify({ partId, quantity, version }) }),
  removePart: (id, reqPartId, version) =>
    request(`/api/requests/${id}/parts/${reqPartId}?version=${version}`, { method: "DELETE" }),
  changeStatus: (id, payload) =>
    request(`/api/requests/${id}/status`, { method: "POST", body: JSON.stringify(payload) })
};
