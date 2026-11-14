using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LastCellOfPower.EscapeSample;

public static class EscapeResourceChecks
{
    static void Near(float actual, float expected, string label)
    {
        if (Mathf.Abs(actual - expected) > .001f)
            throw new Exception(label + ": expected=" + expected + " actual=" + actual);
    }

    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Resource checks require edit mode");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("EscapeResourceCheck");
            SceneManager.MoveGameObjectToScene(root, scene);
            var rules = root.AddComponent<EscapeRules>();
            var battery = root.AddComponent<EscapeBattery>();
            battery.rules = rules;
            battery.Initialize(); Near(battery.Current, 35, "initial");
            battery.Tick(10, false, false); Near(battery.Current, 25, "idle drain");
            battery.Tick(10, true, false); Near(battery.Current, 10, "moving drain");
            battery.Tick(5, false, true); Near(battery.Current, 45, "net charging");
            if (!battery.Spend(45)) throw new Exception("Exact fee must succeed");
            Near(battery.Current, 0, "exact fee reaches zero");
            if (!battery.Depleted || battery.Spend(1)) throw new Exception("Empty battery cannot spend");
            battery.Initialize(); battery.Tick(20, false, true); Near(battery.Current, 100, "charging cap");
            battery.Tick(5, false, true); Near(battery.Current, 100, "holding full charge");
            battery.Tick(200, false, false); Near(battery.Current, 0, "depletion clamp");
            battery.Initialize(); battery.StopRate(); Near(battery.Current, 35, "stop preserves charge");
            Near(battery.LastNetRate, 0, "stop clears rate");
            string dir = Path.Combine(Application.dataPath, "../Evidence/EscapeSample");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "resource-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"),
                "PASS resource numeric boundaries: initial, idle, moving, net charging, exact fee, insufficient fee, cap, depletion, StopRate.\nComponent logic only; does not prove real wall movement, pause dispatch or input.\n");
            Debug.Log("ESCAPE_RESOURCE_CHECK_PASS");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
