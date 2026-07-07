using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CFilm.Vod;

/// <summary>
/// 定期スキャン。IScheduledTask を実装すると、Jellyfin が自動で見つけて
/// ダッシュボードの「予約タスク」に並べてくれます(登録コード不要)。
/// </summary>
public class VodScanScheduledTask : IScheduledTask
{
    private readonly VodProviderManager _manager;
    private readonly ILogger<VodScanScheduledTask> _logger;

    /// <summary>コンストラクタ。DI で共有の VodProviderManager を受け取る。</summary>
    public VodScanScheduledTask(VodProviderManager manager, ILogger<VodScanScheduledTask> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    /// <summary>ダッシュボードに出る名前。</summary>
    public string Name => "CFilm: VOD配信元スキャン";

    /// <summary>タスクを一意に識別するキー。</summary>
    public string Key => "CFilmVodScan";

    /// <summary>説明文。</summary>
    public string Description => "各作品の配信サービス(TMDBのflatrate)を取得し、一括対応表のキャッシュを更新します。";

    /// <summary>ダッシュボードでの分類名。</summary>
    public string Category => "CFilm";

    /// <summary>実際に走る処理。</summary>
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("CFilm: scheduled VOD scan started.");
        await _manager.RebuildAsync(cancellationToken).ConfigureAwait(false);
        progress.Report(100);
    }

    /// <summary>既定の起動タイミング = 毎日 午前4時。</summary>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(4).Ticks
            }
        };
    }
}
