using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;

public sealed class ZeroOneBooleanConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (text == "0") return false;
        if (text == "1") return true;
        throw new FormatException($"Boolean CSV value must be 0 or 1, but was '{text}'.");
    }

    public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
    {
        if (value is bool boolean) return boolean ? "1" : "0";
        throw new FormatException("Boolean CSV value must be a bool.");
    }
}
