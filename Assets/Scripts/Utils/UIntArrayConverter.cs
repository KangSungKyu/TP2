using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Linq;

public class UIntArrayConverter : DefaultTypeConverter
{
    // CSV에서 읽어올 때 호출 (string -> uint[])
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (string.IsNullOrEmpty(text)) return new uint[0];

        // _를 기준으로 나누고 정수로 변환
        return text.Split('_').Select(uint.Parse).ToArray();
    }
}