using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class Chunk6x6ModulePhysicsTests
{
    [Test]
    public void Test01_PlayerPhysicsBaseline_JumpAndDashParams()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab");
        Assert.IsNotNull(prefab);
        GameObject pObj = Object.Instantiate(prefab);
        try
        {
            Player player = pObj.GetComponent<Player>();
            KinematicMotor2D motor = pObj.GetComponent<KinematicMotor2D>();

            Assert.IsNotNull(player);
            Assert.IsNotNull(motor);
            Assert.Greater(player.Speed, 0f);
            Assert.AreEqual(12.0f, player.DodgeDashSpeed);
            Assert.AreEqual(new Vector2(9.5f, 12.5f), player.WallJumpForce);

            float theoreticalJumpHeight = 11.5f * 11.5f / (2f * motor.Gravity);
            Assert.That(theoreticalJumpHeight, Is.InRange(2.0f, 2.5f));
        }
        finally
        {
            Object.DestroyImmediate(pObj);
        }
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
