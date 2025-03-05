using Microsoft.Extensions.Localization;
using System.Linq;
using System.Collections.Generic;
using System;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.RegularExpressions;
using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace WarcraftPlugin.lang
{
    public static class LocalizerMiddleware
    {
        internal static IStringLocalizer Enable(IStringLocalizer localizer, string moduleDirectory)
        {
            var chatColors = GetChatColors();

            var searchPattern = $"*.{CultureInfo.CurrentUICulture.TwoLetterISOLanguageName}*.json";

            var customHeroLocalizations = Directory.EnumerateFiles(Path.Combine(moduleDirectory, "lang"), searchPattern);

            // Use a thread-safe collection for parallel processing
            var concurrentLocalizerStrings = new ConcurrentBag<LocalizedString>();

            Parallel.ForEach(customHeroLocalizations, file =>
            {
                var jsonContent = File.ReadAllText(file);
                var customHeroLocalizations = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

                if (customHeroLocalizations != null)
                {
                    foreach (var localization in customHeroLocalizations)
                    {
                        concurrentLocalizerStrings.Add(new LocalizedString(localization.Key, ReplaceChatColors(localization.Value, chatColors)));
                    }
                }
            });

            // Add the parallel-processed strings to the main list
            var localizerStrings = new List<LocalizedString>();
            localizerStrings.AddRange(concurrentLocalizerStrings);

            // Process the localizer strings
            var localizedStrings = localizer.GetAllStrings()
                .Select(ls => new LocalizedString(ls.Name, ReplaceChatColors(ls.Value, chatColors)))
                .Concat(localizerStrings)
                .ToList();

            return new ColorLocalizer(localizerStrings);
        }

        private static Dictionary<string, string> GetChatColors()
        {
            return typeof(ChatColors).GetProperties()
                .ToDictionary(prop => prop.Name, prop => prop.GetValue(null)?.ToString() ?? string.Empty, StringComparer.InvariantCultureIgnoreCase);
        }

        private static readonly Regex ChatColorRegex = new Regex(@"{(\D+?)}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static string ReplaceChatColors(string input, Dictionary<string, string> chatColors)
        {
            return ChatColorRegex.Replace(input, match =>
            {
                var key = match.Groups[1].Value;
                return chatColors.TryGetValue(key, out var value) ? value : "{UNKNOWN-COLOR}";
            });
        }
    }

    public class ColorLocalizer(List<LocalizedString> localizedStrings) : IStringLocalizer
    {
        private readonly List<LocalizedString> _localizedStrings = localizedStrings;

        public LocalizedString this[string name] => _localizedStrings.FirstOrDefault(ls => ls.Name == name) ?? new LocalizedString(name, name);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(_localizedStrings.FirstOrDefault(ls => ls.Name == name)?.Value ?? name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => _localizedStrings;

        public IStringLocalizer WithCulture(CultureInfo culture) => this;
    }
}
