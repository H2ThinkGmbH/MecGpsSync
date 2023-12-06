using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QServerValidation.DataStreaming.GpsHelper
{
    public class GpGgaMessage
    {
        /// <summary>
        /// The message identifier, which is always "GPGGA" for GGA messages.
        /// </summary>
        public string MessageId { get; private set; }

        /// <summary>
        /// The time of the GPS fix.
        /// </summary>
        public DateTime Time { get; private set; }

        /// <summary>
        /// The latitude value in degrees.
        /// </summary>
        public double Latitude { get; private set; }

        /// <summary>
        /// The longitude value in degrees.
        /// </summary>
        public double Longitude { get; private set; }

        /// <summary>
        /// The fix quality indicator, which represents the type of fix.
        /// </summary>
        public FixQuality FixQuality { get; private set; }

        /// <summary>
        /// The number of satellites in use.
        /// </summary>
        public int NumberOfSatellites { get; private set; }

        /// <summary>
        /// The horizontal dilution of precision, a measure of the GPS accuracy.
        /// </summary>
        public double HorizontalDilutionOfPrecision { get; private set; }

        /// <summary>
        /// The altitude above mean sea level.
        /// </summary>
        public double Altitude { get; private set; }

        /// <summary>
        /// The units of the altitude (e.g., "M" for meters).
        /// </summary>
        public string AltitudeUnits { get; private set; }

        /// <summary>
        /// The geoidal separation, the difference between the ellipsoid and mean sea level.
        /// </summary>
        public double GeoidalSeparation { get; private set; }

        /// <summary>
        /// The units of the geoidal separation (e.g., "M" for meters).
        /// </summary>
        public string GeoidalSeparationUnits { get; private set; }

        /// <summary>
        /// The age of differential GPS data, if applicable.
        /// </summary>
        public double AgeOfDifferentialGpsData { get; private set; }

        /// <summary>
        /// The reference station ID for differential corrections.
        /// </summary>
        public int ReferenceStationId { get; private set; }

        /// <summary>
        /// A Checksum for data validation.
        /// </summary>
        public int Checksum { get; private set; }

        /// <summary>
        /// Creates a new instance of the <see cref="GpGgaMessage"/> class.
        /// </summary>
        /// <param name="message">Provide a message string which will be parsed into the properties.</param>
        public GpGgaMessage(string message)
        {
            // Use regular expressions to parse the NMEA message and extract data
            // Example message from GPS42S6: $GPGGA,120813.00,5225.70090,N,01330.96235,E,1,06,2.87,71.6,M,42.1,M,,*6D\r\n
            var match = Regex.Match(message, @"\$GPGGA,(\d{6})\.\d{2},(\d+\.\d+),([NS]),(\d+\.\d+),([EW]),(\d),(\d{2}),(\d+\.\d+),(\d+\.\d+),([M]),(\d+\.\d+),([M]),(\d+\.\d+)*,(.+)?\*([0-9A-Fa-f]{2})\r\n");

            if (!match.Success || match.Groups.Count != 16)
            {
                throw new ArgumentException("Invalid NMEA GGA message format.");
            }

            // Validate Checksum
            var calculatedChecksum = CalculateChecksum(match.Groups[0].Value);
            Checksum = Convert.ToInt32(match.Groups[15].Value, 16);
            if (calculatedChecksum != Checksum)
            {
                throw new ArgumentException("Corrupt NMEA GGA message, checksum has failed.");
            }

            // Assign the extracted values to the properties
            MessageId = "GPGGA";
            Time = DateTime.ParseExact(match.Groups[1].Value, "HHmmss", null);
            Latitude = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            Latitude *= match.Groups[3].Value.Equals("S") ? -1 : 1; // If Southern Hemisphere, negate the latitude value
            Longitude = double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
            Longitude *= match.Groups[5].Value.Equals("W") ? -1 : 1; // If Western Hemisphere, negate the longitude value
            FixQuality = (FixQuality)Enum.Parse(typeof(FixQuality), match.Groups[6].Value);
            NumberOfSatellites = int.Parse(match.Groups[7].Value);
            HorizontalDilutionOfPrecision = double.Parse(match.Groups[8].Value, CultureInfo.InvariantCulture);
            Altitude = double.Parse(match.Groups[9].Value, CultureInfo.InvariantCulture);
            AltitudeUnits = match.Groups[10].Value;
            GeoidalSeparation = double.Parse(match.Groups[11].Value, CultureInfo.InvariantCulture);
            GeoidalSeparationUnits = match.Groups[12].Value;
            AgeOfDifferentialGpsData = !string.IsNullOrEmpty(match.Groups[13].Value) ? double.Parse(match.Groups[13].Value, CultureInfo.InvariantCulture) : 0.0;
            ReferenceStationId = 0; // Not captured in the regex pattern, so set to 0 for now (you can add extraction if needed)
        }

        private int CalculateChecksum(string message)
        {
            var bytes = Encoding.ASCII.GetBytes(message);
            var checksum = 0;
            for (var index = 0; index < bytes.Length; ++index)
            {
                switch ((char)bytes[index])
                {
                    case '$':
                        // Ignore the dollar sign, it indicates the start of the message.
                        break;
                    case '*':
                        // Stop processing before the asterisk, it indicates the end.
                        return checksum;

                    default:
                        // XOR the checksum with this character's value
                        checksum ^= bytes[index];
                        break;
                }
            }

            throw new ArgumentException("Checksum could not be calculated due to missing * character.");
        }
    }

    public enum FixQuality
    {
        Invalid = 0,
        Gps = 1,
        DGPS = 2,
        PPS = 3,
        RealTimeKinematic = 4,
        FloatRTK = 5,
        Estimated = 6,
        ManualInputMode = 7,
        SimulationMode = 8,
    }
}
