# Feature Map

## Repository Scan Summary
- Current repository contains only `README.md` and no source projects or implementation files were found.
- No existing capabilities beyond the placeholder README are implemented yet.

## Implemented Capabilities (Discovered)
- None identified in the current repository state.

## Missing Capabilities (Typical Industrial Automotive EOL Platform)

### 1) Flashing (FlashRunner-style control port + log port, scriptable commands)
- **Project**: `Platform.FlashRunner`
- **Public interface proposal (C#)**:
  ```csharp
  namespace Platform.FlashRunner
  {
      public interface IFlashRunnerClient
      {
          Task ConnectAsync(FlashRunnerEndpoint endpoint, CancellationToken ct);
          Task DisconnectAsync(CancellationToken ct);
          Task<FlashCommandResult> ExecuteAsync(FlashCommand command, CancellationToken ct);
          IAsyncEnumerable<FlashLogLine> StreamLogsAsync(CancellationToken ct);
      }

      public record FlashRunnerEndpoint(string ControlPort, string LogPort, int BaudRate);
      public record FlashCommand(string ScriptLine, TimeSpan Timeout);
      public record FlashCommandResult(bool Success, string RawResponse, int ExitCode);
      public record FlashLogLine(DateTimeOffset Timestamp, string Line);
  }
  ```
- **Config keys needed**:
  - `flashRunner.controlPort`
  - `flashRunner.logPort`
  - `flashRunner.baudRate`
  - `flashRunner.commandTimeoutMs`
  - `flashRunner.logBufferLines`
- **Test strategy**:
  - Unit: command parsing, timeout handling, log line parsing, configuration binding.
  - Integration: connect to loopback serial emulator; execute canned scripts; verify log streaming.

### 2) UDS diagnostics routines framework (request/response, sessions, security access, DID read/write)
- **Project**: `Platform.Diagnostics.Uds`
- **Public interface proposal (C#)**:
  ```csharp
  namespace Platform.Diagnostics.Uds
  {
      public interface IUdsClient
      {
          Task<UdsResponse> SendAsync(UdsRequest request, CancellationToken ct);
          Task<UdsSessionResult> SwitchSessionAsync(UdsSession session, CancellationToken ct);
          Task<UdsSecurityAccessResult> UnlockAsync(UdsSecurityLevel level, CancellationToken ct);
          Task<UdsReadResult> ReadDidAsync(ushort did, CancellationToken ct);
          Task<UdsWriteResult> WriteDidAsync(ushort did, ReadOnlyMemory<byte> data, CancellationToken ct);
      }

      public record UdsRequest(byte ServiceId, ReadOnlyMemory<byte> Payload);
      public record UdsResponse(byte ServiceId, ReadOnlyMemory<byte> Payload, bool IsNegativeResponse);
      public enum UdsSession { Default, Extended, Programming }
      public enum UdsSecurityLevel { Level1, Level2, Level3 }
  }
  ```
- **Config keys needed**:
  - `uds.canInterface`
  - `uds.requestId`
  - `uds.responseId`
  - `uds.responseTimeoutMs`
  - `uds.security.seedKeyProvider`
- **Test strategy**:
  - Unit: request framing, response decoding, NRC handling, DID payload encoding.
  - Integration: simulated ECU (UDS test server) for session switching and security access.

### 3) MES integration abstraction (offline mock + real adapter interface)
- **Project**: `Platform.Mes`
- **Public interface proposal (C#)**:
  ```csharp
  namespace Platform.Mes
  {
      public interface IMesAdapter
      {
          Task<MesStartResponse> StartJobAsync(MesStartRequest request, CancellationToken ct);
          Task<MesReportResponse> ReportResultAsync(MesReportRequest request, CancellationToken ct);
          Task<MesHeartbeatResponse> HeartbeatAsync(MesHeartbeatRequest request, CancellationToken ct);
      }

      public record MesStartRequest(string Vin, string StationId, string OperatorId);
      public record MesStartResponse(bool Accepted, string? OrderId, string? Message);
      public record MesReportRequest(string OrderId, string Vin, bool Passed, string PayloadJson);
      public record MesReportResponse(bool Accepted, string? Message);
      public record MesHeartbeatRequest(string StationId, DateTimeOffset Timestamp);
      public record MesHeartbeatResponse(bool Ok, string? Message);
  }
  ```
- **Config keys needed**:
  - `mes.mode` (values: `mock`, `adapter`)
  - `mes.adapterUrl`
  - `mes.timeoutMs`
  - `mes.retryCount`
- **Test strategy**:
  - Unit: serialization/deserialization, retry policy.
  - Integration: contract tests against mock server; adapter tests in staging MES.

### 4) Instrument health check + auto-recover (re-init CAN card, USB reconnect)
- **Project**: `Platform.Hardware`
- **Public interface proposal (C#)**:
  ```csharp
  namespace Platform.Hardware
  {
      public interface IInstrumentHealthMonitor
      {
          Task<HealthSnapshot> GetSnapshotAsync(CancellationToken ct);
          Task<RecoverResult> RecoverAsync(RecoverAction action, CancellationToken ct);
      }

      public record HealthSnapshot(bool CanOk, bool UsbOk, bool PowerOk, string[] ActiveAlerts);
      public enum RecoverAction { ReinitCanCard, ReconnectUsbDevice, PowerCycle }
      public record RecoverResult(bool Success, string Message);
  }
  ```
- **Config keys needed**:
  - `hardware.health.pollIntervalMs`
  - `hardware.recover.maxAttempts`
  - `hardware.can.interfaceName`
  - `hardware.usb.deviceId`
- **Test strategy**:
  - Unit: alert mapping, retry/backoff logic.
  - Integration: hardware-in-loop or simulator to validate recovery actions.

### 5) Limit tables versioning + traceability snapshot in report
- **Project**: `Platform.Quality`
- **Public interface proposal (C#)**:
  ```csharp
  namespace Platform.Quality
  {
      public interface ILimitTableStore
      {
          Task<LimitTable> GetActiveAsync(string stationId, CancellationToken ct);
          Task<LimitTableSnapshot> SnapshotAsync(string stationId, CancellationToken ct);
      }

      public record LimitTable(string Version, IReadOnlyDictionary<string, LimitSpec> Limits);
      public record LimitSpec(double Min, double Max, string Unit);
      public record LimitTableSnapshot(string Version, DateTimeOffset CapturedAt, string Hash);
  }
  ```
- **Config keys needed**:
  - `limits.store.type` (values: `file`, `db`)
  - `limits.store.path`
  - `limits.snapshot.includeInReport` (bool)
- **Test strategy**:
  - Unit: version resolution, snapshot hashing, schema validation.
  - Integration: report export includes snapshot metadata.

### 6) CPK calculation pipeline (offline report analyzer module)
- **Project**: `Platform.Analytics`
- **Public interface proposal (C#)**:
  ```csharp
  namespace Platform.Analytics
  {
      public interface ICpkAnalyzer
      {
          Task<CpkReport> AnalyzeAsync(CpkInput input, CancellationToken ct);
      }

      public record CpkInput(string DataSetId, IReadOnlyList<double> Samples, double Lsl, double Usl);
      public record CpkReport(string DataSetId, double Cpk, double Mean, double Sigma);
  }
  ```
- **Config keys needed**:
  - `analytics.cpk.minSampleSize`
  - `analytics.cpk.reportOutputPath`
- **Test strategy**:
  - Unit: statistical correctness on known datasets.
  - Integration: pipeline ingesting exported reports to generate CPK summaries.

### 7) Plugin DLL steps (load from /plugins, reflection, sandboxing, versioning)
- **Project**: `Platform.Plugins`
- **Public interface proposal (C#)**:
  ```csharp
  namespace Platform.Plugins
  {
      public interface IPluginLoader
      {
          Task<IReadOnlyList<PluginDescriptor>> DiscoverAsync(string path, CancellationToken ct);
          Task<IPluginStep> LoadAsync(PluginDescriptor descriptor, CancellationToken ct);
      }

      public interface IPluginStep
      {
          string Name { get; }
          Version Version { get; }
          Task<PluginStepResult> ExecuteAsync(PluginContext context, CancellationToken ct);
      }

      public record PluginDescriptor(string Name, Version Version, string AssemblyPath, string TypeName);
      public record PluginStepResult(bool Passed, string Message);
      public record PluginContext(string StationId, string Vin, string PayloadJson);
  }
  ```
- **Config keys needed**:
  - `plugins.path`
  - `plugins.sandbox.enabled`
  - `plugins.allowedAssemblies`
  - `plugins.minVersion`
- **Test strategy**:
  - Unit: reflection discovery, version validation.
  - Integration: load test plugins from `/plugins` directory and execute in sandbox.

### 8) Supervisor multi-station scaling (1-100 slots), UI virtualization
- **Project**: `Platform.Supervisor` (services) and `Platform.UI` (frontend)
- **Public interface proposal (C#)**:
  ```csharp
  namespace Platform.Supervisor
  {
      public interface IStationSupervisor
      {
          Task RegisterAsync(StationDescriptor station, CancellationToken ct);
          Task UpdateStatusAsync(string stationId, StationStatus status, CancellationToken ct);
          Task<IReadOnlyList<StationStatus>> GetStatusesAsync(CancellationToken ct);
      }

      public record StationDescriptor(string StationId, string DisplayName, int SlotIndex);
      public record StationStatus(string StationId, bool Online, string CurrentJob, string State);
  }
  ```
- **Config keys needed**:
  - `supervisor.maxStations`
  - `supervisor.refreshIntervalMs`
  - `ui.virtualization.pageSize`
  - `ui.virtualization.bufferSize`
- **Test strategy**:
  - Unit: slot allocation, status diffing.
  - Integration: simulate 100 stations and verify UI virtualization performance.

## Prioritized Implementation Roadmap

### P0 (Foundational Core)
1) **MES integration abstraction**
   - Acceptance criteria:
     - Mock adapter operational with deterministic responses.
     - Real adapter interface contract defined and tested.
     - Config-driven mode switching verified.
2) **UDS diagnostics framework**
   - Acceptance criteria:
     - Basic request/response and session switching functional.
     - Security access and DID read/write covered by integration tests.
3) **Instrument health check + auto-recover**
   - Acceptance criteria:
     - Health polling and recovery actions implemented for CAN + USB.
     - Auto-recover attempts logged and rate-limited.

### P1 (Production Quality)
1) **Flashing control + log ports**
   - Acceptance criteria:
     - Scriptable command execution with log streaming.
     - Failure handling and retries configurable.
2) **Limit tables versioning + traceability snapshot**
   - Acceptance criteria:
     - Limit table store supports versioning.
     - Report includes limit snapshot with hash.

### P2 (Scale + Extensibility + Analytics)
1) **Supervisor multi-station scaling + UI virtualization**
   - Acceptance criteria:
     - Supervisor handles 1-100 stations with stable refresh rates.
     - UI virtualization validated with large station list.
2) **Plugin DLL steps**
   - Acceptance criteria:
     - Plugin discovery and execution with versioning enforced.
     - Sandbox mode toggleable and verified.
3) **CPK calculation pipeline**
   - Acceptance criteria:
     - Offline analyzer produces CPK report from historical data.
     - Output integrated into reporting workflow.
