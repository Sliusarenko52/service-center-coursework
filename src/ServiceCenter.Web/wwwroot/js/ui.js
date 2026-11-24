export const ROLE_LABELS = {
  CLIENT: "Клієнт",
  RECEIVER: "Приймальник",
  TECHNICIAN: "Виконавець"
};

export const STATUS_LABELS = {
  NEW: "Нова",
  ASSIGNED: "Призначено",
  DIAGNOSING: "Діагностика",
  AWAITING_APPROVAL: "Очікує погодження",
  APPROVED: "Погоджено",
  IN_PROGRESS: "У роботі",
  WORK_DONE: "Роботу виконано",
  COMPLETED: "Завершено",
  REJECTED: "Відхилено",
  CANCELLED: "Скасовано"
};

export function escapeHtml(str) {
  return String(str ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

export function formatMoney(val) {
  const num = Number(val ?? 0);
  return `${num.toFixed(2)} грн`;
}

export function formatDate(iso) {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  return d.toLocaleString("uk-UA", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });
}

export function renderStatusBadge(status) {
  const label = STATUS_LABELS[status] || status;
  return `<span class="status-badge status-${escapeHtml(status)}">${escapeHtml(label)}</span>`;
}

export function formatApiError(err) {
  if (!err) return "Невідома помилка.";
  if (err.fieldErrors) {
    const details = Object.values(err.fieldErrors).flat().join(" ");
    return details ? `${err.message} ${details}` : err.message;
  }
  return err.message;
}
