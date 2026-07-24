using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Linq;

public class IntArrayConverter : DefaultTypeConverter
{
    // CSV에서 읽어올 때 호출 (string -> int[])
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (string.IsNullOrEmpty(text)) return new int[0];

        // _를 기준으로 나누고 정수로 변환
        return text.Split('_').Select(int.Parse).ToArray();
    }
}