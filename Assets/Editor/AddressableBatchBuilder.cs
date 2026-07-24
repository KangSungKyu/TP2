#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AddressableBatchBuilder
{
    public static void ExecuteAutoRegisterAndBuild()
    {
        Debug.Log("[AddressableBatchBuilder] Auto Register & Build Started...");
        AddressableAutoRegister.RegisterAllAddressables();
        AddressablesDeployer.BuildAndDeploy();
        Debug.Log("[AddressableBatchBuilder] Completed!");
    }
}
#endif
