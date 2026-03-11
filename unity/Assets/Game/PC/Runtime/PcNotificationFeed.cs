#nullable enable

namespace PampaSkylines.PC
{
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class PcNotificationFeed
{
    private readonly List<PcNotificationEntry> _entries = new();
    private readonly int _capacity;

    public PcNotificationFeed(int capacity = 12)
    {
        _capacity = Math.Max(3, capacity);
    }

    public IReadOnlyList<PcNotificationEntry> Entries => _entries;

    public void Push(
        string message,
        PcStatusTone tone = PcStatusTone.Neutral,
        PcNotificationCategory category = PcNotificationCategory.Allerta)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _entries.Insert(0, new PcNotificationEntry
        {
            Message = message.Trim(),
            Tone = tone,
            Category = category,
            TimestampUtc = DateTimeOffset.UtcNow
        });

        if (_entries.Count > _capacity)
        {
            _entries.RemoveRange(_capacity, _entries.Count - _capacity);
        }
    }

    public string FormatRecent(int maxEntries = 4)
    {
        return string.Join(
            "\n",
            _entries
                .Take(Math.Max(1, maxEntries))
                .Select(entry => $"[{entry.TimestampUtc:HH:mm:ss}] [{entry.Category.ToShortTag()}] {entry.Message}"));
    }
}

public sealed class PcNotificationEntry
{
    public string Message { get; set; } = string.Empty;

    public PcStatusTone Tone { get; set; }

    public PcNotificationCategory Category { get; set; }

    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public enum PcNotificationCategory
{
    Eventi = 0,
    Economia = 1,
    Servizi = 2,
    Milestone = 3,
    Allerta = 4
}

public static class PcNotificationCategoryExtensions
{
    public static string ToShortTag(this PcNotificationCategory category)
    {
        return category switch
        {
            PcNotificationCategory.Eventi => "EVT",
            PcNotificationCategory.Economia => "ECO",
            PcNotificationCategory.Servizi => "SRV",
            PcNotificationCategory.Milestone => "MLS",
            _ => "ALT"
        };
    }

    public static string ToDisplayName(this PcNotificationCategory category)
    {
        return category switch
        {
            PcNotificationCategory.Eventi => "Eventi",
            PcNotificationCategory.Economia => "Economia",
            PcNotificationCategory.Servizi => "Servizi",
            PcNotificationCategory.Milestone => "Milestone",
            _ => "Allerta"
        };
    }
}
}
