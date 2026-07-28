using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Globalization;
using System.Linq;

public class FloatArrayConverter : DefaultTypeConverter
{
    // CSV에서 읽어올 때 호출 (string -> float[], '_' 구분자 사용)
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (string.IsNullOrEmpty(text)) return new float[0];

        return text.Split('_')
                   .Select(s => float.Parse(s, CultureInfo.InvariantCulture))
                   .ToArray();
    }
}
