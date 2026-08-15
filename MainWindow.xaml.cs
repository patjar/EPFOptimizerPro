using System.Diagnostics;
using System.IO;
using System.Security.Principal;
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
        TxtActionHint.Text = "Conseil : lancez Audit seul pour analyser le poste, ou Optimiser pour corriger automatiquement.";
        TxtStep.Text = "Conseil de démarrage";
        TxtPercent.Text = "0 %";
        ProgressGlobal.Value = 0;

        if (TxtAi.Text.Length == 0)
        {
            TxtAi.Text = "Conseil de démarrage\n\nLancez Audit seul pour obtenir un diagnostic du poste. Utilisez Optimiser quand vous voulez appliquer les corrections automatiquement.\n";
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
            TxtActionHint.Text = "Tâches terminées. Rapport disponible.";
            RefreshAiDashboardV2(cpuStart, memoryStart);
        }
        catch (OperationCanceledException)
        {
            Append("[WARN] Opération annulée.");
            TxtActionHint.Text = "Opération annulée.";
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
        TxtAi.Clear();
        TxtAi.AppendText("Assistant IA local" + Environment.NewLine);
        TxtAi.AppendText("==================" + Environment.NewLine + Environment.NewLine);
        TxtAi.AppendText("Lance un audit ou une optimisation pour générer la synthèse IA." + Environment.NewLine + Environment.NewLine);

        foreach (var item in recommendations)
        {
            TxtAi.AppendText($"[{item.Severity}] {item.Title}" + Environment.NewLine);
            TxtAi.AppendText(item.Detail + Environment.NewLine + Environment.NewLine);
        }

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
        TxtAiSubScore.Text = $"Perf {health.Performance} | Sécu {health.Security} | Stockage {health.Storage} | Update {health.WindowsUpdate} | Stabilité {health.Stability}";

        var tips = _aiAdvisor.Analyze(
            _engine.Logs.Cast<object>(),
            _engine.CompletedTasks.Cast<object>(),
            score,
            _lastWorkerCount,
            _lastWorkerMode,
            cpuStart,
            memoryStart);

        TxtAi.Clear();
        TxtAi.AppendText("Synthèse IA détaillée" + Environment.NewLine);
        TxtAi.AppendText("====================" + Environment.NewLine + Environment.NewLine);
        TxtAi.AppendText(_healthScores.RenderText(health));
        TxtAi.AppendText(Environment.NewLine);
        TxtAi.AppendText(Environment.NewLine);
        TxtAi.AppendText("Conseils" + Environment.NewLine);
        TxtAi.AppendText("--------" + Environment.NewLine);
        TxtAi.AppendText(_aiAdvisor.RenderText(tips));
        TxtAi.ScrollToHome();
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
        TxtAiSubScore.Text = $"Perf {health.Performance} | Sécu {health.Security} | Stockage {health.Storage} | Update {health.WindowsUpdate} | Stabilité {health.Stability}";
        TxtAiAdvice.Text = health.Summary + Environment.NewLine + trendText;

        var tips = _aiAdvisor.Analyze(
            _engine.Logs.Cast<object>(),
            _engine.CompletedTasks.Cast<object>(),
            score,
            _lastWorkerCount,
            _lastWorkerMode,
            cpuStart,
            memoryStart);

        TxtAi.Clear();
        TxtAi.AppendText("Synthèse IA détaillée" + Environment.NewLine);
        TxtAi.AppendText("====================" + Environment.NewLine + Environment.NewLine);
        TxtAi.AppendText(_healthScores.RenderText(health));
        TxtAi.AppendText(Environment.NewLine);
        TxtAi.AppendText(trendText + Environment.NewLine);
        TxtAi.AppendText(Environment.NewLine);
        TxtAi.AppendText("Conseils" + Environment.NewLine);
        TxtAi.AppendText("--------" + Environment.NewLine);
        TxtAi.AppendText(_aiAdvisor.RenderText(tips));
        TxtAi.ScrollToHome();
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
        BtnAudit.Background = BrushFromHex(optimize ? "#1E3A8A" : "#38BDF8");
        BtnOptimize.Background = BrushFromHex(optimize ? "#22C55E" : "#1E3A8A");
        BtnCancel.Background = BrushFromHex("#F59E0B");
        TxtActionHint.Text = optimize ? "Optimisation adaptative en cours." : "Audit adaptatif en cours.";
    }

    private void ResetButtonVisualState()
    {
        BtnAudit.IsEnabled = true;
        BtnOptimize.IsEnabled = true;
        BtnCheckUpdate.IsEnabled = true;
        BtnAudit.Background = BrushFromHex("#2563EB");
        BtnOptimize.Background = BrushFromHex("#2563EB");
        BtnCancel.Background = BrushFromHex("#2563EB");
    }

    private void UpdateScoreHero(int score)
    {
        TxtScoreHero.Text = score.ToString();
        TxtScoreHero.Foreground = score >= 85 ? BrushFromHex("#22C55E") : score >= 65 ? BrushFromHex("#F59E0B") : BrushFromHex("#EF4444");
    }

    
    private void ApplyDynamicAppVersion()
    {
        string displayName = "EPF Optimizer Pro Premium IA v" + ApplicationVersionProvider.GetDisplayVersion();
        Title = displayName;
        TxtAppVersionTitle.Text = displayName;
    }

private void UpdateAdminVisualStatus()
    {
        bool isAdmin = IsRunningAsAdministrator();

        TxtAdminStatus.Text = isAdmin ? "Admin : oui" : "Admin : non";

        if (isAdmin)
        {
            _adminBlinkTimer.Stop();
            TxtAdminStatus.Visibility = Visibility.Visible;
            TxtAdminStatus.Foreground = BrushFromHex("#22C55E");
            Append("[INFO] Application lancée avec privilèges administrateur.");
            Append("[INFO] Mode update : GitHub public sans token personnel.");
        }
        else
        {
            TxtAdminStatus.Visibility = Visibility.Visible;
            TxtAdminStatus.Foreground = BrushFromHex("#EF4444");
            _adminBlinkState = true;
            _adminBlinkTimer.Start();
            Append("[WARN] Application lancée sans privilèges administrateur. Certaines optimisations système peuvent être limitées.");
            TxtActionHint.Text = "Mode non administrateur : certaines optimisations système peuvent être limitées.";
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
        TxtAdminStatus.Foreground = _adminBlinkState ? BrushFromHex("#EF4444") : BrushFromHex("#7F1D1D");
    }
    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
    private static SolidColorBrush BrushFromHex(string hex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        BtnCancel.Background = BrushFromHex("#EF4444");
        TxtActionHint.Text = "Annulation demandée.";
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
        Append("[INFO] Centre IA ouvert.");
    }

private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_cts is not null) return;

        BtnCheckUpdate.IsEnabled = false;
        TxtUpdateStatus.Text = "Mise à jour : vérification GitHub...";
        Append("[INFO] Vérification GitHub des mises à jour...");

        try
        {
            _lastUpdateCheck = await _updateService.CheckLatestAsync(CancellationToken.None);
            TxtUpdateStatus.Text = _lastUpdateCheck.UpdateAvailable
                ? $"Mise à jour disponible : {_lastUpdateCheck.LatestVersion}"
                : $"Mise à jour : OK ({_lastUpdateCheck.CurrentVersion})";
            BtnOpenUpdateRelease.IsEnabled = !string.IsNullOrWhiteSpace(_lastUpdateCheck.ReleaseUrl);
            BtnDownloadUpdate.IsEnabled = _lastUpdateCheck.UpdateAvailable && _lastUpdateCheck.Asset is not null;

            if (_lastUpdateCheck.UpdateAvailable)
            {
                Append($"[OK] Mise à jour disponible : {_lastUpdateCheck.LatestVersion}");
            }
            else
            {
                Append($"[OK] Aucune mise à jour disponible. Version locale : {_lastUpdateCheck.CurrentVersion}, GitHub : {_lastUpdateCheck.LatestVersion}");
            }
        }
        catch (Exception ex)
        {
            TxtUpdateStatus.Text = "Mise à jour : erreur GitHub";
            Append("[ERROR] Erreur GitHub update : " + ex.Message);
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
        if (string.IsNullOrWhiteSpace(msiPath) || !File.Exists(msiPath))
        {
            throw new FileNotFoundException("MSI update introuvable.", msiPath);
        }

        string exePath = Process.GetCurrentProcess().MainModule?.FileName
            ?? Path.Combine(AppContext.BaseDirectory, "EPFOptimizerPro.exe");

        string scriptPath = Path.Combine(
            Path.GetTempPath(),
            "EPFOptimizerPro-Update-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".ps1");

        string logPath = Path.Combine(Path.GetTempPath(), "EPFOptimizerPro-update-install.log");

        string script = string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Stop'",
            "$targetPid = " + Environment.ProcessId.ToString(),
            "$msi = " + ToPowerShellSingleQuoted(msiPath),
            "$exe = " + ToPowerShellSingleQuoted(exePath),
            "$log = " + ToPowerShellSingleQuoted(logPath),
            "$sig = Get-AuthenticodeSignature -FilePath $msi",
            "if ($sig.Status -ne 'Valid') { throw 'MSI signature invalid: ' + $sig.Status }",
            "Write-Host ('MSI signature valid: ' + $msi)",
            "$limit = (Get-Date).AddSeconds(60)",
            "while ((Get-Process -Id $targetPid -ErrorAction SilentlyContinue) -and ((Get-Date) -lt $limit)) { Start-Sleep -Milliseconds 500 }",
            "Write-Host ('MSI update path: ' + $msi)",
            "Write-Host ('MSI log path: ' + $log)",
            "$p = Start-Process -FilePath 'msiexec.exe' -ArgumentList @('/i', $msi, '/qn', '/norestart', '/L*v', $log) -Wait -PassThru",
            "Write-Host ('msiexec exit code: ' + $p.ExitCode)",
            "if ($p.ExitCode -notin @(0,3010)) { throw 'MSI install failed with exit code: ' + $p.ExitCode + ' - log: ' + $log }",
            "if (Test-Path $exe) { Write-Host ('Relaunching app: ' + $exe); Start-Process -FilePath $exe } else { Write-Host ('App executable not found after install: ' + $exe) }"
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
        TxtStep.Text = "Téléchargement update";
        TxtUpdateStatus.Text = "Téléchargement du MSI GitHub...";
        Append("[INFO] Téléchargement update : " + _lastUpdateCheck.Asset.Name);

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
            Append("[OK] MSI valide : " + msiPath);
            TxtUpdateStatus.Text = "Update téléchargée : " + Path.GetFileName(msiPath);
            TxtStep.Text = "Update téléchargée";
            ProgressGlobal.Value = 100;
            TxtPercent.Text = "100 %";
            Append("[OK] MSI téléchargé : " + msiPath);
            Append("[INFO] Installation automatique de l'update...");
            Append("[INFO] Verification signature MSI avant installation...");
            Append("[INFO] Log installation update : " + Path.Combine(Path.GetTempPath(), "EPFOptimizerPro-update-install.log"));
            TxtUpdateStatus.Text = UpdateStatusFormatter.Format("installation...");
            TxtStep.Text = "Installation update";
            StartUpdateInstallerAndExit(msiPath);
            return;
        }
        catch (OperationCanceledException)
        {
            TxtUpdateStatus.Text = "Téléchargement update annulé";
            Append("[WARN] Téléchargement update annulé.");
        }
        catch (Exception ex)
        {
            TxtUpdateStatus.Text = "Erreur téléchargement update";
            Append("[ERROR] Erreur téléchargement update : " + ex.Message);
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
        int score = ExtractDashboardScoreFromAiText(text);
        SetDashboardScore(score);
    }

    private static int ExtractDashboardScoreFromAiText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        var averageMatch = System.Text.RegularExpressions.Regex.Match(
            text,
            @"moyenne\s+(\d{1,3})\s*/\s*100",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if (averageMatch.Success)
        {
            int averageScore;
            if (int.TryParse(averageMatch.Groups[1].Value, out averageScore))
            {
                return ClampDashboardScore(averageScore);
            }
        }

        var indicators = System.Text.RegularExpressions.Regex.Matches(
            text,
            @"(?:Perf|S[ée]cu|Stockage|Update|Stabilit[ée])\s*:??\s*(\d{1,3})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if (indicators.Count > 0)
        {
            int total = 0;
            int count = 0;

            foreach (System.Text.RegularExpressions.Match indicator in indicators)
            {
                int value;
                if (int.TryParse(indicator.Groups[1].Value, out value))
                {
                    total += ClampDashboardScore(value);
                    count++;
                }
            }

            if (count > 0)
            {
                return ClampDashboardScore((int)Math.Round(total / (double)count, MidpointRounding.AwayFromZero));
            }
        }

        var healthMatch = System.Text.RegularExpressions.Regex.Match(
            text,
            @"Sant[ée]\s+IA\s*:\s*(\d{1,3})\s*/\s*100",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if (healthMatch.Success)
        {
            int healthScore;
            if (int.TryParse(healthMatch.Groups[1].Value, out healthScore))
            {
                return ClampDashboardScore(healthScore);
            }
        }

        return 0;
    }

    private static int ClampDashboardScore(int score)
    {
        if (score < 0) return 0;
        if (score > 100) return 100;
        return score;
    }

    private void SetDashboardScore(int score)
    {
        score = ClampDashboardScore(score);
        TxtScoreHero.Text = score.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (score <= 0)
        {
            TxtScoreHero.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139));
        }
        else if (score >= 85)
        {
            TxtScoreHero.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94));
        }
        else if (score >= 70)
        {
            TxtScoreHero.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
        }
        else
        {
            TxtScoreHero.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
        }
    }
}