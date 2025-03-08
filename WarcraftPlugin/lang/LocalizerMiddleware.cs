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
        internal static IStringLocalizer Load(IStringLocalizer localizer, string moduleDirectory)
        {
            var chatColors = GetChatColors();

            List<LocalizedString> customHeroLocalizerStrings = LoadCustomHeroLocalizations(moduleDirectory, chatColors);

            // Process the localizer strings
            var localizedStrings = localizer.GetAllStrings()
                .Select(ls => new LocalizedString(ls.Name.ToLower(), ReplaceChatColors(ls.Value.ToLower(), chatColors)))
                .Concat(customHeroLocalizerStrings)
                .ToList();

            return new WarcraftLocalizer(localizedStrings);
        }

        private static List<LocalizedString> LoadCustomHeroLocalizations(string moduleDirectory, Dictionary<string, string> chatColors)
        {
            var searchPattern = $"*.{CultureInfo.CurrentUICulture.TwoLetterISOLanguageName}*.json";
            var fallbackSearchPattern = "*.en*.json";

            var customHeroLocalizations = Directory.EnumerateFiles(Path.Combine(moduleDirectory, "lang"), searchPattern);
            var fallbackLocalizations = Directory.EnumerateFiles(Path.Combine(moduleDirectory, "lang"), fallbackSearchPattern);

            // Use a thread-safe collection for parallel processing
            var concurrentLocalizerStrings = new ConcurrentBag<LocalizedString>();

            var jsonOptions = new JsonSerializerOptions { AllowTrailingCommas = true };

            Parallel.ForEach(customHeroLocalizations.Concat(fallbackLocalizations), file =>
            {
                var jsonContent = File.ReadAllText(file);
                var customHeroLocalizations = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent, jsonOptions);

                if (customHeroLocalizations != null)
                {
                    foreach (var localization in customHeroLocalizations)
                    {
                        concurrentLocalizerStrings.Add(new LocalizedString(localization.Key, ReplaceChatColors(localization.Value.ToLower(), chatColors), false, searchedLocation: file));
                    }
                }
            });

            // Use English as fallback
            var uniqueLocalizerStrings = concurrentLocalizerStrings
                .GroupBy(ls => ls.Name)
                .Select(g => g.FirstOrDefault(ls => !ls.SearchedLocation.Contains(".en.")) ?? g.First())
                .ToList();

            return uniqueLocalizerStrings;
        }

        private static Dictionary<string, string> GetChatColors()
        {
            return typeof(ChatColors).GetProperties()
                .ToDictionary(prop => prop.Name.ToLower(), prop => prop.GetValue(null)?.ToString() ?? string.Empty, StringComparer.InvariantCultureIgnoreCase);
        }

        private static readonly Regex ChatColorRegex = new Regex(@"{(\D+?)}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static string ReplaceChatColors(string input, Dictionary<string, string> chatColors)
        {
            return ChatColorRegex.Replace(input, match =>
            {
                var key = match.Groups[1].Value.ToLower();
                return chatColors.TryGetValue(key, out var value) ? value : "{UNKNOWN-COLOR}";
            });
        }
    }

    public class WarcraftLocalizer(List<LocalizedString> localizedStrings) : IStringLocalizer
    {
        private readonly List<LocalizedString> _localizedStrings = localizedStrings;

        public LocalizedString this[string name] => _localizedStrings.FirstOrDefault(ls => ls.Name == name.ToLower()) ?? new LocalizedString(name.ToLower(), name.ToLower());

        public LocalizedString this[string name, params object[] arguments] =>
            new(name.ToLower(), string.Format(_localizedStrings.FirstOrDefault(ls => ls.Name == name.ToLower())?.Value ?? name.ToLower(), arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => _localizedStrings;

        public IStringLocalizer WithCulture(CultureInfo culture) => this;
    }
}
