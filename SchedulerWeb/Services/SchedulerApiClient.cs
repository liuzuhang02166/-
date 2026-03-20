using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using SchedulerWeb.Data;

namespace SchedulerWeb.Services;

public sealed class SchedulerApiClient
{
    private const string ApiBaseUrlKey = "scheduler.api.baseUrl.v1";
    private const string ApiTokenKey = "scheduler.api.token.v1";

    private readonly HttpClient _http;
    private readonly LocalStorageService _localStorage;
    private readonly JsonSerializerOptions _jsonOptions;

    public string ApiBaseUrl { get; private set; } = string.Empty;
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(ApiBaseUrl) && !string.IsNullOrWhiteSpace(_token);

    public SchedulerSnapshot Snapshot { get; private set; } = new(
        Teachers: [],
        Courses: [],
        Overrides: [],
        WeekNotes: []
    );

    private string? _token;

    public event Action? Changed;

    public SchedulerApiClient(HttpClient http, LocalStorageService localStorage)
    {
        _http = http;
        _localStorage = localStorage;
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public async Task LoadAsync()
    {
        ApiBaseUrl = (await _localStorage.GetStringAsync(ApiBaseUrlKey))?.Trim() ?? string.Empty;
        _token = (await _localStorage.GetStringAsync(ApiTokenKey))?.Trim();
        if (IsAuthenticated)
        {
            await RefreshSnapshotAsync();
        }
    }

    public async Task SetApiBaseUrlAsync(string baseUrl)
    {
        ApiBaseUrl = (baseUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            await _localStorage.RemoveAsync(ApiBaseUrlKey);
            Changed?.Invoke();
            return;
        }
        await _localStorage.SetStringAsync(ApiBaseUrlKey, ApiBaseUrl);
        Changed?.Invoke();
    }

    public async Task CheckHealthAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
            throw new InvalidOperationException("请先填写服务地址。");
        var url = BuildUrl("/health");
        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("服务不可用。");
    }

    public async Task LoginAsync(string password)
    {
        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
            throw new InvalidOperationException("请先填写服务地址。");
        var url = BuildUrl("/auth/login");
        using var resp = await _http.PostAsJsonAsync(url, new LoginRequest(password ?? string.Empty), _jsonOptions);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("登录失败：口令不正确或服务不可用。");
        var data = await resp.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions);
        if (data is null || string.IsNullOrWhiteSpace(data.Token))
            throw new InvalidOperationException("登录失败：服务返回异常。");

        _token = data.Token;
        await _localStorage.SetStringAsync(ApiTokenKey, _token);
        await RefreshSnapshotAsync();
        Changed?.Invoke();
    }

    public async Task LogoutAsync()
    {
        _token = null;
        await _localStorage.RemoveAsync(ApiTokenKey);
        Snapshot = new SchedulerSnapshot([], [], [], []);
        Changed?.Invoke();
    }

    public async Task RefreshSnapshotAsync()
    {
        EnsureAuthenticated();
        var url = BuildUrl("/api/snapshot");
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        using var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("拉取数据失败，请检查服务地址与登录状态。");
        var snapshot = await resp.Content.ReadFromJsonAsync<SchedulerSnapshot>(_jsonOptions);
        if (snapshot is null)
            throw new InvalidOperationException("拉取数据失败：服务返回异常。");
        Snapshot = snapshot;
        Changed?.Invoke();
    }

    private void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
            throw new InvalidOperationException("未登录云端服务。");
    }

    private string BuildUrl(string path)
    {
        var baseUrl = ApiBaseUrl.TrimEnd('/');
        return baseUrl + path;
    }

    private sealed record LoginRequest(string Password);
    private sealed record LoginResponse(string Token);
}

