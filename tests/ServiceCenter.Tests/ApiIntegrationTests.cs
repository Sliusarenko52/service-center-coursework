using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using ServiceCenter.Web.Models;

namespace ServiceCenter.Tests;

public sealed class CustomWebFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sc_test_{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* ігноруємо блокування файлу на Windows */ }
        }
    }
}

public sealed class ApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static async Task<HttpClient> CreateAuthedClientAsync(CustomWebFactory factory, string email, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        resp.EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task EndToEnd_RequestLifecycle_WithPartsAndApproval_Succeeds()
    {
        using var factory = new CustomWebFactory();
        var client1 = await CreateAuthedClientAsync(factory, "client1@service.local", "ClientPass123456!");
        var receiver = await CreateAuthedClientAsync(factory, "receiver@service.local", "ReceiverPass123!");
        var tech1 = await CreateAuthedClientAsync(factory, "tech1@service.local", "TechPass123456!");

        // 1. Клієнт створює заявку
        var createResp = await client1.PostAsJsonAsync("/api/requests", new CreateServiceRequestDto(
            null, "Ноутбук", "Dell", "XPS 15", "SN-XPS-9991", "Не вмикається після оновлення BIOS"));
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = (await createResp.Content.ReadFromJsonAsync<ServiceRequestDetailsDto>(JsonOpts))!;
        Assert.Equal(RequestStatuses.New, created.Status);
        Assert.Equal(1, created.Version);

        // 2. Приймальник призначає виконавця (tech1 має id = 4 у seed)
        var techs = (await receiver.GetFromJsonAsync<List<UserDto>>("/api/users/technicians", JsonOpts))!;
        var tech1Id = techs.First(t => t.Email == "tech1@service.local").Id;

        var assignResp = await receiver.PostAsJsonAsync($"/api/requests/{created.Id}/assign",
            new AssignTechnicianDto(tech1Id, created.Version));
        assignResp.EnsureSuccessStatusCode();
        var assigned = (await assignResp.Content.ReadFromJsonAsync<ServiceRequestDetailsDto>(JsonOpts))!;
        Assert.Equal(RequestStatuses.Assigned, assigned.Status);
        Assert.Equal(2, assigned.Version);

        // 3. Виконавець фіксує діагностику, додає роботу й деталь
        var diagResp = await tech1.PutAsJsonAsync($"/api/requests/{created.Id}/diagnostics",
            new UpdateDiagnosticsDto("Пошкоджено мікросхему BIOS та висохла термопаста.", assigned.Version));
        diagResp.EnsureSuccessStatusCode();
        var afterDiag = (await diagResp.Content.ReadFromJsonAsync<ServiceRequestDetailsDto>(JsonOpts))!;
        Assert.Equal(RequestStatuses.Diagnosing, afterDiag.Status);

        var workResp = await tech1.PostAsJsonAsync($"/api/requests/{created.Id}/works",
            new AddWorkItemDto("Прошивка програматором BIOS", 950m, afterDiag.Version));
        workResp.EnsureSuccessStatusCode();
        var afterWork = (await workResp.Content.ReadFromJsonAsync<ServiceRequestDetailsDto>(JsonOpts))!;

        var parts = (await tech1.GetFromJsonAsync<List<PartRecord>>("/api/parts", JsonOpts))!;
        var thermalPart = parts.First(p => p.Sku == "PRT-001");
        var partResp = await tech1.PostAsJsonAsync($"/api/requests/{created.Id}/parts",
            new AddRequestPartDto(thermalPart.Id, 1, afterWork.Version));
        partResp.EnsureSuccessStatusCode();
        var afterPart = (await partResp.Content.ReadFromJsonAsync<ServiceRequestDetailsDto>(JsonOpts))!;
        Assert.Equal(950m + thermalPart.UnitPrice, afterPart.TotalCost);

        // 4. Надсилання на погодження -> погодження клієнтом
        var awaitResp = await tech1.PostAsJsonAsync($"/api/requests/{created.Id}/status",
            new ChangeStatusDto(RequestStatuses.AwaitingApproval, "Кошторис сформовано", null, afterPart.Version));
        awaitResp.EnsureSuccessStatusCode();
        var awaiting = (await awaitResp.Content.ReadFromJsonAsync<ServiceRequestDetailsDto>(JsonOpts))!;

        var approveResp = await client1.PostAsJsonAsync($"/api/requests/{created.Id}/status",
            new ChangeStatusDto(RequestStatuses.Approved, "Погоджую вартість", null, awaiting.Version));
        approveResp.EnsureSuccessStatusCode();
        var approved = (await approveResp.Content.ReadFromJsonAsync<ServiceRequestDetailsDto>(JsonOpts))!;
        Assert.All(approved.WorkItems, w => Assert.True(w.IsApproved));

        // 5. Виконання та закриття
        var inProgResp = await tech1.PostAsJsonAsync($"/api/requests/{created.Id}/status",
            new ChangeStatusDto(RequestStatuses.InProgress, "Розпочато ремонт", null, approved.Version));
        inProgResp.EnsureSuccessStatusCode();
        var inProg = (await inProgResp.Content.ReadFromJsonAsync<ServiceRequestDetailsDto>(JsonOpts))!;

        var doneResp = await tech1.PostAsJsonAsync($"/api/requests/{created.Id}/status",
            new ChangeStatusDto(RequestStatuses.WorkDone, "Тест пройдено", "Прошито BIOS, система стабільна", inProg.Version));
        doneResp.EnsureSuccessStatusCode();
        var workDone = (await doneResp.Content.ReadFromJsonAsync<ServiceRequestDetailsDto>(JsonOpts))!;

        var compResp = await receiver.PostAsJsonAsync($"/api/requests/{created.Id}/status",
            new ChangeStatusDto(RequestStatuses.Completed, "Видано клієнту", null, workDone.Version));
        compResp.EnsureSuccessStatusCode();
        var completed = (await compResp.Content.ReadFromJsonAsync<ServiceRequestDetailsDto>(JsonOpts))!;

        Assert.Equal(RequestStatuses.Completed, completed.Status);
        Assert.True(completed.History.Count >= 6);
    }

    [Fact]
    public async Task Security_ClientCannotAccessOtherClientsRequest()
    {
        using var factory = new CustomWebFactory();
        var client1 = await CreateAuthedClientAsync(factory, "client1@service.local", "ClientPass123456!");
        var client2 = await CreateAuthedClientAsync(factory, "client2@service.local", "ClientPass123456!");

        var list1 = (await client1.GetFromJsonAsync<PagedResult<ServiceRequestSummaryDto>>("/api/requests", JsonOpts))!;
        var foreignId = list1.Items.First().Id;

        var resp = await client2.GetAsync($"/api/requests/{foreignId}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Validation_NegativeWorkPrice_ReturnsBadRequest()
    {
        using var factory = new CustomWebFactory();
        var receiver = await CreateAuthedClientAsync(factory, "receiver@service.local", "ReceiverPass123!");
        var list = (await receiver.GetFromJsonAsync<PagedResult<ServiceRequestSummaryDto>>("/api/requests?status=DIAGNOSING", JsonOpts))!;
        var target = list.Items.First();

        var resp = await receiver.PostAsJsonAsync($"/api/requests/{target.Id}/works",
            new AddWorkItemDto("Некоректна робота", -150m, target.Version));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Concurrency_SimultaneousEditByTwoEmployees_ReturnsConflictWithCurrentVersion()
    {
        using var factory = new CustomWebFactory();
        var receiver = await CreateAuthedClientAsync(factory, "receiver@service.local", "ReceiverPass123!");
        var tech1 = await CreateAuthedClientAsync(factory, "tech1@service.local", "TechPass123456!");

        var list = (await tech1.GetFromJsonAsync<PagedResult<ServiceRequestSummaryDto>>("/api/requests?status=DIAGNOSING", JsonOpts))!;
        var target = list.Items.First();
        var staleVersion = target.Version;

        // Перший працівник (виконавець) зберігає діагностику -> версія зростає
        var firstEdit = await tech1.PutAsJsonAsync($"/api/requests/{target.Id}/diagnostics",
            new UpdateDiagnosticsDto("Оновлена діагностика виконавцем", staleVersion));
        firstEdit.EnsureSuccessStatusCode();

        // Другий працівник (приймальник) намагається додати роботу зі старою версією
        var secondEdit = await receiver.PostAsJsonAsync($"/api/requests/{target.Id}/works",
            new AddWorkItemDto("Додаткова робота приймальника", 300m, staleVersion));

        Assert.Equal(HttpStatusCode.Conflict, secondEdit.StatusCode);
        var body = await secondEdit.Content.ReadAsStringAsync();
        Assert.Contains("VERSION_CONFLICT", body);
    }
}
