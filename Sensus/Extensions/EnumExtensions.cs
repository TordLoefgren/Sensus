using System.ComponentModel;

namespace Sensus.Extensions
{
    public static class EnumExtensions
    {
        public static string ToDisplayString(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());

            var attribute = field?
                .GetCustomAttributes(typeof(DescriptionAttribute), false)
                .Cast<DescriptionAttribute>()
                .FirstOrDefault();

            return attribute?.Description ?? value.ToString();
        }
    }
}
