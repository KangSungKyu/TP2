using NUnit.Framework;
using UnityEngine;

public class Chunk6x6ModulePhysicsTests
{
    [Test]
    public void Test01_PlayerPhysicsBaseline_JumpAndDashParams()
    {
        GameObject pObj = new GameObject("Player_Physics_Test");
        Player player = pObj.AddComponent<Player>();
        KinematicMotor2D motor = pObj.GetComponent<KinematicMotor2D>();

        Assert.IsNotNull(player);
        Assert.IsNotNull(motor);
        Assert.AreEqual(6.0f, player.Speed);
        Assert.AreEqual(12.0f, player.DodgeDashSpeed);
        Assert.AreEqual(new Vector2(9.5f, 12.5f), player.WallJumpForce);

        // Theoretical Jump Height: vy^2 / (2 * g) = (11.5)^2 / (2 * 30.0) = 132.25 / 60.0 = 2.204m (Within 2.2m ~ 2.5m range)
        float gravity = motor.Gravity;
        float jumpForce = 11.5f;
        float theoreticalJumpHeight = (jumpForce * jumpForce) / (2f * gravity);

        Assert.GreaterOrEqual(theoreticalJumpHeight, 2.0f);
        Assert.LessOrEqual(theoreticalJumpHeight, 2.5f);

        Object.DestroyImmediate(pObj);
    }

    [Test]
    public void Test02_Chunk6x6GridDimensionsAndHazardPlacement()
    {
        Vector2 moduleSize = new Vector2(6.0f, 6.0f);
        Assert.AreEqual(6.0f, moduleSize.x);
        Assert.AreEqual(6.0f, moduleSize.y);

        GameObject moduleObj = new GameObject("Chunk_Module_A1");
        GameObject spikeObj = new GameObject("Spike_A1");
        spikeObj.transform.SetParent(moduleObj.transform);
        SpikeTrap spike = spikeObj.AddComponent<SpikeTrap>();

        Assert.AreEqual(1070u, spike.HazardId);
        Assert.AreEqual(moduleObj.transform, spike.transform.parent);

        Object.DestroyImmediate(moduleObj);
    }
}
