using System;
using System.Collections.Generic;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using UnityEngine;

public sealed class AttackMotionTypeConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) && value <= 2)
            return (AttackMotionType)value;
        Debug.LogWarning($"[AttackMotionProfileDataTable] Invalid motiontype '{text}'; Stationary fallback applied.");
        return AttackMotionType.Stationary;
    }
}

public sealed class AttackTargetPolicyConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) && value <= 1)
            return (AttackTargetPolicy)value;
        Debug.LogWarning($"[AttackMotionProfileDataTable] Invalid targetpolicy '{text}'; Snapshot fallback applied.");
        return AttackTargetPolicy.SnapshotAtStartup;
    }
}

public sealed class AttackMotionProfileDataTable : IDataLoad
{
    public const uint StationaryProfileIdx = 10001;
    private readonly Dictionary<uint, AttackMotionProfileData> profiles = new Dictionary<uint, AttackMotionProfileData>();

    public int GetDataCount() => profiles.Count;

    public void LoadData(string csvText)
    {
        var replacement = new Dictionary<uint, AttackMotionProfileData>();
        foreach (AttackMotionProfileData profile in Util.ParseFromCSV<AttackMotionProfileData>(csvText))
        {
            if (Util.GetDataTableType(profile.Idx) != DataTableType.AttackMotionProfile ||
                replacement.ContainsKey(profile.Idx))
                throw new InvalidOperationException($"Invalid or duplicate AttackMotionProfile idx {profile.Idx}.");
            replacement.Add(profile.Idx, profile);
        }
        profiles.Clear();
        foreach (var pair in replacement) profiles.Add(pair.Key, pair.Value);
    }

    public bool TryGetValid(uint idx, out AttackMotionProfileData profile)
    {
        if (profiles.TryGetValue(idx, out profile) && IsValid(profile)) return true;
        profile = null;
        return false;
    }

    public bool Contains(uint idx) => profiles.ContainsKey(idx);

    public void Release() => profiles.Clear();

    private static bool IsValid(AttackMotionProfileData profile)
    {
        if (profile == null || !Enum.IsDefined(typeof(AttackMotionType), profile.MotionType) ||
            !Enum.IsDefined(typeof(AttackTargetPolicy), profile.TargetPolicy)) return false;
        if (profile.MotionType == AttackMotionType.Stationary) return true;
        if (!profile.Enabled || !IsFinitePositive(profile.MaxDistance) || !IsFinitePositive(profile.MaxSpeed))
            return false;
        return profile.MotionType == AttackMotionType.Step
            ? IsFiniteNonNegative(profile.Acceleration)
            : IsFinitePositive(profile.Acceleration);
    }

    private static bool IsFinitePositive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFiniteNonNegative(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
}
