import os, subprocess, time, urllib.request
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

OUT_DIR = r"D:\web_kurs\evidence\screenshots"
DB_PATH = r"D:\web_kurs\evidence\ui_shot.db"
BASE_URL = "http://127.0.0.1:5087"

os.makedirs(OUT_DIR, exist_ok=True)
if os.path.exists(DB_PATH):
    os.remove(DB_PATH)

env = os.environ.copy()
env["ASPNETCORE_URLS"] = BASE_URL
env["ConnectionStrings__DefaultConnection"] = f"Data Source={DB_PATH}"

proc = subprocess.Popen(
    ["dotnet", "run", "--project", r"D:\web_kurs\src\ServiceCenter.Web\ServiceCenter.Web.csproj", "--no-build"],
    env=env,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE
)

def wait_ready():
    for _ in range(40):
        try:
            with urllib.request.urlopen(f"{BASE_URL}/health", timeout=1) as r:
                if r.status == 200:
                    return
        except Exception:
            time.sleep(0.25)
    raise RuntimeError("Server did not start")

def make_driver(w=1280, h=900):
    opts = Options()
    opts.add_argument("--headless=new")
    opts.add_argument("--disable-gpu")
    opts.add_argument(f"--window-size={w},{h}")
    return webdriver.Chrome(options=opts)

def js_click(driver, elem):
    driver.execute_script("arguments[0].scrollIntoView({block: 'center'});", elem)
    time.sleep(0.1)
    driver.execute_script("arguments[0].click();", elem)


def login(driver, email, password):
    wait = WebDriverWait(driver, 10)
    wait.until(EC.visibility_of_element_located((By.ID, "login-form")))
    driver.find_element(By.ID, "login-email").clear()
    driver.find_element(By.ID, "login-email").send_keys(email)
    driver.find_element(By.ID, "login-password").clear()
    driver.find_element(By.ID, "login-password").send_keys(password)
    driver.find_element(By.ID, "login-submit").click()
    wait.until(EC.visibility_of_element_located((By.ID, "requests-table-wrap")))
    time.sleep(0.4)

def logout(driver):
    driver.find_element(By.ID, "logout-btn").click()
    WebDriverWait(driver, 10).until(EC.visibility_of_element_located((By.ID, "login-form")))
    time.sleep(0.2)

try:
    wait_ready()
    driver = make_driver(1280, 900)
    wait = WebDriverWait(driver, 10)

    # 1. Екран авторизації
    driver.get(BASE_URL)
    wait.until(EC.visibility_of_element_located((By.ID, "login-form")))
    time.sleep(0.4)
    driver.save_screenshot(os.path.join(OUT_DIR, "screen_auth.png"))

    # 2. Клієнт створює нову заявку
    login(driver, "client1@service.local", "ClientPass123456!")
    driver.find_element(By.ID, "btn-new-request").click()
    wait.until(EC.visibility_of_element_located((By.ID, "create-request-form")))
    driver.find_element(By.ID, "create-category").send_keys("Ноутбук")
    driver.find_element(By.ID, "create-brand").send_keys("HP")
    driver.find_element(By.ID, "create-model").send_keys("Pavilion 15")
    driver.find_element(By.ID, "create-serial").send_keys("SN-HP-5544")
    driver.find_element(By.ID, "create-issue").send_keys("Не заряджається від мережі, гріється роз'єм живлення")
    time.sleep(0.3)
    driver.save_screenshot(os.path.join(OUT_DIR, "screen_create_request.png"))
    driver.find_element(By.ID, "create-submit").click()
    wait.until(EC.visibility_of_element_located((By.ID, "view-details")))
    time.sleep(0.4)
    new_req_title = driver.find_element(By.ID, "detail-title").text  # Заявка SR-2026-0011

    # 3. Приймальник: список заявок + призначення виконавця
    logout(driver)
    login(driver, "receiver@service.local", "ReceiverPass123!")
    driver.save_screenshot(os.path.join(OUT_DIR, "screen_list_receiver.png"))

    # Відкрити першу заявку у списку (найновішу SR-2026-0011)
    driver.find_elements(By.CSS_SELECTOR, ".open-req-btn")[0].click()
    wait.until(EC.visibility_of_element_located((By.ID, "panel-assign")))
    driver.find_element(By.CSS_SELECTOR, "#assign-form button[type='submit']").click()
    time.sleep(0.5)

    # 4. Виконавець (Мельник): діагностика, робота, списання деталі
    logout(driver)
    login(driver, "tech1@service.local", "TechPass123456!")
    driver.find_elements(By.CSS_SELECTOR, ".open-req-btn")[0].click()
    wait.until(EC.visibility_of_element_located((By.ID, "view-details")))
    time.sleep(0.3)

    # Почати діагностику
    driver.find_element(By.CSS_SELECTOR, 'button.status-action-btn[data-status="DIAGNOSING"]').click()
    time.sleep(0.4)
    driver.find_element(By.ID, "diagnostics-input").send_keys("Діагностика: механічне пошкодження контактів роз'єму USB-C на платі.")
    driver.find_element(By.CSS_SELECTOR, "#diagnostics-form button[type='submit']").click()
    time.sleep(0.4)

    # Негативна перевірка: спроба додати роботу з некоректною ціною -100 (обхід HTML5 для перевірки сервера)
    driver.execute_script("document.getElementById('add-work-form').noValidate = true;")
    driver.find_element(By.ID, "work-title").send_keys("Тестова робота")
    driver.find_element(By.ID, "work-price").send_keys("-100")
    js_click(driver, driver.find_element(By.CSS_SELECTOR, "#add-work-form button[type='submit']"))
    wait.until(EC.visibility_of_element_located((By.ID, "detail-error")))
    driver.execute_script("window.scrollTo(0, 0);")
    time.sleep(0.3)
    driver.save_screenshot(os.path.join(OUT_DIR, "screen_validation_error.png"))

    # Коректне додавання роботи та деталі
    driver.find_element(By.ID, "work-title").clear()
    driver.find_element(By.ID, "work-title").send_keys("Заміна роз'єму живлення USB-C")
    driver.find_element(By.ID, "work-price").clear()
    driver.find_element(By.ID, "work-price").send_keys("750")
    js_click(driver, driver.find_element(By.CSS_SELECTOR, "#add-work-form button[type='submit']"))
    time.sleep(0.4)

    # Списання деталі
    part_opts = driver.find_elements(By.CSS_SELECTOR, "#part-select option")
    for opt in part_opts:
        if "USB-C" in opt.text:
            opt.click()
            break
    js_click(driver, driver.find_element(By.CSS_SELECTOR, "#add-part-form button[type='submit']"))
    time.sleep(0.4)
    driver.execute_script("window.scrollTo(0, 0);")
    driver.set_window_size(1280, 1080)
    time.sleep(0.2)
    driver.save_screenshot(os.path.join(OUT_DIR, "screen_details.png"))

    # 5. Каталог складу деталей
    driver.find_element(By.CSS_SELECTOR, 'button[data-view="parts"]').click()
    wait.until(EC.visibility_of_element_located((By.ID, "view-parts")))
    time.sleep(0.4)
    driver.set_window_size(1280, 750)
    driver.save_screenshot(os.path.join(OUT_DIR, "screen_parts_catalog.png"))

    # Повернутись до заявки, надіслати на погодження
    driver.find_element(By.ID, "back-from-parts").click()
    wait.until(EC.visibility_of_element_located((By.ID, "requests-table-wrap")))
    driver.find_elements(By.CSS_SELECTOR, ".open-req-btn")[0].click()
    wait.until(EC.visibility_of_element_located((By.ID, "view-details")))
    time.sleep(0.3)
    driver.find_element(By.CSS_SELECTOR, 'button.status-action-btn[data-status="AWAITING_APPROVAL"]').click()
    time.sleep(0.4)

    # 6. Клієнт погоджує роботи
    logout(driver)
    login(driver, "client1@service.local", "ClientPass123456!")
    driver.find_elements(By.CSS_SELECTOR, ".open-req-btn")[0].click()
    wait.until(EC.visibility_of_element_located((By.ID, "view-details")))
    time.sleep(0.3)
    driver.find_element(By.CSS_SELECTOR, 'button.status-action-btn[data-status="APPROVED"]').click()
    time.sleep(0.4)

    # 7. Виконавець завершує роботу з результатом
    logout(driver)
    login(driver, "tech1@service.local", "TechPass123456!")
    driver.find_elements(By.CSS_SELECTOR, ".open-req-btn")[0].click()
    wait.until(EC.visibility_of_element_located((By.ID, "view-details")))
    time.sleep(0.3)
    driver.find_element(By.CSS_SELECTOR, 'button.status-action-btn[data-status="IN_PROGRESS"]').click()
    time.sleep(0.4)
    driver.find_element(By.ID, "action-result-input").send_keys("Роз'єм USB-C замінено, проведено тест зарядки під навантаженням.")
    driver.find_element(By.CSS_SELECTOR, 'button.status-action-btn[data-status="WORK_DONE"]').click()
    time.sleep(0.4)

    # 8. Приймальник закриває заявку -> COMPLETED
    logout(driver)
    login(driver, "receiver@service.local", "ReceiverPass123!")
    driver.find_elements(By.CSS_SELECTOR, ".open-req-btn")[0].click()
    wait.until(EC.visibility_of_element_located((By.ID, "view-details")))
    time.sleep(0.3)
    driver.find_element(By.CSS_SELECTOR, 'button.status-action-btn[data-status="COMPLETED"]').click()
    time.sleep(0.5)
    driver.set_window_size(1280, 1050)
    driver.save_screenshot(os.path.join(OUT_DIR, "screen_completed.png"))

    # 9. Демонстрація реального конфлікту версій 409 (два працівники відкрили заявку SR-2026-0007)
    driver2 = make_driver(1280, 900)
    driver2.get(BASE_URL)
    login(driver2, "tech1@service.local", "TechPass123456!")

    driver.find_element(By.ID, "back-from-details").click()
    wait.until(EC.visibility_of_element_located((By.ID, "requests-table-wrap")))
    # Знайти рядок з діагностикою (або відкрити SR-2026-0007)
    for row in driver.find_elements(By.CSS_SELECTOR, "#requests-tbody tr"):
        if "SR-2026-0007" in row.text:
            row.find_element(By.CSS_SELECTOR, ".open-req-btn").click()
            break
    wait.until(EC.visibility_of_element_located((By.ID, "diagnostics-form")))

    for row in driver2.find_elements(By.CSS_SELECTOR, "#requests-tbody tr"):
        if "SR-2026-0007" in row.text:
            row.find_element(By.CSS_SELECTOR, ".open-req-btn").click()
            break
    WebDriverWait(driver2, 10).until(EC.visibility_of_element_located((By.ID, "diagnostics-form")))

    # Перший працівник (driver2) зберігає діагностику -> версія зростає
    driver2.find_element(By.ID, "diagnostics-input").clear()
    driver2.find_element(By.ID, "diagnostics-input").send_keys("Оновлено виконавцем: потрібна заміна шлейфу матриці.")
    driver2.find_element(By.CSS_SELECTOR, "#diagnostics-form button[type='submit']").click()
    time.sleep(0.5)

    # Другий працівник (driver) із застарілою версією намагається додати роботу -> 409 Conflict!
    driver.find_element(By.ID, "work-title").send_keys("Заміна блока живлення")
    driver.find_element(By.ID, "work-price").send_keys("600")
    js_click(driver, driver.find_element(By.CSS_SELECTOR, "#add-work-form button[type='submit']"))
    wait.until(EC.visibility_of_element_located((By.ID, "detail-conflict-banner")))
    driver.execute_script("window.scrollTo(0, 0);")
    time.sleep(0.3)
    driver.save_screenshot(os.path.join(OUT_DIR, "screen_conflict_409.png"))

    # 10. Мобільна адаптивність 360px
    driver.find_element(By.ID, "back-from-details").click()
    wait.until(EC.visibility_of_element_located((By.ID, "requests-table-wrap")))
    driver.set_window_size(360, 780)
    time.sleep(0.4)
    driver.save_screenshot(os.path.join(OUT_DIR, "screen_mobile_360.png"))

    driver2.quit()
    driver.quit()
    print("All scenario screenshots captured in", OUT_DIR)
finally:
    proc.terminate()
    try:
        proc.wait(timeout=5)
    except Exception:
        proc.kill()
    if os.path.exists(DB_PATH):
        try:
            os.remove(DB_PATH)
        except Exception:
            pass
