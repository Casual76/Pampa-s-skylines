#nullable enable

namespace PampaSkylines.PC
{
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PampaSkylines.Core;
using PampaSkylines.SaveSync;

public sealed class PcSaveSyncCoordinator
{
    private readonly PcSimulationSession _session;
    private readonly LocalCitySaveStore _localStore;
    private readonly Queue<PendingRemoteSync> _remoteQueue = new();

    private BackendApiClient? _backendApiClient;
    private HttpClient? _httpClient;
    private string _accessToken = string.Empty;
    private float _autosaveTimer;
    private float _remoteRetryTimer;
    private Task? _activeOperation;

    public PcSaveSyncCoordinator(PcSimulationSession session, string rootPath, float autosaveIntervalSeconds = 45f)
    {
        _session = session;
        _localStore = new LocalCitySaveStore(rootPath);
        AutosaveIntervalSeconds = autosaveIntervalSeconds < 10f ? 10f : autosaveIntervalSeconds;
        StatusText = "Sistema salvataggi pronto.";
    }

    public event Action<string, PcStatusTone>? StatusChanged;

    public float AutosaveIntervalSeconds { get; set; }

    public string SaveRootPath => _localStore.RootPath;

    public string StatusText { get; private set; }

    public PcStatusTone StatusTone { get; private set; }

    public string LastSavedVersion { get; private set; } = string.Empty;

    public DateTimeOffset? LastSavedAtUtc { get; private set; }

    public bool IsBusy => _activeOperation is not null && !_activeOperation.IsCompleted;

    public bool HasPendingRemoteSync => _remoteQueue.Count > 0;

    public void ConfigureRemoteSync(string baseUrl, string accessToken)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(accessToken))
        {
            DisableRemoteSync();
            return;
        }

        _httpClient?.Dispose();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };
        _backendApiClient = new BackendApiClient(_httpClient);
        _accessToken = accessToken;
        SetStatus("Sync remoto configurato.");
    }

    public void DisableRemoteSync()
    {
        _backendApiClient = null;
        _httpClient?.Dispose();
        _httpClient = null;
        _accessToken = string.Empty;
        _remoteQueue.Clear();
        SetStatus("Sync remoto disattivato.", PcStatusTone.Warning);
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (unscaledDeltaTime <= 0f)
        {
            return;
        }

        _autosaveTimer += unscaledDeltaTime;

        if (!IsBusy && _autosaveTimer >= AutosaveIntervalSeconds)
        {
            _autosaveTimer = 0f;
            _activeOperation = SaveNowInternalAsync("autosave", "autosave");
            return;
        }

        if (!HasPendingRemoteSync || IsBusy)
        {
            return;
        }

        _remoteRetryTimer -= unscaledDeltaTime;
        if (_remoteRetryTimer <= 0f)
        {
            _activeOperation = FlushRemoteQueueAsync();
        }
    }

    public Task SaveNowAsync(string saveSlotId = "manual", string saveReason = "manual")
    {
        if (IsBusy)
        {
            SetStatus("Sistema salvataggi occupato.", PcStatusTone.Warning);
            return _activeOperation ?? Task.CompletedTask;
        }

        _autosaveTimer = 0f;
        _activeOperation = SaveNowInternalAsync(saveSlotId, saveReason);
        return _activeOperation;
    }

    public async Task<bool> LoadCurrentAsync(string? cityId = null, CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            SetStatus("Sistema salvataggi occupato.", PcStatusTone.Warning);
            return false;
        }

        try
        {
            SetStatus("Caricamento salvataggio locale...");
            var targetCityId = string.IsNullOrWhiteSpace(cityId) ? _session.State.CityId : cityId;
            var loadResult = await _localStore.LoadCurrentWithRecoveryAsync(targetCityId, cancellationToken);
            _session.RestoreSnapshot(loadResult.Snapshot);
            LastSavedVersion = loadResult.SourceVersion;
            LastSavedAtUtc = loadResult.Snapshot.SavedAtUtc;
            SetStatus(
                loadResult.RecoveredFromBackup
                    ? $"Recuperato backup {loadResult.SourceVersion}."
                    : $"Caricato {loadResult.SourceVersion}.");
            _autosaveTimer = 0f;
            return true;
        }
        catch (Exception exception)
        {
            SetStatus($"Caricamento fallito: {exception.Message}", PcStatusTone.Error);
            return false;
        }
    }

    public async Task<bool> LoadVersionAsync(string version, string? cityId = null, CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            SetStatus("Sistema salvataggi occupato.", PcStatusTone.Warning);
            return false;
        }

        try
        {
            SetStatus($"Caricamento backup {version}...");
            var targetCityId = string.IsNullOrWhiteSpace(cityId) ? _session.State.CityId : cityId;
            var snapshot = await _localStore.LoadVersionAsync(targetCityId, version, cancellationToken);
            _session.RestoreSnapshot(snapshot);
            LastSavedVersion = snapshot.Version;
            LastSavedAtUtc = snapshot.SavedAtUtc;
            _autosaveTimer = 0f;
            SetStatus($"Backup {version} caricato.");
            return true;
        }
        catch (Exception exception)
        {
            SetStatus($"Caricamento backup fallito: {exception.Message}", PcStatusTone.Error);
            return false;
        }
    }

    public Task<IReadOnlyList<LocalCitySlotSummary>> ListCitiesAsync(CancellationToken cancellationToken = default)
    {
        return _localStore.ListCitiesAsync(cancellationToken);
    }

    public IReadOnlyList<string> ListBackupVersions()
    {
        return _localStore.ListBackupVersions(_session.State.CityId);
    }

    public void ResetAutosaveTimer()
    {
        _autosaveTimer = 0f;
    }

    private async Task SaveNowInternalAsync(string saveSlotId, string saveReason)
    {
        try
        {
            SetStatus(saveReason == "autosave" ? "Autosalvataggio in corso..." : "Salvataggio in corso...");
            var snapshot = _session.CreateSnapshot(saveSlotId, saveReason);
            await _localStore.SaveAsync(snapshot);
            LastSavedVersion = snapshot.Version;
            LastSavedAtUtc = snapshot.SavedAtUtc;
            SetStatus(saveReason == "autosave" ? $"Autosalvato {snapshot.Version}." : $"Salvato {snapshot.Version}.");

            if (_backendApiClient is not null && !string.IsNullOrWhiteSpace(_accessToken))
            {
                _remoteQueue.Enqueue(new PendingRemoteSync
                {
                    Snapshot = snapshot.DeepClone(),
                    Head = SyncHead.FromSnapshot(snapshot)
                });
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Salvataggio fallito: {exception.Message}", PcStatusTone.Error);
        }
    }

    private async Task FlushRemoteQueueAsync()
    {
        if (_backendApiClient is null || string.IsNullOrWhiteSpace(_accessToken))
        {
            _remoteQueue.Clear();
            return;
        }

        while (_remoteQueue.Count > 0)
        {
            var pending = _remoteQueue.Peek();

            try
            {
                SetStatus($"Sincronizzazione {pending.Head.Version}...");
                var response = await _backendApiClient.UploadSnapshotAsync(
                    _accessToken,
                    new UploadSnapshotRequest
                    {
                        Head = pending.Head,
                        Snapshot = pending.Snapshot
                    });

                if (response is null)
                {
                    SetStatus("Sync fallita: risposta vuota.", PcStatusTone.Error);
                    _remoteRetryTimer = 15f;
                    return;
                }

                if (response.Applied || string.Equals(response.Reason, "duplicate_version", StringComparison.Ordinal))
                {
                    _remoteQueue.Dequeue();
                    SetStatus($"Sincronizzato {pending.Head.Version}.");
                    continue;
                }

                if (string.Equals(response.Reason, "stale_head", StringComparison.Ordinal) ||
                    string.Equals(response.Reason, "version_conflict", StringComparison.Ordinal))
                {
                    SetStatus($"Conflitto sync: {response.Reason}.", PcStatusTone.Warning);
                    _remoteRetryTimer = 30f;
                    return;
                }

                SetStatus($"Sync bloccata: {response.Reason}.", PcStatusTone.Error);
                _remoteRetryTimer = 30f;
                return;
            }
            catch (Exception exception)
            {
                SetStatus($"Sync non raggiungibile: {exception.Message}", PcStatusTone.Warning);
                _remoteRetryTimer = 20f;
                return;
            }
        }
    }

    private void SetStatus(string message, PcStatusTone tone = PcStatusTone.Neutral)
    {
        StatusText = message;
        StatusTone = tone;
        StatusChanged?.Invoke(message, tone);
    }

    private sealed class PendingRemoteSync
    {
        public CitySnapshot Snapshot { get; set; } = new();

        public SyncHead Head { get; set; } = new();
    }
}
}
