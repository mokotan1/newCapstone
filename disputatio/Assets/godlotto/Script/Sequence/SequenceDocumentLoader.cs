using System;

namespace Godlotto.Sequence
{
    public static class SequenceDocumentLoader
    {
        public static SequenceDocument Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new SequencePlayException("invalid_document", "Sequence JSON is empty.");
            }

            SequenceDocument document;
            try
            {
#if UNITY_5_3_OR_NEWER
                document = UnityEngine.JsonUtility.FromJson<SequenceDocument>(json);
#else
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    IncludeFields = true,
                };
                document = System.Text.Json.JsonSerializer.Deserialize<SequenceDocument>(json, options);
#endif
            }
            catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException)
            {
                throw new SequencePlayException(
                    "invalid_document",
                    "Sequence JSON parse failed: " + ex.Message);
            }

            if (document == null)
            {
                throw new SequencePlayException("invalid_document", "Sequence JSON parse returned null.");
            }

            return document;
        }
    }
}
