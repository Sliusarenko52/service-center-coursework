import json
import os
from doc_helpers import (
    add_p, add_struct_heading, add_sub_heading, add_bullet,
    add_styled_table, add_figure, add_code_block
)

DIAG = r"D:\web_kurs\docs\diagrams"
SHOTS = r"D:\web_kurs\evidence\screenshots"
METRICS_FILE = r"D:\web_kurs\evidence\code_metrics.json"


def build_section_3(doc):
    add_struct_heading(doc, "3 ПРОЄКТУВАННЯ ВЕБСИСТЕМИ")

    add_sub_heading(doc, "3.1 Архетип і обмеження архітектури (Architecture Constraints)")
    add_p(doc, "За типом взаємодії обрано архетип клієнтського вебзастосунку з єдиним довіреним сервером (модульний моноліт ASP.NET Core + SPA-подібний інтерфейс на HTML5, Bootstrap 5 та модульному JavaScript). Такий вибір обґрунтований невеликим обсягом навчального проєкту, спільним циклом розгортання, відсутністю мережевих накладних витрат між мікросервісами та простотою наскрізного тестування.")
    add_styled_table(
        doc,
        "Таблиця 3.1 — Паспорт архітектурних обмежень",
        ["Код і зміст", "Джерело", "Наслідок і перевірка"],
        [
            ["AC-01: Вебінтерфейс у браузері (HTML5/CSS3/JS/Bootstrap 5)", "Завдання дисципліни та досвід автора", "Відмова від важких SPA-фреймворків; перевірено у Chrome"],
            ["AC-02: Серверна платформа .NET 10 (ASP.NET Core)", "Технологічний стек кафедри", "Використано вбудований Kestrel та PasswordHasher"],
            ["AC-03: Локальна реляційна СКБД SQLite без окремого сервера БД", "Вимога автономного запуску на Windows", "БД у файлі service_center.db; перевірено збереження після рестарту"],
            ["AC-04: Відсутність платних зовнішніх сервісів", "Індивідуальне завдання", "Власна локальна автентифікація через HttpOnly cookie-сесії"],
        ],
        col_widths_cm=[5.6, 4.4, 6.8],
    )

    add_sub_heading(doc, "3.2 Подання користувацького інтерфейсу (UI View)")
    add_p(doc, "Інтерфейс складається з п’яти основних екранів: авторизації та реєстрації (UI-01), списку заявок із пошуком/фільтрами/пагінацією (UI-02), форми реєстрації нової заявки (UI-03), детальної картки заявки з банером конфлікту версій (UI-04) та каталогу деталей складу (UI-05). Карту переходів станів інтерфейсу наведено на рисунку 3.1.")
    add_figure(doc, os.path.join(DIAG, "fig3_1_ui_states.png"), "Рисунок 3.1 — Карта станів інтерфейсу користувача (UI View)")

    add_styled_table(
        doc,
        "Таблиця 3.2 — Паспорт ключових екранів та станів інтерфейсу",
        ["Ідентифікатор", "Призначення і ролі", "Стани та UI-патерни"],
        [
            ["UI-01 Вхід / Реєстрація", "Автентифікація трьох ролей; гість", "Нормальний, помилка валідації (400), блокування кнопки; таби та кнопки демо-заповнення"],
            ["UI-02 Список заявок", "Перегляд, пошук, фільтр, пагінація; усі авторизовані", "Завантаження (spinner), порожня вибірка, таблиця, помилка мережі"],
            ["UI-03 Нова заявка", "Реєстрація звернення; клієнт, приймальник", "Збереження введених полів при помилці 400, захист від подвійного надсилання"],
            ["UI-04 Картка заявки", "Діагностика, роботи, деталі, історія; за правами", "Нормальний, банер 409 VERSION_CONFLICT з кнопкою оновлення, фінансовий підсумок"],
            ["UI-05 Склад деталей", "Перегляд артикулів, цін і залишків; приймальник, виконавець", "Табличне подання, адаптивний горизонтальний скрол"],
        ],
        col_widths_cm=[3.8, 5.2, 7.8],
    )
    add_p(doc, "Для адаптивної верстки застосовано сітку Bootstrap 5 з контрольними точками 360, 768 та 1280 CSS-пікселів. Статус заявки позначається не лише кольором бейджа, а й текстовим підписом українською мовою. Усі форми зберігають введені значення при помилці сервера.")

    add_sub_heading(doc, "3.3 Логічне подання системи (Logical View)")
    add_p(doc, "На рисунку 3.2 зображено структурну схему вебсистеми з чітким розмежуванням недовіреної клієнтської частини (браузера) та довіреної серверної частини, яка єдина має доступ до сховища SQLite.")
    add_figure(doc, os.path.join(DIAG, "fig3_2_logical_architecture.png"), "Рисунок 3.2 — Логічна та структурна схема вебсистеми")

    add_sub_heading(doc, "3.4 Подання розгортання (Deployment View)")
    add_p(doc, "У навчальній конфігурації система розгортається локально на ОС Windows 11 (рисунок 3.3). Веббраузер взаємодіє з сервером Kestrel по протоколу HTTP на порту 5080, а сервер виконує локальний файловий ввід/вивід до бази SQLite у режимі WAL. Така схема чесно відображає односерверне розгортання без вигаданих брокерів чи балансувальників.")
    add_figure(doc, os.path.join(DIAG, "fig3_3_deployment.png"), "Рисунок 3.3 — Схема розгортання вебсистеми (Deployment View)")

    add_sub_heading(doc, "3.5 Подання шарів і компонентів (Design View)")
    add_p(doc, "Серверну частину структуровано у чотири логічні шари (рисунок 3.4): шар подання (контролери AuthController, RequestsController, статичні файли wwwroot та DTO), прикладний шар (AuthService, RequestService), шар предметних правил (WorkflowRules, DomainModels) та інфраструктурний шар (SqliteConnectionFactory, DatabaseInitializer). Контролери не містять SQL-запитів, а делегують операції прикладному шару.")
    add_figure(doc, os.path.join(DIAG, "fig3_4_layers.png"), "Рисунок 3.4 — Розподіл відповідальності між логічними шарами (Design View)")

    add_sub_heading(doc, "3.6 Подання процесів (Process View)")
    add_p(doc, "Як архітектурно значущий процес розглянуто одночасне редагування заявки з оптимістичним блокуванням (рисунок 3.5). Якщо версія в БД збігається з версією клієнта, транзакція фіксується (COMMIT) і повертається 200 OK; якщо заявку вже змінив інший працівник, виконується відкат (ROLLBACK) і повертається 409 Conflict.")
    add_figure(doc, os.path.join(DIAG, "fig3_5_sequence_conflict.png"), "Рисунок 3.5 — Діаграма послідовності: оновлення діагностики та конфлікт версій")

    add_sub_heading(doc, "3.7 Подання даних (Data View)")
    add_p(doc, "Модель зберігання реалізовано в реляційній СКБД SQLite із увімкненою перевіркою зовнішніх ключів (PRAGMA foreign_keys = ON) та режимом журналювання WAL. Вона містить 7 пов’язаних таблиць (рисунок 3.6): users, equipment, service_requests, work_items, parts, request_parts та request_status_history. Доменні сутності відокремлено від DTO (LoginRequest, CreateServiceRequestDto, ServiceRequestDetailsDto тощо), тому поле password_hash ніколи не передається у відповідях API.")
    add_figure(doc, os.path.join(DIAG, "fig3_6_er_diagram.png"), "Рисунок 3.6 — ER-діаграма бази даних SQLite")

    add_styled_table(
        doc,
        "Таблиця 3.3 — Словник даних ключової сутності service_requests",
        ["Поле", "Тип / обмеження", "Призначення"],
        [
            ["id", "INTEGER PK AUTOINCREMENT", "Унікальний ідентифікатор заявки"],
            ["request_number", "TEXT UNIQUE NOT NULL", "Людиночитабельний номер (SR-2026-XXXX)"],
            ["client_id", "INTEGER FK → users(id)", "Власник заявки (клієнт)"],
            ["equipment_id", "INTEGER FK → equipment(id)", "Посилання на прийняте обладнання"],
            ["technician_id", "INTEGER NULL FK → users(id)", "Призначений виконавець (може бути NULL)"],
            ["status", "TEXT NOT NULL CHECK (...)", "Поточний стан життєвого циклу заявки"],
            ["total_cost", "REAL NOT NULL CHECK (>= 0)", "Загальна вартість робіт і деталей, грн"],
            ["version", "INTEGER NOT NULL DEFAULT 1", "Лічильник версій для оптимістичного блокування"],
        ],
        col_widths_cm=[3.8, 5.4, 7.6],
    )

    add_sub_heading(doc, "3.8 Подання інтерфейсів і контрактів (Interface View)")
    add_p(doc, "Взаємодія між браузером і сервером відбувається через REST API у форматі JSON (специфікацію збережено у файлі docs/openapi.yaml за стандартом OpenAPI 3.1.0). Помилки повертаються у стандартизованому форматі Problem Details (RFC 9457) із полями status, title, detail, code та опційними fieldErrors або currentVersion. Каталог основних операцій наведено в таблиці 3.4.")

    add_styled_table(
        doc,
        "Таблиця 3.4 — Каталог основних HTTP-операцій REST API",
        ["Метод і маршрут", "Призначення", "Коди відповіді"],
        [
            ["POST /api/auth/login", "Автентифікація користувача", "200 OK; 400 (помилкові дані)"],
            ["GET /api/requests", "Список заявок із фільтрами та пагінацією", "200 OK; 401 (неавторизований)"],
            ["POST /api/requests", "Створення нової заявки", "201 Created; 400; 403 (для виконавця)"],
            ["GET /api/requests/{id}", "Отримання повної картки заявки", "200 OK; 403 (чужа заявка); 404"],
            ["POST /api/requests/{id}/assign", "Призначення виконавця приймальником", "200 OK; 403; 409 (конфлікт версій)"],
            ["PUT /api/requests/{id}/diagnostics", "Збереження результатів діагностики", "200 OK; 400; 403; 409"],
            ["POST /api/requests/{id}/works", "Додавання виду роботи до кошторису", "200 OK; 400 (ціна <= 0); 409"],
            ["POST /api/requests/{id}/parts", "Списання деталі зі складу до заявки", "200 OK; 409 (нестача / версія)"],
            ["POST /api/requests/{id}/status", "Зміна стану заявки за життєвим циклом", "200 OK; 400; 403; 409"],
        ],
        col_widths_cm=[5.6, 6.0, 5.2],
    )

    add_sub_heading(doc, "3.9–3.12 Безпека, операційне, інфраструктурне подання та стек технологій")
    add_p(doc, "Подання безпеки (Security View): для кожної захищеної дії на сервері перевіряється ідентифікатор і роль користувача з cookie-сесії (HttpOnly, SameSite=Lax), що усуває ризик IDOR (OWASP Top 10:2025 A01). Паролі хешуються алгоритмом PBKDF2 через ASP.NET Core PasswordHasher; при реєстрації застосовується мінімальна довжина 15 символів згідно з NIST SP 800-63B-4. Усі SQL-запити параметризовані, а виведення в інтерфейсі екранується функцією escapeHtml().")
    add_p(doc, "Операційне та інфраструктурне подання (Operational / Infrastructure View): готовність процесу й підключення до БД перевіряється через маршрут GET /health (200 healthy або 503). Навчальний стенд: ОС Windows 11, .NET SDK 10.0.404, браузер Google Chrome, локальний диск без потреби у зовнішній мережі.")

    add_styled_table(
        doc,
        "Таблиця 3.5 — Паспорт технологічного стека",
        ["Рівень", "Засіб і фактична версія", "Роль і фіксація"],
        [
            ["Серверна платформа", ".NET SDK 10.0.404 (ASP.NET Core)", "REST API, автентифікація, статика; ServiceCenter.Web.csproj"],
            ["СКБД і драйвер", "SQLite 3 + Microsoft.Data.Sqlite 10.0.0", "Реляційне зберігання, транзакції, WAL; NuGet"],
            ["Інтерфейс", "HTML5, CSS3, Bootstrap 5.3.3, ES2023 JS", "Адаптивна верстка та клієнтська логіка; wwwroot/"],
            ["Тестування", "xUnit 2.9.3, Microsoft.AspNetCore.Mvc.Testing 10.0.0", "Модульні та інтеграційні тести; ServiceCenter.Tests.csproj"],
            ["Аналіз і заміри", "Python 3.14.5, Matplotlib 3.11.2, Selenium 4.54", "Бенчмарки, графіки, зняття скриншотів; evidence/"],
        ],
        col_widths_cm=[3.8, 5.8, 7.2],
    )


def build_section_4(doc):
    add_struct_heading(doc, "4 РЕАЛІЗАЦІЯ ВЕБСИСТЕМИ")

    add_sub_heading(doc, "4.1 Ключові модулі, класи та інтерфейси")
    add_p(doc, "Програмну частину реалізовано у проєкті src/ServiceCenter.Web. Залежності між основними класами серверної частини відображено на рисунку 4.1. Контролери AuthController та RequestsController приймають HTTP-запити і делегують їх сервісам AuthService та RequestService, які використовують статичний клас предметних правил WorkflowRules і фабрику підключень SqliteConnectionFactory.")
    add_figure(doc, os.path.join(DIAG, "fig4_1_classes.png"), "Рисунок 4.1 — Діаграма ключових класів та залежностей серверної частини")

    add_code_block(
        doc,
        "Лістинг 4.1 — Фрагмент реалізації оптимістичного блокування (RequestService.cs)",
        """private static void EnsureOptimisticLock(
    SqliteConnection conn, SqliteTransaction tx, long requestId, int rowsAffected)
{
    if (rowsAffected > 0) return;

    using var verCmd = conn.CreateCommand();
    verCmd.Transaction = tx;
    verCmd.CommandText = "SELECT version FROM service_requests WHERE id = @id;";
    verCmd.Parameters.AddWithValue("@id", requestId);
    var currentVer = Convert.ToInt32(verCmd.ExecuteScalar());

    throw new ConflictAppException(
        $"Заявку вже було змінено іншим працівником (актуальна версія: v{currentVer}).",
        "VERSION_CONFLICT",
        currentVer);
}"""
    )

    add_sub_heading(doc, "4.2 Реалізація інтерфейсу та демонстрація роботи")
    add_p(doc, "Клієнтський інтерфейс (wwwroot/index.html, js/api.js, js/ui.js, js/app.js) реалізує повний цикл супроводу заявки та всі граничні стани. На рисунку 4.2 показано екран входу, на рисунку 4.3 — форму реєстрації нової заявки клієнтом, на рисунку 4.4 — робочий список заявок приймальника з фільтрацією й пагінацією.")

    add_figure(doc, os.path.join(SHOTS, "screen_auth.png"), "Рисунок 4.2 — Екран входу до системи з тестовими обліковими записами", width_cm=14.5)
    add_figure(doc, os.path.join(SHOTS, "screen_create_request.png"), "Рисунок 4.3 — Форма реєстрації нової заявки на обслуговування (HP Pavilion 15)", width_cm=14.5)
    add_figure(doc, os.path.join(SHOTS, "screen_list_receiver.png"), "Рисунок 4.4 — Робочий список заявок приймальника з пошуком, фільтрацією та пагінацією", width_cm=15.0)

    add_p(doc, "На рисунках 4.5–4.6 продемонстровано роботу виконавця з карткою заявки (діагностика, додавання роботи за 750 грн та списання деталі зі складу за 320 грн із автоматичним підрахунком 1070 грн) і таблицю залишків на складі деталей. На рисунку 4.7 показано реакцію системи на некоректну вартість роботи (-100 грн), на рисунку 4.8 — виявлення реального конфлікту одночасного редагування (409 Conflict) двома працівниками, а на рисунку 4.9 — фінально завершену заявку (COMPLETED) з повною історією переходів. На рисунку 4.10 перевірено мобільну адаптацію при ширині 360 CSS-пікселів.")

    add_figure(doc, os.path.join(SHOTS, "screen_details.png"), "Рисунок 4.5 — Детальна картка заявки: діагностика, роботи, списана деталь і фінансовий підсумок", width_cm=15.0)
    add_figure(doc, os.path.join(SHOTS, "screen_parts_catalog.png"), "Рисунок 4.6 — Склад запчастин та актуальні залишки після списання деталі", width_cm=14.5)
    add_figure(doc, os.path.join(SHOTS, "screen_validation_error.png"), "Рисунок 4.7 — Серверна валідація: відмова при спробі додати роботу з некоректною вартістю (-100 грн)", width_cm=14.5)
    add_figure(doc, os.path.join(SHOTS, "screen_conflict_409.png"), "Рисунок 4.8 — Спрацювання контролю версій (409 Conflict) при одночасному редагуванні двома працівниками", width_cm=15.0)
    add_figure(doc, os.path.join(SHOTS, "screen_completed.png"), "Рисунок 4.9 — Завершена заявка (COMPLETED) із результатом виконання та журналом історії", width_cm=15.0)
    add_figure(doc, os.path.join(SHOTS, "screen_mobile_360.png"), "Рисунок 4.10 — Адаптивне відображення інтерфейсу на ширині 360 CSS-пікселів", width_cm=7.0)

    add_sub_heading(doc, "4.3 Керування кодом, метрики та контроль реалізації")
    add_p(doc, "Код організовано за структурою src/, tests/, docs/, evidence/ із винесенням прикладу конфігурації у .env.example без секретів. За результатами автоматичного підрахунку скриптом evidence/collect_metrics.py власна кодова база проєкту (без сторонніх бібліотек і каталогів bin/obj) налічує 15 файлів, 3 397 рядків (3 033 непорожніх рядків). Найбільші модулі — RequestService.cs (769 рядків, що пояснюється інкапсуляцією всіх транзакційних операцій роботи з SQLite) та app.js (636 рядків, координація 5 екранів UI).")

    if os.path.exists(METRICS_FILE):
        with open(METRICS_FILE, "r", encoding="utf-8") as f:
            m = json.load(f)
        rows = [[item["file"], item["ext"], str(item["total_lines"]), str(item["non_empty_lines"])] for item in m["by_file"]]
        rows.append(["РАЗОМ (15 файлів)", "—", str(m["total_lines"]), str(m["non_empty_lines"])])
        add_styled_table(
            doc,
            "Таблиця 4.1 — Фактичні метрики обсягу власного коду",
            ["Файл", "Тип", "Усього рядків", "Непорожніх рядків"],
            rows,
            col_widths_cm=[7.5, 2.5, 3.4, 3.4],
        )

    add_styled_table(
        doc,
        "Таблиця 4.2 — Контроль якості реалізації",
        ["Контрольне запитання", "Статус", "Підтвердження / доказ"],
        [
            ["Чи є завершений основний сценарій?", "Так", "Інтеграційний тест EndToEnd_RequestLifecycle_WithPartsAndApproval_Succeeds"],
            ["Чи перевіряє сервер права доступу?", "Так", "Перевірка в WorkflowRules.CanView + тест Security_ClientCannotAccessOtherClientsRequest (403)"],
            ["Чи зберігається цілісність даних?", "Так", "Транзакції SQLite, FK, CHECK та тест Concurrency_SimultaneousEdit (409)"],
            ["Чи обробляються помилки введення й мережі?", "Так", "RFC 9457 ProblemDetails + ApiError у js/api.js, збереження полів форми"],
            ["Чи винесено секрети з коду?", "Так", "Конфігурація через змінні середовища та .env.example"],
            ["Чи запускається проєкт у чистому каталозі?", "Так", "Перевірено командами dotnet build, dotnet test, dotnet run за README.md"],
        ],
        col_widths_cm=[5.6, 1.8, 9.4],
    )
