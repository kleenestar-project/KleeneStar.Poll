using KleeneStar.Core.WebRestApi;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Poll.WebRestApi
{
    /// <summary>
    /// The submitted values of a dialog, read without regard to how the form sent them: a JSON
    /// object (the data forms) or form fields, a check box as <c>true</c>, <c>"true"</c> or
    /// <c>"on"</c>, a multiple selection as an array or a list separated by semicolons or commas.
    /// </summary>
    public sealed class FormPayload
    {
        private readonly Dictionary<string, string> _values;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="values">The values, keyed by input name.</param>
        public FormPayload(IDictionary<string, string> values)
        {
            _values = new Dictionary<string, string>(values ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads the payload of a request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The payload; empty when the request carries none.</returns>
        public static FormPayload Read(IRequest request)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var json = JsonApi.Read<Dictionary<string, JsonElement>>(request);

            foreach (var (key, element) in json ?? [])
            {
                try
                {
                    values[key] = Text(element);
                }
                catch (InvalidOperationException)
                {
                    // a value that is no valid UTF-8 is not read; the dialog then reports the
                    // input as missing rather than the request failing
                }
            }

            return new FormPayload(values);
        }

        /// <summary>
        /// Determines whether the payload carries an input.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns><see langword="true"/> when it does.</returns>
        public bool Has(string name)
        {
            return _values.ContainsKey(name);
        }

        /// <summary>
        /// Returns a submitted text, trimmed; <see langword="null"/> for a blank or missing one.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The text.</returns>
        public string Text(string name)
        {
            return _values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
        }

        /// <summary>
        /// Returns a submitted text as it was sent; <see langword="null"/> for a missing one.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The text.</returns>
        public string Raw(string name)
        {
            return _values.TryGetValue(name, out var value) ? value : null;
        }

        /// <summary>
        /// Returns a submitted check box.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <param name="fallback">The value of a missing input.</param>
        /// <returns>The value.</returns>
        public bool Flag(string name, bool fallback = false)
        {
            if (!_values.TryGetValue(name, out var value) || value is null)
            {
                return fallback;
            }

            return value.Trim().ToLowerInvariant() is "true" or "on" or "1" or "yes" or "checked";
        }

        /// <summary>
        /// Returns a submitted whole number.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The number, or <see langword="null"/>.</returns>
        public int? Number(string name)
        {
            return int.TryParse(Text(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;
        }

        /// <summary>
        /// Returns a submitted id.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The id, or <see langword="null"/>.</returns>
        public Guid? Id(string name)
        {
            return Ids(name).Cast<Guid?>().FirstOrDefault();
        }

        /// <summary>
        /// Returns the ids of a multiple selection.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The distinct ids, in the given order.</returns>
        public IReadOnlyList<Guid> Ids(string name)
        {
            return [.. List(name)
                .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
                .Where(x => x != Guid.Empty)
                .Distinct()];
        }

        /// <summary>
        /// Returns the items of a multiple selection or list.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The items.</returns>
        public IReadOnlyList<string> List(string name)
        {
            var text = Raw(name);

            if (string.IsNullOrWhiteSpace(text))
            {
                return [];
            }

            return [.. text
                .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length > 0)];
        }

        /// <summary>
        /// Writes a JSON value as text.
        /// </summary>
        /// <param name="element">The value.</param>
        /// <returns>The text.</returns>
        private static string Text(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.Array => string.Join(";", element.EnumerateArray().Select(Text).Where(x => !string.IsNullOrWhiteSpace(x))),
                JsonValueKind.Object => element.TryGetProperty("id", out var id) ? Text(id)
                    : element.TryGetProperty("value", out var value) ? Text(value)
                    : element.GetRawText(),
                _ => element.GetRawText()
            };
        }
    }
}
