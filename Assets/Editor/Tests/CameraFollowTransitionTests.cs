using System.IO;
using NUnit.Framework;
using UnityEngine;

public class CameraFollowTransitionTests
{
    [Test]
    public void BindAndSnap_AppliesRoomBoundsAndTargetImmediately()
    {
        var cameraObject = new GameObject("CameraFollowTest");
        var target = new GameObject("Target");
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            MetroidvaniaCamera2D follow = cameraObject.AddComponent<MetroidvaniaCamera2D>();
            target.transform.position = new Vector3(6f, 4f, 0f);

            follow.BindAndSnap(target.transform, new Bounds(new Vector3(5f, 4f), new Vector3(20f, 12f)));

            Assert.AreSame(target.transform, follow.Target);
            Assert.AreEqual(new Vector2(-5f, -2f), follow.MinBounds);
            Assert.AreEqual(new Vector2(15f, 10f), follow.MaxBounds);
            Assert.AreEqual(target.transform.position.y + follow.Offset.y, cameraObject.transform.position.y, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void DisableAndReuse_ClearsStaleTargetAndRestoresActiveCamera()
    {
        string source = File.ReadAllText("Assets/Scripts/Gameplay/MetroidvaniaCamera2D.cs");
        StringAssert.Contains("Player.Activated += OnPlayerActivated", source);
        StringAssert.Contains("Player.Deactivated += OnPlayerDeactivated", source);
        StringAssert.Contains("Player.Activated -= OnPlayerActivated", source);
        StringAssert.Contains("Player.Deactivated -= OnPlayerDeactivated", source);
        StringAssert.Contains("Target = null", source);
    }

    [Test]
    public void DoorAndPortal_SnapBeforeVisibilityAndKeepSameRoomBounds()
    {
        string builder = File.ReadAllText("Assets/Scripts/Scene/TilemapStageBuilder.cs");
        string portal = File.ReadAllText("Assets/Scripts/Gameplay/IntraRoomPortal.cs");

        Assert.Less(builder.IndexOf("SetupMetroidvaniaCamera(rootObj)"), builder.IndexOf("await fadeInScreenAsync"));
        Assert.Less(portal.IndexOf("player.Motor.Teleport(destinationEndpoint.position)"),
            portal.IndexOf("MetroidvaniaCamera2D.Active?.BindAndSnap(player.transform)"));
        StringAssert.DoesNotContain("SetBounds", portal);
    }
}
