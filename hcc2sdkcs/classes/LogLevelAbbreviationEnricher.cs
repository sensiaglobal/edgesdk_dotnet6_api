using Serilog.Core;
using Serilog.Events;

namespace Sensia.HCC2.SDK.Classes
{
    public class LogLevelAbbreviationEnricher : ILogEventEnricher
    {
        private readonly Dictionary<LogEventLevel, string> _levelAbbreviations = new Dictionary<LogEventLevel, string>
        {
            { LogEventLevel.Verbose, "trace" },
            { LogEventLevel.Debug, "debug" },
            { LogEventLevel.Information, "info" },
            { LogEventLevel.Warning, "warning" },
            { LogEventLevel.Error, "error" },
            { LogEventLevel.Fatal, "critical" }
        };

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (_levelAbbreviations.TryGetValue(logEvent.Level, out string abbreviation))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("LevelAbbreviation", abbreviation));
            }
        }
    }
}
