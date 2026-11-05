using System.Globalization;
using System.Text.Json;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.AviationWeather
{
    /// <summary>
    /// Converts the JSON answer of the aviationweather.gov METAR endpoint to weather reports.
    /// </summary>
    public static class AviationWeatherParser
    {
        /// <summary>
        /// Number of meters in a statute mile.
        /// </summary>
        private const double MetersPerMile = 1609.344;

        /// <summary>
        /// Visibility values are rounded to this many meters, because the source gives them converted from miles.
        /// </summary>
        private const double VisibilityStepMeters = 100.0;

        /// <summary>
        /// Cover codes of a layer that always has a base. The source leaves the base out when it is zero (for example BKN000), so a layer of these covers without a base is on the ground. Codes such as NSC or CLR have no base at all.
        /// </summary>
        private static readonly string[] LayerCovers = new string[] { "FEW", "SCT", "BKN", "OVC" };

        /// <summary>
        /// Parses the answer.
        /// </summary>
        /// <param name="json">Body of the answer; a JSON array of reports.</param>
        /// <returns>The reports; those without a code, a time or a text are skipped, and for a repeated airport only the newest report is kept.</returns>
        /// <exception cref="JsonException">Invalid JSON.</exception>
        public static IReadOnlyList<Metar> Parse(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new List<Metar>();
            }

            Dictionary<string, Metar> newest = new Dictionary<string, Metar>(StringComparer.OrdinalIgnoreCase);

            foreach (JsonElement item in document.RootElement.EnumerateArray())
            {
                Metar? metar = ParseItem(item);

                if (metar != null && (!newest.TryGetValue(metar.IcaoCode, out Metar? existing) || metar.ObservedAt > existing.ObservedAt))
                {
                    newest[metar.IcaoCode] = metar;
                }
            }

            return newest.Values.ToList();
        }

        /// <summary>
        /// Parses the answer of the TAF endpoint.
        /// </summary>
        /// <param name="json">Body of the answer; a JSON array of forecasts.</param>
        /// <returns>The forecasts; those without a code, a validity or a text are skipped, and for a repeated airport only the one issued last is kept.</returns>
        /// <exception cref="JsonException">Invalid JSON.</exception>
        public static IReadOnlyList<Taf> ParseTafs(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new List<Taf>();
            }

            Dictionary<string, Taf> newest = new Dictionary<string, Taf>(StringComparer.OrdinalIgnoreCase);

            foreach (JsonElement item in document.RootElement.EnumerateArray())
            {
                Taf? taf = ParseTaf(item);

                if (taf != null && (!newest.TryGetValue(taf.IcaoCode, out Taf? existing) || taf.IssuedAt > existing.IssuedAt))
                {
                    newest[taf.IcaoCode] = taf;
                }
            }

            return newest.Values.ToList();
        }

        /// <summary>
        /// Converts one forecast.
        /// </summary>
        /// <param name="item">JSON object of the forecast.</param>
        /// <returns>The forecast, or null when it has no code, validity or text.</returns>
        private static Taf? ParseTaf(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? code = GetText(item, "icaoId");
            double? validFrom = GetNumber(item, "validTimeFrom");
            double? validTo = GetNumber(item, "validTimeTo");
            string? raw = GetText(item, "rawTAF");

            if (string.IsNullOrWhiteSpace(code) || !validFrom.HasValue || !validTo.HasValue || string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            DateTimeOffset from = DateTimeOffset.FromUnixTimeSeconds((long)validFrom.Value);
            DateTimeOffset issued = DateTimeOffset.TryParse(GetText(item, "issueTime"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsedIssue) ? parsedIssue : from;
            List<TafPeriod> periods = new List<TafPeriod>();

            if (item.TryGetProperty("fcsts", out JsonElement forecasts) && forecasts.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement forecast in forecasts.EnumerateArray())
                {
                    TafPeriod? period = ParseTafPeriod(forecast);

                    if (period != null)
                    {
                        periods.Add(period);
                    }
                }
            }

            return new Taf(code.Trim().ToUpperInvariant(), issued, from, DateTimeOffset.FromUnixTimeSeconds((long)validTo.Value), raw.Trim(), periods.OrderBy((TafPeriod period) => period.From).ToList());
        }

        /// <summary>
        /// Converts one period of a forecast.
        /// </summary>
        /// <param name="item">JSON object of the period.</param>
        /// <returns>The period, or null when it has no times.</returns>
        private static TafPeriod? ParseTafPeriod(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            double? from = GetNumber(item, "timeFrom");
            double? to = GetNumber(item, "timeTo");

            if (!from.HasValue || !to.HasValue)
            {
                return null;
            }

            bool isVariable = item.TryGetProperty("wdir", out JsonElement direction) && direction.ValueKind == JsonValueKind.String && string.Equals(direction.GetString(), "VRB", StringComparison.OrdinalIgnoreCase);
            double? visibility = null;
            bool lowerBound = false;

            if (item.TryGetProperty("visib", out JsonElement visibilityElement))
            {
                visibility = ParseVisibility(visibilityElement, out lowerBound);
            }

            return new TafPeriod(
                DateTimeOffset.FromUnixTimeSeconds((long)from.Value),
                DateTimeOffset.FromUnixTimeSeconds((long)to.Value),
                NullIfEmpty(GetText(item, "fcstChange"))?.ToUpperInvariant(),
                ToInt(GetNumber(item, "probability")),
                isVariable ? null : ToInt(GetNumber(item, "wdir")),
                isVariable,
                ToInt(GetNumber(item, "wspd")),
                ToInt(GetNumber(item, "wgst")),
                visibility,
                lowerBound,
                ParseClouds(item),
                NullIfEmpty(GetText(item, "wxString")));
        }

        /// <summary>
        /// Converts one report.
        /// </summary>
        /// <param name="item">JSON object of the report.</param>
        /// <returns>The report, or null when it has no code, time or text.</returns>
        private static Metar? ParseItem(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? code = GetText(item, "icaoId");
            double? observed = GetNumber(item, "obsTime");
            string? raw = GetText(item, "rawOb");

            if (string.IsNullOrWhiteSpace(code) || !observed.HasValue || string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            JsonElement direction;
            bool isVariable = item.TryGetProperty("wdir", out direction) && direction.ValueKind == JsonValueKind.String && string.Equals(direction.GetString(), "VRB", StringComparison.OrdinalIgnoreCase);
            double? visibility = null;
            bool lowerBound = false;

            if (item.TryGetProperty("visib", out JsonElement visibilityElement))
            {
                visibility = ParseVisibility(visibilityElement, out lowerBound);
            }

            return new Metar(
                code.Trim().ToUpperInvariant(),
                DateTimeOffset.FromUnixTimeSeconds((long)observed.Value),
                raw.Trim(),
                NullIfEmpty(GetText(item, "fltCat"))?.ToUpperInvariant(),
                isVariable ? null : ToInt(GetNumber(item, "wdir")),
                isVariable,
                ToInt(GetNumber(item, "wspd")),
                ToInt(GetNumber(item, "wgst")),
                visibility,
                lowerBound,
                ParseClouds(item),
                GetNumber(item, "temp"),
                GetNumber(item, "dewp"),
                GetNumber(item, "altim"),
                NullIfEmpty(GetText(item, "wxString")),
                GetNumber(item, "lat"),
                GetNumber(item, "lon"));
        }

        /// <summary>
        /// Reads the cloud layers.
        /// </summary>
        /// <param name="item">JSON object of the report.</param>
        /// <returns>The layers from the lowest one; empty when the sky is clear or the property is missing.</returns>
        private static List<CloudLayer> ParseClouds(JsonElement item)
        {
            List<CloudLayer> layers = new List<CloudLayer>();

            if (!item.TryGetProperty("clouds", out JsonElement clouds) || clouds.ValueKind != JsonValueKind.Array)
            {
                return layers;
            }

            foreach (JsonElement cloud in clouds.EnumerateArray())
            {
                string? cover = cloud.ValueKind == JsonValueKind.Object ? NullIfEmpty(GetText(cloud, "cover")) : null;

                if (cover != null)
                {
                    string code = cover.ToUpperInvariant();
                    int? baseFeet = ToInt(GetNumber(cloud, "base"));

                    if (!baseFeet.HasValue && Array.IndexOf(LayerCovers, code) >= 0)
                    {
                        baseFeet = 0;
                    }

                    layers.Add(new CloudLayer(code, baseFeet));
                }
            }

            return layers.OrderBy((CloudLayer layer) => layer.BaseFeet ?? int.MaxValue).ToList();
        }

        /// <summary>
        /// Converts the visibility, which the source gives in statute miles either as a number or as a text such as "6+".
        /// </summary>
        /// <param name="element">The property value.</param>
        /// <param name="lowerBound">Receives whether the value is only a lower bound.</param>
        /// <returns>Visibility in meters, or null when it is missing or not understood.</returns>
        private static double? ParseVisibility(JsonElement element, out bool lowerBound)
        {
            lowerBound = false;
            double miles;

            if (element.ValueKind == JsonValueKind.Number)
            {
                miles = element.GetDouble();
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                string text = (element.GetString() ?? string.Empty).Trim();
                lowerBound = text.EndsWith('+');

                if (!double.TryParse(text.TrimEnd('+'), NumberStyles.Float, CultureInfo.InvariantCulture, out miles))
                {
                    lowerBound = false;

                    return null;
                }
            }
            else
            {
                return null;
            }

            return Math.Round(miles * MetersPerMile / VisibilityStepMeters) * VisibilityStepMeters;
        }

        /// <summary>
        /// Reads a property as a number; numbers given as text are accepted too.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The value, or null when the property is missing or is not a number.</returns>
        private static double? GetNumber(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.GetDouble();
            }

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                return parsed;
            }

            return null;
        }

        /// <summary>
        /// Reads a property as text.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The value, or null when the property is missing or is not a string.</returns>
        private static string? GetText(JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }

        /// <summary>
        /// Rounds a number to an integer.
        /// </summary>
        /// <param name="value">The number or null.</param>
        /// <returns>The rounded value or null.</returns>
        private static int? ToInt(double? value)
        {
            return value.HasValue ? (int)Math.Round(value.Value) : null;
        }

        /// <summary>
        /// Converts an empty text to null.
        /// </summary>
        /// <param name="text">The text or null.</param>
        /// <returns>The trimmed text or null when it is empty.</returns>
        private static string? NullIfEmpty(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }
}
