using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Threading.Tasks;

namespace chatserver.utils
{
    public static class Utils
    {
        public static List<string> GetUrlRoutes(Uri url)
        {
            List<string>? segments = url.Segments.Skip(1).Select(s => s.Trim('/')).ToList();
            return segments;
        }

        public static Dictionary<string, string> ExtractQueryParameters(Uri url)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            var queryParameters = new Dictionary<string, string>();

            // Verifica si hi ha paràmetres de consulta
            if (string.IsNullOrEmpty(url.Query))
            {
                return queryParameters; // Retorna un diccionari buit
            }

            // Elimina el signe '?' del principi de la cadena de consulta
            string query = url.Query.TrimStart('?');

            // Separa els paràmetres en parelles clau-valor
            string[] pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                string[] keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    string key = Uri.UnescapeDataString(keyValue[0]);
                    string value = Uri.UnescapeDataString(keyValue[1]);

                    queryParameters[key] = value;
                }
            }

            return queryParameters;
        }

        public static bool IsGroup(string groupId)
        {
            return groupId.StartsWith("#gId");
        }

        public static JsonElement ModifyFieldUsingJsonNode(JsonElement original, string fieldName, string newValue)
        {
            // Convertir JsonElement a JsonObject
            var jsonObject = JsonNode.Parse(original.GetRawText()).AsObject();

            // Modificar el camp
            if (jsonObject.ContainsKey(fieldName))
            {
                jsonObject[fieldName] = newValue;
            }

            // Retornar el resultat com a JsonElement
            return JsonDocument.Parse(jsonObject.ToJsonString()).RootElement;
        }
    }
}
