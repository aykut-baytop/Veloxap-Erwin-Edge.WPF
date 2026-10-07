using System;
using System.Collections.Generic;
using System.Globalization;

namespace Veloxap.AddIn.Erwin.Models
{
    public class CustomUdpResponse : VeloxapServiceBaseResponse
    {
        public CustomUdpResponse()
        {
            Data = new List<CustomUdpDto>();
        }

        public new List<CustomUdpDto> Data
        {
            get { return (List<CustomUdpDto>)base.Data; }
            set { base.Data = value; }
        }
    }

    public sealed class CustomUdpDto
    {
        public long Id { get; set; }

        public int ModelVersion { get; set; }

        public long ModelId { get; set; }

        public string ModelPath { get; set; } = "";

        public string UdpName { get; set; } = "";

        public string UdpVal { get; set; }

        public string Type { get; set; } = "";

        public string ParentObject { get; set; } = "";

        public string Object { get; set; } = "";

        public string Environment { get; set; } = "";

        public DateTime CreateDate { get; set; }

        public string CreateUser { get; set; } = "";

        public string UpdateUser { get; set; }

        public DateTime UpdateDate { get; set; }
    }

    /// <summary>
    /// Custom UDP endpointindeki tarih alanlari farkli servis/DB formatlarinda
    /// gelebildiginden, kaydin tamamini deserialize hatasina dusurmeden okur.
    /// Gecersiz veya bos tarihler siralamada en eski tarih olarak ele alinir.
    /// </summary>
    public static class FlexibleDateTimeConverter
    {
        private static readonly string[] SupportedFormats =
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.FFFFFFF",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
            "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
            "dd.MM.yyyy HH:mm:ss",
            "dd.MM.yyyy HH:mm:ss.FFFFFFF",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm:ss.FFFFFFF",
            "MM/dd/yyyy HH:mm:ss",
            "MM/dd/yyyy HH:mm:ss.FFFFFFF",
            "yyyyMMddHHmmss"
        };

        public static DateTime Parse(object input)
        {
            if (input == null)
                return DateTime.MinValue;

            if (input is DateTime)
                return (DateTime)input;

            if (input is int || input is long)
                return FromUnixTimestamp(Convert.ToInt64(input, CultureInfo.InvariantCulture));

            string value = input as string;
            if (string.IsNullOrWhiteSpace(value))
                return DateTime.MinValue;

            value = value.Trim();
            if (TryReadMicrosoftJsonDate(value, out DateTime microsoftJsonDate))
                return microsoftJsonDate;

            if (DateTime.TryParseExact(value, SupportedFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out DateTime exactDate))
                return exactDate;

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out DateTime invariantDate))
                return invariantDate;

            if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("tr-TR"),
                DateTimeStyles.AllowWhiteSpaces, out DateTime localDate))
                return localDate;

            return DateTime.MinValue;
        }

        private static bool TryReadMicrosoftJsonDate(string value, out DateTime date)
        {
            date = DateTime.MinValue;
            if (!value.StartsWith("/Date(", StringComparison.Ordinal) || !value.EndsWith(")/", StringComparison.Ordinal))
                return false;

            string timestampText = value.Substring(6, value.Length - 8);
            int timezoneIndex = timestampText.Length > 1
                ? timestampText.IndexOfAny(new[] { '+', '-' }, 1)
                : -1;
            if (timezoneIndex >= 0)
                timestampText = timestampText.Substring(0, timezoneIndex);

            return long.TryParse(timestampText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long timestamp)
                && (date = FromUnixTimestamp(timestamp, true)) != DateTime.MinValue;
        }

        private static DateTime FromUnixTimestamp(long timestamp, bool milliseconds = false)
        {
            try
            {
                // 10 basamakli degerler saniye, 13 basamakli degerler milisaniye olarak gelir.
                return milliseconds || timestamp < -253402300799L || timestamp > 253402300799L
                    ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime
                    : DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateTime.MinValue;
            }
        }
    }
}
