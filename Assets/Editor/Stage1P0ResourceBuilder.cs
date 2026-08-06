#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class Stage1P0ResourceBuilder
{
    private static readonly (uint resourceIdx, uint chunkIdx)[] Ids =
    {
        (1050, 11050), (1051, 11051), (1052, 11052), (1053, 11053),
        (1056, 11056), (1057, 11057), (1061, 11061), (1063, 11063)
    };

    [MenuItem("TP2/Build Stage 1 P0 Resources")]
    public static void Build()
    {
        var tile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Textures/Environment/Tiles/Tile_Ground.asset");
        if (tile == null) throw new System.InvalidOperationException("Tile_Ground.asset is required.");

        foreach (var id in Ids) BuildChunk(id.resourceIdx, id.chunkIdx, tile);
        AssetDatabase.SaveAssets();
        AddressablePipeline.RegisterAllAddressables();
    }

    private static void BuildChunk(uint resourceIdx, uint chunkIdx, Tile tile)
    {
        string name = $"Room_{chunkIdx}";
        var root = new GameObject(name, typeof(Grid));
        var ground = new GameObject("Tilemap_Ground", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D), typeof(Rigidbody2D), typeof(CompositeCollider2D));
        ground.transform.SetParent(root.transform);
        ground.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        ground.GetComponent<TilemapCollider2D>().usedByComposite = true;
        var map = ground.GetComponent<Tilemap>();

        for (int x = -30; x < 30; x++) map.SetTile(new Vector3Int(x, 0), tile);
        for (int y = 1; y < 30; y++)
        {
            map.SetTile(new Vector3Int(-30, y), tile);
            map.SetTile(new Vector3Int(29, y), tile);
        }

        AddSocket(root.transform, ChunkSocketDirection.North, new Vector3(0, 29));
        AddSocket(root.transform, ChunkSocketDirection.East, new Vector3(28, 1));
        AddSocket(root.transform, ChunkSocketDirection.South, new Vector3(0, 1));
        AddSocket(root.transform, ChunkSocketDirection.West, new Vector3(-29, 1));

        var cameraBounds = new GameObject("CameraBounds", typeof(BoxCollider2D));
        cameraBounds.transform.SetParent(root.transform);
        cameraBounds.transform.localPosition = new Vector3(-0.5f, 15f);
        cameraBounds.GetComponent<BoxCollider2D>().size = new Vector2(60f, 30f);
        cameraBounds.GetComponent<BoxCollider2D>().isTrigger = true;

        AddSpawn(root.transform, "EntrySpawn", new Vector3(-22, 1.5f), SpawnType.Player, 0);
        if (resourceIdx <= 1053)
        {
            AddSpawn(root.transform, "MonsterSpawn_1", new Vector3(-8, 1.5f), SpawnType.Monster, resourceIdx % 2 == 0 ? 3104u : 3105u);
            AddSpawn(root.transform, "MonsterSpawn_2", new Vector3(7, 1.5f), SpawnType.Monster, resourceIdx % 2 == 0 ? 3101u : 3102u);
        }

        PrefabUtility.SaveAsPrefabAsset(root, $"Assets/Prefabs/Rooms/{name}.prefab");
        Object.DestroyImmediate(root);
    }

    private static void AddSocket(Transform parent, ChunkSocketDirection direction, Vector3 position)
    {
        var socket = new GameObject($"Socket_{direction}", typeof(ChunkSocketMarker));
        socket.transform.SetParent(parent);
        socket.transform.localPosition = position;
        var entry = new GameObject($"Entry_{direction}");
        entry.transform.SetParent(socket.transform);
        var marker = socket.GetComponent<ChunkSocketMarker>();
        marker.Direction = direction;
        marker.EntryMarker = entry.transform;
    }

    private static void AddSpawn(Transform parent, string name, Vector3 position, SpawnType type, uint unitIdx)
    {
        var go = new GameObject(name, typeof(SpawnPointMarker));
        go.transform.SetParent(parent);
        go.transform.localPosition = position;
        var marker = go.GetComponent<SpawnPointMarker>();
        marker.Type = type;
        marker.MonsterId = unitIdx;
    }
}
#endif
