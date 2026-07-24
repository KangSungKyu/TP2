using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public static class PanelManager
{
    private static Dictionary<string, PanelBase> panelList = new Dictionary<string, PanelBase>();

    public static void RegisterPanel(PanelBase panel)
    {
        RegisterPanel(panel.name, panel);
    }

    public static void RegisterPanel(string name, PanelBase panel)
    {
        if (panel == null)
        {
            return;
        }

        if (panelList.ContainsKey(name))
        {
            Debug.LogError($"duplicated insert panel in panelManager, {(panel != null ? panel.name : "null panel")}");
        }

        panelList.Add(name, panel);
    }

    public static void UnregisterPanel(PanelBase panel)
    {
        if (panel == null)
        {
            return;
        }

        UnregisterPanel(panel.name);
    }

    public static void UnregisterPanel(string name)
    {
        if (!panelList.Remove(name))
        {
            Debug.LogError($"failed remove panel from panelManager, {name}");
        }
    }

    public static T GetPanel<T>(string name) where T : PanelBase
    {
        return GetPanel(name) as T;
    }

    public static PanelBase GetPanel(string name)
    {
        if(panelList.ContainsKey(name))
        {
            return panelList[name];
        }

        return null;
    }
}
