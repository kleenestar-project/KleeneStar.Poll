using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Poll.WebRestApi
{
    /// <summary>
    /// The answers the dialogs of the plugin understand: the values a form starts from, a
    /// success (optionally with the address the form follows), and a refusal - per input, which
    /// the form shows beside the input, or as a whole.
    /// </summary>
    public static class FormAnswer
    {
        /// <summary>
        /// The serializer options: camel case is not used, so the keys stay the input names.
        /// </summary>
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = null
        };

        /// <summary>
        /// Answers a JSON payload.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="status">The status code.</param>
        /// <returns>The response.</returns>
        public static IResponse Json(object payload, int status = 200)
        {
            var content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, _options));

            return status == 400
                ? new ResponseBadRequest { Content = content }.AddHeaderContentType("application/json")
                : new ResponseOK { Content = content }.AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Answers the values a form starts from.
        /// </summary>
        /// <param name="values">The values, keyed by input name.</param>
        /// <returns>The response.</returns>
        public static IResponse Values(IDictionary<string, object> values)
        {
            return Json(new Dictionary<string, object> { ["data"] = values });
        }

        /// <summary>
        /// Answers a success the form follows to an address (<c>FollowUri</c>), or closes on.
        /// </summary>
        /// <param name="uri">The address, or <see langword="null"/>.</param>
        /// <param name="extra">Further members of the answer, or <see langword="null"/>.</param>
        /// <returns>The response.</returns>
        public static IResponse Success(string uri = null, IDictionary<string, object> extra = null)
        {
            var payload = new Dictionary<string, object>(extra ?? new Dictionary<string, object>());

            if (uri is not null)
            {
                payload["uri"] = uri;
            }

            payload["success"] = true;

            return Json(payload);
        }

        /// <summary>
        /// Answers refusals per input, translated.
        /// </summary>
        /// <param name="request">The request, for the language.</param>
        /// <param name="errors">The internationalization keys of the messages, keyed by input name.</param>
        /// <returns>The response.</returns>
        public static IResponse Errors(IRequest request, IDictionary<string, string> errors)
        {
            var translated = new Dictionary<string, string>();

            foreach (var (field, key) in errors)
            {
                translated[field] = I18N.Translate(request, key);
            }

            return Json(new { errors = translated }, 400);
        }

        /// <summary>
        /// Answers refusals per input whose messages are translated already (they carry values
        /// such as the lines that could not be read).
        /// </summary>
        /// <param name="messages">The messages, keyed by input name.</param>
        /// <returns>The response.</returns>
        public static IResponse Messages(IDictionary<string, string> messages)
        {
            return Json(new { errors = new Dictionary<string, string>(messages) }, 400);
        }

        /// <summary>
        /// Answers a refusal as a whole.
        /// </summary>
        /// <param name="message">The message, already translated.</param>
        /// <returns>The response.</returns>
        public static IResponse Refuse(string message)
        {
            return Json(new { message }, 400);
        }
    }
}
