using UnityEngine;

public enum ChunkSocketDirection : byte { North, East, South, West }

public sealed class ChunkSocketMarker : MonoBehaviour
{
    public ChunkSocketDirection Direction;
    public Transform EntryMarker;
}
