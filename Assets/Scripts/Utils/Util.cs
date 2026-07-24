
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public partial class Util
{
    public static DataTableType GetDataTableType(uint idx)
    {
        return (DataTableType)(idx / 1000);
    }

    public static uint GetDataInnerId(uint idx)
    {
        return idx % 1000;
    }

    public static uint CreateDataIdx(DataTableType type, uint innerId)
    {
        return ((uint)type * 1000) + (innerId);
    }

    public static List<T> ParseFromCSV<T>(string csvText)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",", // 구분자 설정
            PrepareHeaderForMatch = args => args.Header,
        };

        using (var reader = new StringReader(csvText))
        using (var csv = new CsvReader(reader, config))
        {
            // GetRecords는 스트리밍 방식으로 데이터를 읽어 객체 리스트로 반환
            return csv.GetRecords<T>().ToList();
        }
    }

    public static int GetRandom(int min, int max, int exclusive = int.MaxValue)
    {
        int r = UnityEngine.Random.Range(min, max);
        bool isValid = min <= exclusive && exclusive <= max;

        while (isValid && r == exclusive)
        {
            r = UnityEngine.Random.Range(min, max);
        }

        return r;
    }

    public static int GetRandom(int min, int max, int[] exclusives = null)
    {
        int r = UnityEngine.Random.Range(min, max);
        bool isValid = exclusives != null && exclusives.Length > 0;
        bool isFull = exclusives != null && max - min <= exclusives.Count(x => min <= x && x <= max);

        while (isValid && !isFull && exclusives.Contains(r))
        {
            r = UnityEngine.Random.Range(min, max);
            isFull = max - min <= exclusives.Count(x => min <= x && x <= max);
        }

        if (isFull)
        {
            r = -1;
        }

        return r;
    }

    // Convert a world position to a Canvas local position (anchoredPosition) suitable for UI placement.
    // canvas: target Canvas
    // cam: camera used for WorldToScreenPoint (use main camera). For ScreenSpace-Overlay canvas, cam may be null.
    public static Vector2 WorldToCanvasPosition(Canvas canvas, Camera cam, Vector3 worldPos)
    {
        if (canvas == null)
            return Vector2.zero;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        // For ScreenSpace-Overlay, RectTransformUtility.WorldToScreenPoint ignores camera, but call with cam (can be null)
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPos);

        Camera forUiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : (canvas.worldCamera ?? cam);

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, forUiCamera, out localPoint);

        return localPoint;
    }

    public static Vector3 CalcBezierPoint_Quadratic(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;

        Vector3 point = uu * p0;

        point += 2 * u * t * p1;
        point += tt * p2;

        return point;
    }

    public static string ToJson<T>(T data)
    {
        string json = JsonConvert.SerializeObject(data); // true: 가독성 좋게 들여쓰기

        return json;
    }

    public static T FromJson<T>(string json)
    {
        T data = default;

        if (json != string.Empty)
        {
            data = JsonConvert.DeserializeObject<T>(json);
        }

        return data;
    }
}