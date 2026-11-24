import os
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from matplotlib.patches import FancyBboxPatch, Polygon

OUT_DIR = r"D:\web_kurs\docs\diagrams"
os.makedirs(OUT_DIR, exist_ok=True)

plt.rcParams["font.family"] = "DejaVu Sans"
plt.rcParams["font.size"] = 10


def box(ax, x, y, w, h, text, fc="#ffffff", ec="#1e293b", lw=1.4, fontsize=9.5, bold=False):
    patch = FancyBboxPatch(
        (x, y), w, h,
        boxstyle="round,pad=0.02,rounding_size=0.02",
        facecolor=fc, edgecolor=ec, linewidth=lw, zorder=3
    )
    ax.add_patch(patch)
    ax.text(
        x + w / 2, y + h / 2, text,
        ha="center", va="center",
        fontsize=fontsize, fontweight="bold" if bold else "normal",
        color="#0f172a", zorder=4
    )


def rect(ax, x, y, w, h, text, fc="#ffffff", ec="#1e293b", lw=1.4, ls="-", fontsize=9.5, bold=False, va="center"):
    patch = mpatches.Rectangle((x, y), w, h, facecolor=fc, edgecolor=ec, linewidth=lw, linestyle=ls, zorder=3)
    ax.add_patch(patch)
    ty = y + h / 2 if va == "center" else y + h - 0.03
    ax.text(
        x + w / 2, ty, text,
        ha="center", va=va,
        fontsize=fontsize, fontweight="bold" if bold else "normal",
        color="#0f172a", zorder=4
    )


def diamond(ax, cx, cy, w, h, text, fc="#fff7ed", ec="#9a3412"):
    pts = [(cx, cy + h / 2), (cx + w / 2, cy), (cx, cy - h / 2), (cx - w / 2, cy)]
    poly = Polygon(pts, closed=True, facecolor=fc, edgecolor=ec, linewidth=1.4, zorder=3)
    ax.add_patch(poly)
    ax.text(cx, cy, text, ha="center", va="center", fontsize=8.8, color="#0f172a", zorder=4)


def arrow(ax, x1, y1, x2, y2, label="", lx=0, ly=0, ls="-", color="#334155"):
    ax.annotate(
        "", xy=(x2, y2), xytext=(x1, y1),
        arrowprops=dict(arrowstyle="-|>", color=color, lw=1.3, linestyle=ls, shrinkA=0, shrinkB=0),
        zorder=2
    )
    if label:
        mx, my = (x1 + x2) / 2 + lx, (y1 + y2) / 2 + ly
        ax.text(mx, my, label, ha="center", va="center", fontsize=8.3, color="#1e293b",
                bbox=dict(boxstyle="round,pad=0.15", fc="white", ec="none", alpha=0.85), zorder=5)


def line(ax, x1, y1, x2, y2, color="#334155", ls="-"):
    ax.plot([x1, x2], [y1, y2], color=color, lw=1.3, linestyle=ls, zorder=2)


def new_fig(w=10, h=6):
    fig, ax = plt.subplots(figsize=(w, h), dpi=240)
    ax.set_xlim(0, 1)
    ax.set_ylim(0, 1)
    ax.axis("off")
    return fig, ax


def draw_use_case():
    fig, ax = new_fig(10, 6.2)
    # System boundary
    rect(ax, 0.23, 0.06, 0.54, 0.88, "Вебсистема обліку заявок сервісного центру", fc="#f8fafc", ec="#334155", lw=1.6, bold=True, va="top")

    cases = [
        (0.80, "Увійти / зареєструватися"),
        (0.69, "Зареєструвати заявку на ремонт"),
        (0.58, "Переглянути власні заявки"),
        (0.47, "Погодити або відхилити перелік робіт"),
        (0.36, "Переглянути всі заявки та призначити виконавця"),
        (0.25, "Зафіксувати діагностику, роботи й деталі"),
        (0.14, "Зафіксувати результат і завершити заявку"),
    ]
    for cy, txt in cases:
        el = mpatches.Ellipse((0.50, cy), 0.44, 0.08, facecolor="#ffffff", edgecolor="#2563eb", lw=1.3, zorder=3)
        ax.add_patch(el)
        ax.text(0.50, cy, txt, ha="center", va="center", fontsize=9, zorder=4)

    # Actors
    def actor(ax_x, ax_y, name):
        circ = mpatches.Circle((ax_x, ax_y + 0.055), 0.022, fc="white", ec="#0f172a", lw=1.4, zorder=4)
        ax.add_patch(circ)
        line(ax, ax_x, ax_y + 0.033, ax_x, ax_y - 0.025, "#0f172a")
        line(ax, ax_x - 0.028, ax_y + 0.015, ax_x + 0.028, ax_y + 0.015, "#0f172a")
        line(ax, ax_x, ax_y - 0.025, ax_x - 0.022, ax_y - 0.065, "#0f172a")
        line(ax, ax_x, ax_y - 0.025, ax_x + 0.022, ax_y - 0.065, "#0f172a")
        ax.text(ax_x, ax_y - 0.09, name, ha="center", va="center", fontsize=9.5, fontweight="bold")

    actor(0.10, 0.62, "Клієнт")
    actor(0.90, 0.62, "Приймальник")
    actor(0.90, 0.25, "Виконавець")

    for cy in [0.80, 0.69, 0.58, 0.47]:
        line(ax, 0.13, 0.62, 0.28, cy, "#475569")
    for cy in [0.80, 0.69, 0.36, 0.14]:
        line(ax, 0.87, 0.62, 0.72, cy, "#475569")
    for cy in [0.80, 0.25, 0.14]:
        line(ax, 0.87, 0.25, 0.72, cy, "#475569")

    fig.tight_layout()
    fig.savefig(os.path.join(OUT_DIR, "fig1_1_use_case.png"), dpi=240, bbox_inches="tight")
    plt.close(fig)


def draw_flowchart():
    fig, ax = new_fig(9.5, 7.5)
    box(ax, 0.35, 0.91, 0.30, 0.06, "Початок", fc="#e0f2fe", ec="#0369a1", bold=True)
    rect(ax, 0.25, 0.80, 0.50, 0.07, "Отримати id, newStatus, version, сесію", fc="#f8fafc")
    diamond(ax, 0.50, 0.69, 0.38, 0.10, "Сесія дійсна і роль\nдозволяє перехід?")
    rect(ax, 0.76, 0.655, 0.21, 0.07, "Повернути 401 / 403", fc="#fee2e2", ec="#991b1b")

    diamond(ax, 0.50, 0.54, 0.40, 0.10, "Перехід допустимий,\nроботи погоджені, є результат?")
    rect(ax, 0.76, 0.505, 0.21, 0.07, "Повернути 400 / 409", fc="#fee2e2", ec="#991b1b")

    rect(ax, 0.25, 0.38, 0.50, 0.07, "Почати транзакцію SQLite;\nUPDATE ... WHERE id=@id AND version=@ver", fc="#eff6ff", ec="#1d4ed8")
    diamond(ax, 0.50, 0.26, 0.36, 0.10, "rowsAffected > 0?")
    rect(ax, 0.75, 0.225, 0.23, 0.07, "ROLLBACK;\n409 VERSION_CONFLICT", fc="#fef3c7", ec="#92400e")

    rect(ax, 0.25, 0.12, 0.50, 0.07, "Записати історію станів;\nCOMMIT і повернути 200 OK", fc="#dcfce7", ec="#15803d")
    box(ax, 0.35, 0.02, 0.30, 0.06, "Кінець", fc="#e0f2fe", ec="#0369a1", bold=True)

    arrow(ax, 0.50, 0.91, 0.50, 0.87)
    arrow(ax, 0.50, 0.80, 0.50, 0.74)
    arrow(ax, 0.69, 0.69, 0.76, 0.69, "Ні", ly=0.02)
    arrow(ax, 0.50, 0.64, 0.50, 0.59, "Так", lx=0.03)
    arrow(ax, 0.70, 0.54, 0.76, 0.54, "Ні", ly=0.02)
    arrow(ax, 0.50, 0.49, 0.50, 0.45, "Так", lx=0.03)
    arrow(ax, 0.50, 0.38, 0.50, 0.31)
    arrow(ax, 0.68, 0.26, 0.75, 0.26, "Ні", ly=0.02)
    arrow(ax, 0.50, 0.21, 0.50, 0.19, "Так", lx=0.03)
    arrow(ax, 0.50, 0.12, 0.50, 0.08)

    fig.tight_layout()
    fig.savefig(os.path.join(OUT_DIR, "fig2_1_flowchart_status.png"), dpi=240, bbox_inches="tight")
    plt.close(fig)


def draw_ui_states():
    fig, ax = new_fig(10, 5.5)
    box(ax, 0.04, 0.68, 0.24, 0.14, "Вхід / Реєстрація\n(Автентифікація)", fc="#f8fafc", bold=True)
    box(ax, 0.38, 0.68, 0.26, 0.14, "Список заявок\n(Пошук, фільтр, пагінація)", fc="#eff6ff", ec="#1d4ed8", bold=True)
    box(ax, 0.74, 0.68, 0.22, 0.14, "Склад деталей\n(Перегляд залишків)", fc="#f8fafc")
    box(ax, 0.04, 0.28, 0.24, 0.14, "Форма нової заявки\n(Збереження при помилці)", fc="#f0fdf4", ec="#15803d")
    box(ax, 0.38, 0.28, 0.26, 0.14, "Картка заявки\n(Діагностика, роботи, деталі)", fc="#eff6ff", ec="#1d4ed8", bold=True)
    box(ax, 0.74, 0.28, 0.22, 0.14, "Банер конфлікту (409)\n«Оновити актуальні дані»", fc="#fef3c7", ec="#92400e")

    arrow(ax, 0.28, 0.75, 0.38, 0.75, "успішний вхід", ly=0.03)
    arrow(ax, 0.64, 0.75, 0.74, 0.75, "меню", ly=0.03)
    arrow(ax, 0.44, 0.68, 0.20, 0.42, "створити", lx=-0.05)
    arrow(ax, 0.28, 0.35, 0.38, 0.35, "створено", ly=0.03)
    arrow(ax, 0.51, 0.68, 0.51, 0.42, "відкрити", lx=0.05)
    arrow(ax, 0.64, 0.37, 0.74, 0.37, "версія застаріла", ly=0.03)
    arrow(ax, 0.74, 0.32, 0.64, 0.32, "перезавантажити", ly=-0.03)

    fig.tight_layout()
    fig.savefig(os.path.join(OUT_DIR, "fig3_1_ui_states.png"), dpi=240, bbox_inches="tight")
    plt.close(fig)


def draw_logical_view():
    fig, ax = new_fig(10, 5.8)
    rect(ax, 0.33, 0.80, 0.34, 0.13, "Користувач\n(Клієнт / Приймальник / Виконавець)", fc="#f8fafc", bold=True)
    rect(ax, 0.28, 0.56, 0.44, 0.14, "Веббраузер (HTML5, Bootstrap 5, JS)\nВідображення станів, локальна валідація", fc="#eff6ff", ec="#1d4ed8")
    rect(ax, 0.18, 0.23, 0.64, 0.23, "", fc="#f8fafc", ec="#334155", ls="--")
    ax.text(0.50, 0.43, "Довірена серверна частина (ASP.NET Core .NET 10)", ha="center", va="center", fontsize=9.5, fontweight="bold")
    rect(ax, 0.22, 0.26, 0.56, 0.13, "REST API, Cookie-сесії, перевірка прав доступу,\nжиттєвий цикл заявки, контроль версій, облік деталей", fc="#ffffff", ec="#1e293b")
    rect(ax, 0.28, 0.04, 0.44, 0.12, "Сховище даних SQLite (service_center.db)\nТранзакції, індекси, ключі, історія", fc="#ecfdf5", ec="#047857", bold=True)

    arrow(ax, 0.50, 0.80, 0.50, 0.70, "дія / результат", lx=0.09)
    arrow(ax, 0.50, 0.56, 0.50, 0.46, "HTTP/HTTPS JSON + Cookie", lx=0.13)
    arrow(ax, 0.50, 0.23, 0.50, 0.16, "SQL-запити (Microsoft.Data.Sqlite)", lx=0.16)

    fig.tight_layout()
    fig.savefig(os.path.join(OUT_DIR, "fig3_2_logical_architecture.png"), dpi=240, bbox_inches="tight")
    plt.close(fig)


def draw_deployment():
    fig, ax = new_fig(9.5, 5.5)
    rect(ax, 0.08, 0.65, 0.84, 0.26, "«device» Пристрій користувача (Windows / Linux / Mobile)", fc="#f8fafc", bold=True, va="top")
    rect(ax, 0.22, 0.69, 0.56, 0.14, "«executionEnvironment» Веббраузер (Chrome / Edge)\nHTML5 + Bootstrap 5 + ES-модулі", fc="#eff6ff", ec="#1d4ed8")

    rect(ax, 0.08, 0.08, 0.84, 0.45, "«node» Навчальний сервер / ПК автора (Windows 11, .NET 10.0)", fc="#f8fafc", bold=True, va="top")
    rect(ax, 0.18, 0.30, 0.64, 0.15, "«executionEnvironment» Сервер Kestrel (ServiceCenter.Web)\nПорт 5080 • REST API • Статика wwwroot • Перевірка /health", fc="#eff6ff", ec="#1d4ed8")
    rect(ax, 0.18, 0.11, 0.64, 0.13, "«executionEnvironment» СКБД SQLite 3 (WAL mode)\nЛокальний файл App_Data/service_center.db", fc="#ecfdf5", ec="#047857")

    arrow(ax, 0.50, 0.69, 0.50, 0.45, "HTTP :5080 (JSON / Static)", lx=0.12)
    arrow(ax, 0.50, 0.30, 0.50, 0.24, "Внутрішній файловий I/O", lx=0.12)

    fig.tight_layout()
    fig.savefig(os.path.join(OUT_DIR, "fig3_3_deployment.png"), dpi=240, bbox_inches="tight")
    plt.close(fig)


def draw_layers():
    fig, ax = new_fig(9.5, 6.2)
    rect(ax, 0.12, 0.77, 0.76, 0.17, "Шар подання (Presentation Layer)\nwwwroot (HTML/JS/Bootstrap) + AuthController + RequestsController + DTO", fc="#eff6ff", ec="#1d4ed8")
    rect(ax, 0.12, 0.54, 0.76, 0.17, "Прикладний шар (Application Layer)\nAuthService + RequestService (координація сценаріїв і транзакцій)", fc="#f0fdf4", ec="#15803d")
    rect(ax, 0.12, 0.31, 0.76, 0.17, "Шар предметних правил (Domain Layer)\nWorkflowRules + DomainModels (дозволені переходи, права, інваріанти)", fc="#fff7ed", ec="#c2410c")
    rect(ax, 0.12, 0.08, 0.76, 0.17, "Інфраструктурний шар (Infrastructure Layer)\nSqliteConnectionFactory + DatabaseInitializer (SQLite DDL, seed, WAL)", fc="#f8fafc", ec="#475569")

    arrow(ax, 0.50, 0.77, 0.50, 0.71, "виклик сервісу", lx=0.08)
    arrow(ax, 0.50, 0.54, 0.50, 0.48, "перевірка правил", lx=0.09)
    arrow(ax, 0.50, 0.31, 0.50, 0.25, "доступ до БД", lx=0.08)

    fig.tight_layout()
    fig.savefig(os.path.join(OUT_DIR, "fig3_4_layers.png"), dpi=240, bbox_inches="tight")
    plt.close(fig)


def draw_sequence():
    fig, ax = new_fig(10, 6.5)
    actors = [
        (0.14, "Браузер (Виконавець)"),
        (0.40, "RequestsController"),
        (0.66, "RequestService"),
        (0.90, "SQLite БД")
    ]
    for x, name in actors:
        rect(ax, x - 0.10, 0.88, 0.20, 0.08, name, fc="#f8fafc", bold=True, fontsize=8.8)
        line(ax, x, 0.88, x, 0.06, "#94a3b8", ls="--")

    arrow(ax, 0.14, 0.81, 0.40, 0.81, "PUT /diagnostics (ver=1)", ly=0.025)
    arrow(ax, 0.40, 0.74, 0.66, 0.74, "UpdateDiagnostics(id, ver=1)", ly=0.025)
    arrow(ax, 0.66, 0.67, 0.90, 0.67, "BEGIN TX; UPDATE ... WHERE ver=1", ly=0.025)

    # alt frame
    rect(ax, 0.06, 0.11, 0.90, 0.50, "", fc="none", ec="#475569", lw=1.2)
    rect(ax, 0.06, 0.55, 0.08, 0.06, "alt", fc="#e2e8f0", ec="#475569", bold=True, fontsize=8.5)
    ax.text(0.32, 0.58, "[версія збігається: rowsAffected = 1]", fontsize=8.5, fontweight="bold", color="#15803d")

    arrow(ax, 0.90, 0.50, 0.66, 0.50, "COMMIT; ver=2", ly=0.025, ls="--")
    arrow(ax, 0.66, 0.44, 0.40, 0.44, "DetailsDto (v2)", ly=0.025, ls="--")
    arrow(ax, 0.40, 0.38, 0.14, 0.38, "200 OK", ly=0.025, ls="--")

    line(ax, 0.06, 0.33, 0.96, 0.33, "#475569", ls="--")
    ax.text(0.32, 0.30, "[заявку змінив колега: rowsAffected = 0]", fontsize=8.5, fontweight="bold", color="#b45309")

    arrow(ax, 0.90, 0.24, 0.66, 0.24, "ROLLBACK; currentVer=2", ly=0.025, ls="--")
    arrow(ax, 0.66, 0.18, 0.40, 0.18, "ConflictAppException", ly=0.025, ls="--")
    arrow(ax, 0.40, 0.13, 0.14, 0.13, "409 Conflict (VERSION_CONFLICT)", ly=0.025, ls="--")

    fig.tight_layout()
    fig.savefig(os.path.join(OUT_DIR, "fig3_5_sequence_conflict.png"), dpi=240, bbox_inches="tight")
    plt.close(fig)


def draw_er():
    fig, ax = new_fig(10.5, 6.8)
    rect(ax, 0.04, 0.68, 0.25, 0.24, "users\nPK id\nemail, full_name, phone\nrole, password_hash", fc="#eff6ff", ec="#1d4ed8", fontsize=8.5)
    rect(ax, 0.04, 0.32, 0.25, 0.24, "equipment\nPK id\nFK client_id\ncategory, brand, model\nserial_number", fc="#f8fafc", ec="#334155", fontsize=8.5)
    rect(ax, 0.38, 0.52, 0.28, 0.38, "service_requests\nPK id, request_number\nFK client_id, equipment_id\nFK technician_id\nissue_description, status\ndiagnostics_notes\ncompletion_result\ntotal_cost, version", fc="#f0fdf4", ec="#15803d", fontsize=8.3)
    rect(ax, 0.74, 0.68, 0.23, 0.22, "work_items\nPK id\nFK request_id\ntitle, price\nis_approved", fc="#f8fafc", ec="#334155", fontsize=8.5)
    rect(ax, 0.74, 0.36, 0.23, 0.22, "request_parts\nPK id\nFK request_id, part_id\npart_name, quantity\nunit_price", fc="#fff7ed", ec="#c2410c", fontsize=8.5)
    rect(ax, 0.74, 0.06, 0.23, 0.20, "parts\nPK id, sku\nname, unit_price\nstock_qty", fc="#fff7ed", ec="#c2410c", fontsize=8.5)
    rect(ax, 0.38, 0.08, 0.28, 0.24, "request_status_history\nPK id\nFK request_id, changed_by_id\nfrom_status, to_status\ncomment, changed_at", fc="#f8fafc", ec="#334155", fontsize=8.3)

    line(ax, 0.16, 0.68, 0.16, 0.56)
    ax.text(0.19, 0.62, "1 : 0..*", fontsize=8)
    line(ax, 0.29, 0.78, 0.38, 0.78)
    ax.text(0.335, 0.81, "1 : 0..*", fontsize=8)
    line(ax, 0.29, 0.45, 0.38, 0.60)
    ax.text(0.32, 0.55, "1 : 0..*", fontsize=8)
    line(ax, 0.66, 0.78, 0.74, 0.78)
    ax.text(0.70, 0.81, "1 : 0..*", fontsize=8)
    line(ax, 0.66, 0.62, 0.74, 0.48)
    ax.text(0.70, 0.58, "1 : 0..*", fontsize=8)
    line(ax, 0.85, 0.36, 0.85, 0.26)
    ax.text(0.89, 0.31, "0..* : 1", fontsize=8)
    line(ax, 0.52, 0.52, 0.52, 0.32)
    ax.text(0.56, 0.42, "1 : 1..*", fontsize=8)

    fig.tight_layout()
    fig.savefig(os.path.join(OUT_DIR, "fig3_6_er_diagram.png"), dpi=240, bbox_inches="tight")
    plt.close(fig)


def draw_classes():
    fig, ax = new_fig(10, 6.0)
    rect(ax, 0.06, 0.65, 0.36, 0.25, "AuthController\n+ Login(LoginRequest)\n+ Register(RegisterRequest)\n+ Me(), Logout()", fc="#eff6ff", ec="#1d4ed8", fontsize=8.8)
    rect(ax, 0.56, 0.65, 0.38, 0.25, "RequestsController\n+ List(), GetById(), Create()\n+ Assign(), UpdateDiagnostics()\n+ AddWork(), AddPart(), ChangeStatus()", fc="#eff6ff", ec="#1d4ed8", fontsize=8.8)
    rect(ax, 0.06, 0.33, 0.36, 0.22, "AuthService\n+ Authenticate()\n+ RegisterClient()\n+ GetUsersByRole()", fc="#f0fdf4", ec="#15803d", fontsize=8.8)
    rect(ax, 0.56, 0.33, 0.38, 0.22, "RequestService\n+ CreateRequest(), AssignTechnician()\n+ AddWorkItem(), AddRequestPart()\n+ ChangeStatus()", fc="#f0fdf4", ec="#15803d", fontsize=8.8)
    rect(ax, 0.06, 0.06, 0.36, 0.18, "SqliteConnectionFactory\n+ CreateConnection(): SqliteConnection", fc="#f8fafc", ec="#334155", fontsize=8.8)
    rect(ax, 0.56, 0.06, 0.38, 0.18, "WorkflowRules (static)\n+ CanView()\n+ ValidateStatusTransition()", fc="#fff7ed", ec="#c2410c", fontsize=8.8)

    arrow(ax, 0.24, 0.65, 0.24, 0.55, "використовує", lx=0.07)
    arrow(ax, 0.75, 0.65, 0.75, 0.55, "використовує", lx=0.07)
    arrow(ax, 0.24, 0.33, 0.24, 0.24, "залежить від", lx=0.07)
    arrow(ax, 0.56, 0.39, 0.42, 0.20, "залежить від", lx=-0.06)
    arrow(ax, 0.75, 0.33, 0.75, 0.24, "перевіряє", lx=0.06)

    fig.tight_layout()
    fig.savefig(os.path.join(OUT_DIR, "fig4_1_classes.png"), dpi=240, bbox_inches="tight")
    plt.close(fig)


if __name__ == "__main__":
    draw_use_case()
    draw_flowchart()
    draw_ui_states()
    draw_logical_view()
    draw_deployment()
    draw_layers()
    draw_sequence()
    draw_er()
    draw_classes()
    print("Generated 9 diagrams in", OUT_DIR)
