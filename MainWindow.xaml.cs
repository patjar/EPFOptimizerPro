using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using EPFOptimizerPro.Models;
using EPFOptimizerPro.Services;

namespace EPFOptimizerPro;

public partial class MainWindow : Window
{
    private readonly SystemMetrics _metrics = new();
    private readonly SystemCountersService _systemCounters = new();
    private readonly AiAdvisorService _aiAdvisor = new();
    private readonly HealthScoreService _healthScores = new();
    private readonly AiScoreHistoryService _aiHistory = new();
    private readonly AiMemoryReportService _aiMemoryReport = new();
    private readonly DispatcherTimer _timer = new();
    private readonly DispatcherTimer _adminBlinkTimer = new();
    private bool _adminBlinkState;
    private readonly string[] _frames = { "◐", "◓", "◑", "◒" };
    private readonly GitHubUpdateService _updateService = new();
    private AdaptiveTaskEngine _engine;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _updateCts;
    private UpdateCheckResult? _lastUpdateCheck;
    private int _frameIndex;
    private int _lastWorkerCount;
    private string _lastWorkerMode = "non initialise";
    private string? _lastReport;

    public MainWindow()
    {
        InitializeComponent();
        ApplyDynamicAppVersion();
            InitializeDashboardScoreGauge();
        _engine = new AdaptiveTaskEngine(Dispatcher);
        WireEngine();
        ActiveTasksItems.ItemsSource = _engine.ActiveTasks;
        CompletedTasksItems.ItemsSource = _engine.CompletedTasks;
        UpdateDashboardSummary();
        UpdateSystemCounters();
        _timer.Interval = TimeSpan.FromMilliseconds(500);
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        _adminBlinkTimer.Interval = TimeSpan.FromMilliseconds(650);
        _adminBlinkTimer.Tick += (_, _) => AdminBlinkTick();
        RenderRecommendations(_engine.CurrentRecommendations);
        ShowStartupAdvice();
        UpdateAdminVisualStatus();
        RenderAiAdvisor(0, 0);
        TxtUpdateStatus.Text = UpdateStatusFormatter.Format("non v\u00e9rifi\u00e9");
        // Update download disabled until GitHub check has confirmed an available MSI.
        _lastUpdateCheck = null;
        BtnDownloadUpdate.IsEnabled = false;
        BtnOpenUpdateRelease.IsEnabled = false;
    }

    private void ShowStartupAdvice()
    {
        TxtActionHint.Text = StartupAdviceProvider.ActionHint;
        TxtStep.Text = StartupAdviceProvider.StepTitle;
        TxtPercent.Text = "0 %";
        ProgressGlobal.Value = 0;

        if (TxtAi.Text.Length == 0)
        {
            TxtAi.Text = StartupAdviceProvider.GetAssistantText();
        }
    }

    private void UpdateSystemCounters()
    {
        try
        {
            int openHandles = _systemCounters.GetOpenHandleCount();
            var services = _systemCounters.GetServiceCounts();

            TxtFilesCard.Text = openHandles.ToString("N0");
            ProgressFilesMini.Value = Math.Clamp(openHandles / 2000.0, 0, 100);

            TxtServicesCard.Text = services.Running + " / " + services.Resting;
            int total = services.Running + services.Resting;
            ProgressServicesMini.Value = total <= 0 ? 0 : Math.Clamp(services.Running * 100.0 / total, 0, 100);
        }
        catch
        {
            // Indicateurs informatifs uniquement : aucune erreur visuelle ne doit bloquer l'application.
        }
    }
    private void WireEngine()
    {
        _engine.GlobalProgressChanged += OnGlobalProgressChanged;
        _engine.LogWritten += OnLogWritten;
        _engine.RecommendationsUpdated += items => Dispatcher.Invoke(() => RenderRecommendations(items));
        _engine.ScoreUpdated += score => Dispatcher.Invoke(() => { /* Dashboard gauge is updated from HealthScore.Global. */ });
                _engine.WorkerModeChanged += (count, mode) =>
        {
            _lastWorkerCount = count;
            _lastWorkerMode = mode;
            Dispatcher.Invoke(() => TxtWorkers.Text = $"Workers : {count} | {mode}");
        };
    }

    private void Tick()
    {
        _frameIndex = (_frameIndex + 1) % _frames.Length;
        double cpu = _metrics.CpuPercent();
        double ram = _metrics.MemoryPercent();
        TxtClock.Text = $"{_frames[_frameIndex]} {DateTime.Now:HH:mm:ss}";
        TxtMetrics.Text = $"CPU {cpu:0} % | RAM {ram:0} %";
        ProgressCpuMini.Value = cpu;
        ProgressRamMini.Value = ram;
        TxtCpuCard.Text = $"{cpu:0} %";
        TxtRamCard.Text = $"{ram:0} %";
        UpdateDashboardSummary();
        UpdateSystemCounters();
    }

    private async void BtnAudit_Click(object sender, RoutedEventArgs e) => await RunAsync(false);
    private async void BtnOptimize_Click(object sender, RoutedEventArgs e) => await RunAsync(true);

    private async Task RunAsync(bool optimize)
    {
        if (_cts is not null) return;

        _cts = new CancellationTokenSource();
        TxtLog.Clear();
        SetRunningVisualState(optimize);

        double cpuStart = _metrics.CpuPercent();
        double memoryStart = _metrics.MemoryPercent();

        try
        {
            _lastReport = await _engine.RunAsync(optimize, _cts.Token, cpuStart, memoryStart);
            TxtActionHint.Text = ActionStatusTextProvider.CompletedTasksReportAvailable;
            RefreshAiDashboardV2(cpuStart, memoryStart);
        }
        catch (OperationCanceledException)
        {
            Append(AppLogTextProvider.OperationCanceled);
            TxtActionHint.Text = ActionStatusTextProvider.OperationCanceled;
        }
        finally
        {
            ResetButtonVisualState();
            _cts.Dispose();
            _cts = null;
        }
    }

    private void OnGlobalProgressChanged(int percent, string step)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressGlobal.Value = percent;
            TxtPercent.Text = percent + " %";
            TxtStep.Text = step;
        });
    }

    private void OnLogWritten(LogEntry entry)
    {
        Dispatcher.Invoke(() =>
        {
            Append(entry.ToString());
            TxtEvents.Text = _engine.Logs.Count <= 1 ? "1 événement" : $"{_engine.Logs.Count} événements";
        });
    }

    private void UpdateDashboardSummary()
    {
        int total = _engine.Tasks.Count;
        int done = _engine.CompletedTasks.Count(t => t.Status.Equals("Terminé", StringComparison.OrdinalIgnoreCase));
        int running = _engine.ActiveTasks.Count(t => t.Status.Equals("En cours", StringComparison.OrdinalIgnoreCase));
        int waiting = _engine.ActiveTasks.Count(t => t.Status.Equals("En attente", StringComparison.OrdinalIgnoreCase));
        int warn = _engine.CompletedTasks.Count(t => t.Status.Equals("Avertissement", StringComparison.OrdinalIgnoreCase));
        int error = _engine.CompletedTasks.Count(t => t.Status.Equals("Erreur", StringComparison.OrdinalIgnoreCase));
        string activeNames = string.Join(", ", _engine.ActiveTasks.Where(t => t.Status.Equals("En cours", StringComparison.OrdinalIgnoreCase)).Select(t => $"{t.Name} {t.Progress}%"));
        if (string.IsNullOrWhiteSpace(activeNames))
        {
            activeNames = "aucune tâche active";
        }

        bool hasActiveVisibleTasks = _engine.ActiveTasks.Any(t =>
            t.Status.Equals("En cours", StringComparison.OrdinalIgnoreCase) ||
            t.Status.Equals("En attente", StringComparison.OrdinalIgnoreCase));

        TxtNoActiveTasks.Visibility = hasActiveVisibleTasks
            ? Visibility.Collapsed
            : Visibility.Visible;
        TxtDashboardSummary.Text = $"  |  {done}/{total} terminées  |  {running} en cours : {activeNames}  |  {waiting} attente  |  {warn} avert.  |  {error} erreur";
    }


    private void RenderAiAdvisor(double cpuInitial, double ramInitial)
    {
        int score = 0;
        _ = int.TryParse(TxtScoreHero.Text, out score);

        var tips = _aiAdvisor.Analyze(
            _engine.Logs.Cast<object>(),
            _engine.CompletedTasks.Cast<object>(),
            score,
            _lastWorkerCount,
            _lastWorkerMode,
            cpuInitial,
            ramInitial);

        TxtAi.Clear();
                TxtAi.AppendText(_aiAdvisor.RenderText(tips));

        var health = _healthScores.Compute(
            _engine.Logs.Cast<object>(),
            _engine.CompletedTasks.Cast<object>(),
            score,
            _lastWorkerCount,
            _lastWorkerMode);

        TxtAi.AppendText(Environment.NewLine);
        TxtAi.AppendText("Scores IA par categorie" + Environment.NewLine);
        TxtAi.AppendText("-----------------------" + Environment.NewLine);
        TxtAi.AppendText(_healthScores.RenderText(health));
        TxtAi.AppendText(Environment.NewLine);
        TxtAi.ScrollToHome();
    }
    private void RenderRecommendations(IReadOnlyList<AiRecommendation> recommendations)
    {
        TxtAi.Text = AiDashboardTextFormatter.FormatRecommendations(recommendations);
        TxtAi.ScrollToHome();
    }
            private void RefreshAiDashboard(double cpuStart, double memoryStart)
    {
        int score = 0;
        _ = int.TryParse(TxtScoreHero.Text, out score);

        var health = _healthScores.Compute(
            _engine.Logs.Cast<object>(),
            _engine.CompletedTasks.Cast<object>(),
            score,
            _lastWorkerCount,
            _lastWorkerMode);

        _aiHistory.SaveSnapshot(health, _lastWorkerCount, _lastWorkerMode);

        TxtAiHeadline.Text = $"Santé IA : {health.Global}/100";
        SetDashboardScore(health.Global);
        TxtAiSubScore.Text = AiDashboardTextFormatter.FormatSubScore(health);

        var tips = _aiAdvisor.Analyze(
            _engine.Logs.Cast<object>(),
            _engine.CompletedTasks.Cast<object>(),
            score,
            _lastWorkerCount,
            _lastWorkerMode,
            cpuStart,
            memoryStart);

        TxtAi.Text = AiDashboardTextFormatter.FormatDetailedSynthesis(             _healthScores.RenderText(health),             _aiAdvisor.RenderText(tips));         TxtAi.ScrollToHome();
    }

    private void RefreshAiDashboardV2(double cpuStart, double memoryStart)
    {
        int score = 0;
        _ = int.TryParse(TxtScoreHero.Text, out score);

        var health = _healthScores.Compute(
            _engine.Logs.Cast<object>(),
            _engine.CompletedTasks.Cast<object>(),
            score,
            _lastWorkerCount,
            _lastWorkerMode);

        _aiHistory.SaveSnapshot(health, _lastWorkerCount, _lastWorkerMode);
        string trendText = _aiHistory.GetTrendText();

        TxtAiHeadline.Text = $"Santé IA : {health.Global}/100";
        SetDashboardScore(health.Global);
        TxtAiSubScore.Text = AiDashboardTextFormatter.FormatSubScore(health);
        TxtAiAdvice.Text = AiDashboardTextFormatter.FormatAdvice(health, trendText);

        var tips = _aiAdvisor.Analyze(
            _engine.Logs.Cast<object>(),
            _engine.CompletedTasks.Cast<object>(),
            score,
            _lastWorkerCount,
            _lastWorkerMode,
            cpuStart,
            memoryStart);

        TxtAi.Text = AiDashboardTextFormatter.FormatDetailedSynthesis(             _healthScores.RenderText(health),             trendText,             _aiAdvisor.RenderText(tips));         TxtAi.ScrollToHome();
    }
    private void Append(string text)
    {
        TxtLog.AppendText(text + Environment.NewLine);
        const int maxCharacters = 60000;
        if (TxtLog.Text.Length > maxCharacters)
        {
            TxtLog.Text = TxtLog.Text[^maxCharacters..];
        }
        TxtLog.ScrollToEnd();
    }

    private void SetRunningVisualState(bool optimize)
    {
        BtnAudit.IsEnabled = false;
        BtnOptimize.IsEnabled = false;
        BtnCheckUpdate.IsEnabled = false;
        BtnAudit.Background = UiBrushProvider.FromHex(optimize ? "#1E3A8A" : "#38BDF8");
        BtnOptimize.Background = UiBrushProvider.FromHex(optimize ? "#22C55E" : "#1E3A8A");
        BtnCancel.Background = UiBrushProvider.FromHex("#F59E0B");
        TxtActionHint.Text = ActionStatusTextProvider.AdaptiveRunStatus(optimize);
    }

    private void ResetButtonVisualState()
    {
        BtnAudit.IsEnabled = true;
        BtnOptimize.IsEnabled = true;
        BtnCheckUpdate.IsEnabled = true;
        BtnAudit.Background = UiBrushProvider.FromHex("#2563EB");
        BtnOptimize.Background = UiBrushProvider.FromHex("#2563EB");
        BtnCancel.Background = UiBrushProvider.FromHex("#2563EB");
    }

    private void UpdateScoreHero(int score)
    {
        TxtScoreHero.Text = score.ToString();
        TxtScoreHero.Foreground = score >= 85 ? UiBrushProvider.FromHex("#22C55E") : score >= 65 ? UiBrushProvider.FromHex("#F59E0B") : UiBrushProvider.FromHex("#EF4444");
    }

    
    private void ApplyDynamicAppVersion()
    {
        string displayName = "EPF Optimizer Pro Premium IA v" + ApplicationVersionProvider.GetDisplayVersion();
        Title = displayName;
        TxtAppVersionTitle.Text = displayName;
    }

private void UpdateAdminVisualStatus()
    {
        bool isAdmin = SystemPrivilegeService.IsRunningAsAdministrator();

        TxtAdminStatus.Text = isAdmin ? "Admin : oui" : "Admin : non";

        if (isAdmin)
        {
            _adminBlinkTimer.Stop();
            TxtAdminStatus.Visibility = Visibility.Visible;
            TxtAdminStatus.Foreground = UiBrushProvider.FromHex("#22C55E");
            Append(AppLogTextProvider.AdminLaunch);
            Append(UpdateLogTextProvider.PublicGitHubMode);
        }
        else
        {
            TxtAdminStatus.Visibility = Visibility.Visible;
            TxtAdminStatus.Foreground = UiBrushProvider.FromHex("#EF4444");
            _adminBlinkState = true;
            _adminBlinkTimer.Start();
            Append(AppLogTextProvider.NonAdminLaunch);
            TxtActionHint.Text = ActionStatusTextProvider.NonAdminLimitations;
        }
    }

    private void AdminBlinkTick()
    {
        if (TxtAdminStatus.Text != "Admin : non")
        {
            _adminBlinkTimer.Stop();
            TxtAdminStatus.Visibility = Visibility.Visible;
            return;
        }

        _adminBlinkState = !_adminBlinkState;
        TxtAdminStatus.Foreground = _adminBlinkState ? UiBrushProvider.FromHex("#EF4444") : UiBrushProvider.FromHex("#7F1D1D");
    }
private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        BtnCancel.Background = UiBrushProvider.FromHex("#EF4444");
        TxtActionHint.Text = ActionStatusTextProvider.CancellationRequested;
        _cts?.Cancel();
        _updateCts?.Cancel();
    }

    private void BtnOpenReport_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_lastReport) && File.Exists(_lastReport))
        {
            Process.Start(new ProcessStartInfo(_lastReport) { UseShellExecute = true });
        }
        else
        {
            MessageBox.Show("Aucun rapport disponible.", "EPF Optimizer Pro", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

                private void BtnOpenLearning_Click(object sender, RoutedEventArgs e)
    {
        var window = new AiCenterWindow
        {
            Owner = this
        };

        window.ShowDialog();
        Append(AppLogTextProvider.AiCenterOpened);
    }

private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_cts is not null) return;

        BtnCheckUpdate.IsEnabled = false;
        TxtUpdateStatus.Text = UpdateStatusTextProvider.CheckingGithub;
        Append(UpdateLogTextProvider.CheckingGitHubUpdates);

        try
        {
            _lastUpdateCheck = await _updateService.CheckLatestAsync(CancellationToken.None);
            TxtUpdateStatus.Text = _lastUpdateCheck.UpdateAvailable
                ? UpdateStatusTextProvider.CheckResult(true, _lastUpdateCheck.LatestVersion)
                : UpdateStatusTextProvider.NoUpdate(_lastUpdateCheck.CurrentVersion);
            BtnOpenUpdateRelease.IsEnabled = !string.IsNullOrWhiteSpace(_lastUpdateCheck.ReleaseUrl);
            BtnDownloadUpdate.IsEnabled = _lastUpdateCheck.UpdateAvailable && _lastUpdateCheck.Asset is not null;

            if (_lastUpdateCheck.UpdateAvailable)
            {
                Append(UpdateLogTextProvider.UpdateAvailable(_lastUpdateCheck.LatestVersion));
            }
            else
            {
                Append(UpdateLogTextProvider.NoUpdateAvailable(_lastUpdateCheck.CurrentVersion, _lastUpdateCheck.LatestVersion));
            }
        }
        catch (Exception ex)
        {
            TxtUpdateStatus.Text = UpdateStatusTextProvider.GithubError;
            Append(UpdateLogTextProvider.GitHubUpdateError(ex.Message));
        }
        finally
        {
            BtnCheckUpdate.IsEnabled = true;
        }
    }

    private static void ValidateDownloadedMsi(string msiPath)
    {
        if (string.IsNullOrWhiteSpace(msiPath))
        {
            throw new InvalidOperationException("MSI update path is empty.");
        }

        if (!File.Exists(msiPath))
        {
            throw new FileNotFoundException("MSI update introuvable.", msiPath);
        }

        if (!string.Equals(Path.GetExtension(msiPath), ".msi", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Le fichier telecharge n'est pas un MSI : " + msiPath);
        }

        string fileName = Path.GetFileName(msiPath);
        if (!fileName.StartsWith("EPFOptimizerPro-Setup-v", StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Nom MSI update inattendu : " + fileName);
        }

        FileInfo fileInfo = new(msiPath);
        if (fileInfo.Length <= 0)
        {
            throw new InvalidOperationException("MSI update vide : " + msiPath);
        }
    }
    private void StartUpdateInstallerAndExit(string msiPath)
    {
        int targetPid = Environment.ProcessId;
        string exePath = Path.Combine(AppContext.BaseDirectory, "EPFOptimizerPro.exe");
        string logPath = Path.Combine(Path.GetTempPath(), "EPFOptimizerPro-update-install.log");
        string wrapperLogPath = Path.Combine(Path.GetTempPath(), "EPFOptimizerPro-update-wrapper.log");
        string scriptPath = Path.Combine(Path.GetTempPath(), "EPFOptimizerPro-update-install.ps1");

        string script = string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Stop'",
            "$msi = " + ToPowerShellSingleQuoted(msiPath),
            "$exe = " + ToPowerShellSingleQuoted(exePath),
            "$msiLog = " + ToPowerShellSingleQuoted(logPath),
            "$wrapperLog = " + ToPowerShellSingleQuoted(wrapperLogPath),
            "$targetPid = " + targetPid.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "$expectedThumbprint = 'A2AA77761B29B66D7F67C5E272F0797954DEB101'",
            "function Write-EpfUpdateLog([string]$message) {",
            "    $line = '[' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '] ' + $message",
            "    Write-Host $line",
            "    Add-Content -Path $wrapperLog -Value $line -Encoding UTF8",
            "}",
            "try {",
            "    Write-EpfUpdateLog 'EPF update installer wrapper started.'",
            "    Write-EpfUpdateLog ('MSI path: ' + $msi)",
            "    Write-EpfUpdateLog ('MSI log path: ' + $msiLog)",
            "    Write-EpfUpdateLog ('Wrapper log path: ' + $wrapperLog)",
            "    if (-not (Test-Path $msi)) { throw 'MSI file not found: ' + $msi }",
            "    $hash = Get-FileHash -Path $msi -Algorithm SHA256",
            "    Write-EpfUpdateLog ('MSI SHA256: ' + $hash.Hash)",
            "    $sig = Get-AuthenticodeSignature -FilePath $msi",             "    $signerThumbprint = if ($sig.SignerCertificate) { $sig.SignerCertificate.Thumbprint } else { '' }",             "    Write-EpfUpdateLog ('MSI signature status: ' + $sig.Status)",             "    Write-EpfUpdateLog ('MSI signature message: ' + $sig.StatusMessage)",             "    Write-EpfUpdateLog ('MSI signer thumbprint: ' + $signerThumbprint)",             "    $knownSigner = ($signerThumbprint -replace ' ', '').Equals($expectedThumbprint, [System.StringComparison]::OrdinalIgnoreCase)",             "    if (($sig.Status -ne 'Valid') -and (-not $knownSigner)) { throw 'MSI signature invalid: ' + $sig.Status + ' / ' + $sig.StatusMessage }",             "    if (($sig.Status -ne 'Valid') -and $knownSigner) { Write-EpfUpdateLog 'Known signer accepted: PROD_CLEARPASS thumbprint matched despite untrusted certificate chain.' }",
            "    $limit = (Get-Date).AddSeconds(120)",
            "    while ((Get-Process -Id $targetPid -ErrorAction SilentlyContinue) -and ((Get-Date) -lt $limit)) { Start-Sleep -Milliseconds 500 }",
            "    Start-Sleep -Seconds 2",
            "    Write-EpfUpdateLog 'Launching msiexec.'",
            "    $arguments = @('/i', $msi, '/qn', '/norestart', '/L*v', $msiLog)",
            "    Write-EpfUpdateLog ('msiexec arguments: ' + ($arguments -join ' '))",
            "    $p = Start-Process -FilePath 'msiexec.exe' -ArgumentList $arguments -Wait -PassThru",
            "    Write-EpfUpdateLog ('msiexec exit code: ' + $p.ExitCode)",
            "    if ($p.ExitCode -notin @(0,3010)) { throw 'MSI install failed with exit code: ' + $p.ExitCode + ' - log: ' + $msiLog }",
            "    if (Test-Path $exe) { Write-EpfUpdateLog ('Relaunching app: ' + $exe); Start-Process -FilePath $exe } else { Write-EpfUpdateLog ('App executable not found after install: ' + $exe) }",
            "    Write-EpfUpdateLog 'EPF update installer wrapper finished successfully.'",
            "    Write-EpfUpdateLog 'Auto-update install completed successfully.'",
            "    exit 0",
            "}",
            "catch {",
            "    $errorText = $_.Exception.Message",
            "    Write-EpfUpdateLog ('ERROR: ' + $errorText)",
            "    Write-Host ''",
            "    Write-Host 'EPF update failed. The window will stay open so the error can be read.' -ForegroundColor Red",
            "    Write-Host ('Wrapper log: ' + $wrapperLog) -ForegroundColor Yellow",
            "    Write-Host ('MSI log    : ' + $msiLog) -ForegroundColor Yellow",
            "    Write-Host ''",
            "    Read-Host 'Press Enter to close this window'",
            "    exit 1",
            "}"
        });

        File.WriteAllText(scriptPath, script, new System.Text.UTF8Encoding(false));

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
            UseShellExecute = true,
            Verb = "runas"
        });

        Application.Current.Shutdown();
    }
    private static string ToPowerShellSingleQuoted(string value)
    {
        return "'" + value.Replace("'", "''") + "'";
    }
    private async void BtnDownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_lastUpdateCheck?.Asset is null || _cts is not null || _updateCts is not null) return;

        _updateCts = new CancellationTokenSource();
        BtnCheckUpdate.IsEnabled = false;
        ProgressGlobal.Value = 0;
        TxtPercent.Text = "0 %";
        TxtStep.Text = UpdateUiTextProvider.DownloadStep;
        TxtUpdateStatus.Text = UpdateStatusTextProvider.DownloadingGithubMsi;
        Append(UpdateLogTextProvider.DownloadingUpdate(_lastUpdateCheck.Asset.Name));

        try
        {
            Progress<double> progress = new(value =>
            {
                Dispatcher.Invoke(() =>
                {
                    double safe = Math.Max(0, Math.Min(100, value));
                    ProgressGlobal.Value = safe;
                    TxtPercent.Text = safe.ToString("0") + " %";
                });
            });

            string msiPath = await _updateService.DownloadAsync(_lastUpdateCheck.Asset, progress, _updateCts.Token);
            ValidateDownloadedMsi(msiPath);
            Append(UpdateLogTextProvider.MsiValid(msiPath));
            TxtUpdateStatus.Text = UpdateStatusTextProvider.Downloaded(Path.GetFileName(msiPath));
            TxtStep.Text = UpdateUiTextProvider.DownloadedStep;
            ProgressGlobal.Value = 100;
            TxtPercent.Text = "100 %";
            Append(UpdateLogTextProvider.MsiDownloaded(msiPath));
            Append(UpdateLogTextProvider.AutomaticUpdateInstall);
            Append(UpdateLogTextProvider.VerifyMsiSignatureBeforeInstall);
            Append(UpdateLogTextProvider.UpdateInstallLogPath(Path.Combine(Path.GetTempPath(), "EPFOptimizerPro-update-install.log")));
            TxtUpdateStatus.Text = UpdateStatusTextProvider.Installation();
            TxtStep.Text = UpdateUiTextProvider.InstallStep;
            StartUpdateInstallerAndExit(msiPath);
            return;
        }
        catch (OperationCanceledException)
        {
            TxtUpdateStatus.Text = UpdateStatusTextProvider.UpdateDownloadCanceled;
            Append(UpdateLogTextProvider.UpdateDownloadCanceled);
        }
        catch (Exception ex)
        {
            TxtUpdateStatus.Text = UpdateStatusTextProvider.UpdateDownloadError;
            Append(UpdateLogTextProvider.UpdateDownloadError(ex.Message));
        }
        finally
        {
            BtnCheckUpdate.IsEnabled = true;
            BtnDownloadUpdate.IsEnabled = _lastUpdateCheck?.UpdateAvailable == true && _lastUpdateCheck.Asset is not null;
            _updateCts?.Dispose();
            _updateCts = null;
        }
    }


            private void BtnOpenUpdateRelease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = "https://github.com/patjar/EPFOptimizerPro/releases/latest";

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Impossible d'ouvrir la page GitHub Release : " + ex.Message,
                    "EPF Optimizer Pro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _updateCts?.Cancel();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _cts?.Dispose();
        base.OnClosed(e);
    }

    private void TxtAi_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshDashboardScoreFromAiText();
    }

    private void InitializeDashboardScoreGauge()
    {
        SetDashboardScore(0);
    }

    private void RefreshDashboardScoreFromAiText()
    {
        string text = TxtAi?.Text ?? string.Empty;
        int score = DashboardScoreParser.ExtractFromText(text);
        SetDashboardScore(score);
    }
private void SetDashboardScore(int score)
    {
        score = DashboardScoreParser.Clamp(score);
        TxtScoreHero.Text = score.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (score <= 0)
        {
            TxtScoreHero.Foreground = UiBrushProvider.FromHex("#64748B");
        }
        else if (score >= 85)
        {
            TxtScoreHero.Foreground = UiBrushProvider.FromHex("#22C55E");
        }
        else if (score >= 70)
        {
            TxtScoreHero.Foreground = UiBrushProvider.FromHex("#F59E0B");
        }
        else
        {
            TxtScoreHero.Foreground = UiBrushProvider.FromHex("#EF4444");
        }
    }
}
