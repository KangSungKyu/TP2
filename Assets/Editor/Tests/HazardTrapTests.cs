using NUnit.Framework;
using UnityEngine;

public class HazardTrapTests
{
    [Test]
    public void Test01_SpikeTrap_InitializationAndSurfaceAlignment()
    {
        GameObject spikeObj = new GameObject("SpikeTrap_Test");
        SpikeTrap spike = spikeObj.AddComponent<SpikeTrap>();
        typeof(SpikeTrap).GetMethod("Awake", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).Invoke(spike, null);

        Assert.AreEqual(1070u, spike.HazardId);
        Assert.AreEqual(15, spike.Damage);
        Assert.AreEqual(0f, spike.KnockbackForce);
        Assert.AreEqual(0.5f, spike.CooldownBetweenHits);

        spike.AlignToSurface(Vector2.up);
        Assert.AreEqual(Vector2.up, spike.SurfaceNormal);

        Object.DestroyImmediate(spikeObj);
    }

    [Test]
    public void Test02_SawBladeTrap_InitializationAndWaypoints()
    {
        GameObject sawObj = new GameObject("SawBladeTrap_Test");
        SawBladeTrap saw = sawObj.AddComponent<SawBladeTrap>();
        typeof(SawBladeTrap).GetMethod("Awake", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).Invoke(saw, null);

        Assert.AreEqual(1071u, saw.HazardId);
        Assert.AreEqual(20, saw.Damage);
        Assert.AreEqual(0f, saw.KnockbackForce);
        Assert.AreEqual(0.4f, saw.CooldownBetweenHits);
        Assert.Greater(saw.RotationSpeed, 0f);

        GameObject p1 = new GameObject("P1");
        GameObject p2 = new GameObject("P2");
        p1.transform.position = Vector3.zero;
        p2.transform.position = new Vector3(5f, 0f, 0f);

        saw.SetupWaypoints(new Transform[] { p1.transform, p2.transform }, 3.0f, SawBladeTrap.MovementMode.PingPong);
        Assert.IsTrue(saw.EnableMovement);

        Object.DestroyImmediate(sawObj);
        Object.DestroyImmediate(p1);
        Object.DestroyImmediate(p2);
    }
}
