namespace ServiceLib.Handler;

/// <summary>
/// Built-in Free group fed from the public site feed. Users cannot delete or retarget it.
/// </summary>
public static class FreeSubscription
{
    public const string Remarks = "رایگان";

    public static bool IsFree(string? id) =>
        string.Equals(id, Global.FreeSubscriptionId, StringComparison.Ordinal);

    /// <summary>
    /// A profile is protected when it belongs to the built-in Free subscription or is present
    /// in that group's list. The membership check also covers profiles whose legacy Subid
    /// metadata is missing or was changed outside the normal import path.
    /// </summary>
    public static bool BlocksExport(string? subscriptionId, bool listedInFreeGroup = false) =>
        IsFree(subscriptionId) || listedInFreeGroup;

    public static bool IsProtectedProfile(ProfileItem item) =>
        item is not null && BlocksExport(item.Subid);

    public static void Pin(SubItem item)
    {
        item.Id = Global.FreeSubscriptionId;
        item.Remarks = Remarks;
        item.Url = Global.FreeSubscriptionUrl;
        item.Enabled = true;
        item.AutoUpdateInterval = Math.Max(item.AutoUpdateInterval, 15);
        item.Sort = Math.Min(item.Sort <= 0 ? 1 : item.Sort, 1);
    }

    /// <summary>
    /// True only for a deliberate empty list: blank body, or base64 that decodes to blank.
    /// Null means the download failed. An error page or a failed download must not wipe saved free configs.
    /// </summary>
    public static bool IsExplicitlyEmptyFeed(string? body)
    {
        if (body is null)
        {
            return false;
        }

        if (body.IsNullOrEmpty())
        {
            return true;
        }

        var compact = Regex.Replace(body.Trim(), @"\s+", string.Empty);
        if (compact.Length == 0)
        {
            return true;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(compact));
            return decoded.IsNullOrEmpty();
        }
        catch
        {
            return false;
        }
    }

    private static void StartupDiagnostic(string message)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "v2rayN-MB", "startup-diagnostic.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] FREE SUBSCRIPTION: {message}{Environment.NewLine}");
        }
        catch { }
    }

    public static async Task EnsureAsync()
    {
        StartupDiagnostic("ENSURE BEGIN");
        StartupDiagnostic("GET FREE SUBITEM BEGIN");
        var existing = await AppManager.Instance.GetSubItem(Global.FreeSubscriptionId);
        StartupDiagnostic("GET FREE SUBITEM COMPLETE: " + (existing is null ? "NULL" : "FOUND"));
        if (existing is null)
        {
            var item = new SubItem
            {
                Id = Global.FreeSubscriptionId,
                Sort = 1
            };
            Pin(item);

            // Push other groups down so Free stays first.
            StartupDiagnostic("GET OTHER SUBITEMS BEGIN");
            var others = await AppManager.Instance.SubItems() ?? [];
            StartupDiagnostic("GET OTHER SUBITEMS COMPLETE: " + others.Count);
            foreach (var other in others.Where(t => !IsFree(t.Id)))
            {
                if (other.Sort < 2)
                {
                    other.Sort += 1;
                    StartupDiagnostic("REPLACE OTHER SUBITEM BEGIN: " + other.Id);
                    await SQLiteHelper.Instance.ReplaceAsync(other);
                    StartupDiagnostic("REPLACE OTHER SUBITEM COMPLETE: " + other.Id);
                }
            }

            StartupDiagnostic("REPLACE NEW FREE SUBITEM BEGIN");
            await SQLiteHelper.Instance.ReplaceAsync(item);
            StartupDiagnostic("REPLACE NEW FREE SUBITEM COMPLETE");
            StartupDiagnostic("ENSURE COMPLETE (NEW ITEM)");
            return;
        }

        StartupDiagnostic("PIN EXISTING FREE SUBITEM BEGIN");
        var beforeRemarks = existing.Remarks;
        var beforeUrl = existing.Url;
        var beforeEnabled = existing.Enabled;
        var beforeInterval = existing.AutoUpdateInterval;
        Pin(existing);
        if (beforeRemarks != existing.Remarks
            || beforeUrl != existing.Url
            || beforeEnabled != existing.Enabled
            || beforeInterval != existing.AutoUpdateInterval
            || existing.Sort != 1)
        {
            existing.Sort = 1;
            StartupDiagnostic("REPLACE EXISTING FREE SUBITEM BEGIN");
            await SQLiteHelper.Instance.ReplaceAsync(existing);
            StartupDiagnostic("REPLACE EXISTING FREE SUBITEM COMPLETE");
        }
        StartupDiagnostic("ENSURE COMPLETE (EXISTING ITEM)");
    }
}
