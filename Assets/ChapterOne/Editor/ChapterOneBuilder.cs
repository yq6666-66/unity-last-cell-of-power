using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Unity.Pipeline.Commands;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.WarehouseExpanded;
using LastCellOfPower.ChapterOne;

// Adds presentation and an opt-in ability to a copy of the accepted warehouse.
// Does not change geometry, navigation data, source scenes or build settings.
public static class ChapterOneBuilder
{
    public const string ScenePath = "Assets/ChapterOne/Scenes/WarehouseChapterOne.unity";
    public const string FontPath = "Assets/ChapterOne/Fonts/ChapterChinese.asset";
    public const string GameplayPath = "Assets/ChapterOne/Config/ChapterGameplay.asset";
    public const string LevelPath = "Assets/ChapterOne/Config/ChapterLevel.asset";
    public const string PulsePath = "Assets/ChapterOne/Config/PulseProfile.asset";
    public const string SourceFontPath = "Assets/ChapterOne/Fonts/NotoSansCJKsc-Regular.otf";
    public static string LastScenePath { get; private set; }
    const string SourceScene = "Assets/WarehouseExpanded/Scenes/WarehouseExpanded.unity";
    static readonly string[] ClipNames = { "patrol_loop", "alert", "chase_loop", "pulse", "recover", "charge_loop", "charge_full", "supply", "door", "success", "failure", "ui_click", "ambient_loop" };
    static Task prerequisiteTask;

    public static void Create()
    {
        CreateAt(ScenePath, FontPath, GameplayPath, LevelPath, PulsePath, "Assets/ChapterOne/Materials");
    }

    public static void CreateRevision(string revision)
    {
        if (string.IsNullOrWhiteSpace(revision) || revision.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-'))
            throw new ArgumentException("Revision must contain only letters, digits, underscore or hyphen");
        string root = "Assets/ChapterOne/Revisions/" + revision;
        CreateAt(root + "/WarehouseChapterOne.unity", root + "/ChapterChinese.asset", root + "/ChapterGameplay.asset",
            root + "/ChapterLevel.asset", root + "/PulseProfile.asset", root + "/Materials");
    }

    static void CreateAt(string scenePath, string fontPath, string gameplayPath, string levelPath, string pulsePath, string materials)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Chapter generation requires edit mode");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Preserve unsaved scene changes before generation");
        foreach (string path in new[] { scenePath, fontPath, gameplayPath, levelPath, pulsePath, materials + "/PulseRing.mat", materials + "/GuardStatus.mat" })
            if (File.Exists(path)) throw new InvalidOperationException("Output exists: " + path + ". Preserve it and use a new revision name.");
        if (!File.Exists(SourceFontPath)) throw new FileNotFoundException("Bundled Noto font must be supplied first", SourceFontPath);
        if (!File.Exists("Assets/ChapterOne/Fonts/LICENSE.txt")) throw new FileNotFoundException("Bundled font license is required");
        foreach (var name in ClipNames)
            if (!File.Exists("Assets/ChapterOne/Audio/" + name + ".wav")) throw new FileNotFoundException("Chapter audio missing: " + name);
        EnsureTmpEssentials();
        foreach (var path in new[] { scenePath, fontPath, gameplayPath, levelPath, pulsePath }) Directory.CreateDirectory(Path.GetDirectoryName(path));
        Directory.CreateDirectory(materials);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var font = CreateChineseFont(fontPath);
        var clips = ClipNames.ToDictionary(name => name, LoadClip);
        var source = EditorSceneManager.OpenScene(SourceScene);
        if (!EditorSceneManager.SaveScene(source, scenePath, true)) throw new InvalidOperationException("Could not save independent chapter scene");
        var scene = EditorSceneManager.OpenScene(scenePath);
        LastScenePath = scenePath;
        var sessions = UnityEngine.Object.FindObjectsByType<ExpandedSession>(FindObjectsSortMode.None);
        if (sessions.Length != 1) throw new InvalidOperationException("Source scene must contain exactly one ExpandedSession");
        var session = sessions[0];
        if (!session.player || !session.enemy || !session.profile || !session.level) throw new InvalidOperationException("Source actor/configuration references missing");
        if (session.pulse || session.statistics || session.externalPlayerUI) throw new InvalidOperationException("Source is already chapter-enabled");

        var gameplay = UnityEngine.Object.Instantiate(session.profile); gameplay.name = "ChapterGameplay"; gameplay.Validate();
        var level = UnityEngine.Object.Instantiate(session.level); level.name = "ChapterLevel"; level.Validate();
        var pulseProfile = ScriptableObject.CreateInstance<PulseProfile>(); pulseProfile.name = "ChapterPulse"; pulseProfile.Validate();
        AssetDatabase.CreateAsset(gameplay, gameplayPath); AssetDatabase.CreateAsset(level, levelPath); AssetDatabase.CreateAsset(pulseProfile, pulsePath);
        session.profile = gameplay; session.level = level; session.enemy.profile = gameplay; session.enemy.level = level;
        session.uiFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath); // F1 also uses redistributable glyphs.
        session.externalPlayerUI = true;
        var statistics = session.gameObject.AddComponent<RunStatistics>();
        var pulse = session.gameObject.AddComponent<EmergencyPulseController>();
        pulse.profile = pulseProfile; pulse.session = session; pulse.player = session.player;
        pulse.enemy = session.enemy; pulse.battery = session.battery; pulse.interaction = session.interaction;
        session.statistics = statistics; session.pulse = pulse;

        var presentation = new GameObject("ChapterPresentation");
        var audio = presentation.AddComponent<ChapterAudioFeedback>(); audio.session = session; audio.pulse = pulse;
        audio.patrolLoop = clips["patrol_loop"]; audio.alert = clips["alert"]; audio.chaseLoop = clips["chase_loop"];
        audio.pulseHit = clips["pulse"]; audio.recover = clips["recover"]; audio.chargeLoop = clips["charge_loop"];
        audio.chargeFull = clips["charge_full"]; audio.supply = clips["supply"]; audio.doorOpen = clips["door"];
        audio.success = clips["success"]; audio.failure = clips["failure"]; audio.uiClick = clips["ui_click"]; audio.ambientLoop = clips["ambient_loop"];
        var ui = presentation.AddComponent<ChapterPlayerUI>();
        ui.session = session; ui.pulse = pulse; ui.statistics = statistics; ui.font = font; ui.audioFeedback = audio;
        var feedback = presentation.AddComponent<ChapterVisualFeedback>(); feedback.session = session; feedback.pulse = pulse;
        feedback.pulseMaterial = MakeMaterial(materials + "/PulseRing.mat", new Color(.25f, .85f, 1, .65f), true);
        var status = GameObject.CreatePrimitive(PrimitiveType.Sphere); status.name = "ChapterGuardStatus";
        UnityEngine.Object.DestroyImmediate(status.GetComponent<Collider>()); status.layer = session.enemy.gameObject.layer;
        status.transform.SetParent(session.enemy.transform, false); status.transform.localPosition = new Vector3(0, 1.2f, 0); status.transform.localScale = Vector3.one * .12f;
        status.GetComponent<Renderer>().sharedMaterial = MakeMaterial(materials + "/GuardStatus.mat", new Color(.4f, .9f, 1, 1), false);
        var lamp = status.AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 2.5f; lamp.intensity = .8f;
        lamp.color = new Color(.4f, .9f, 1); lamp.shadows = LightShadows.None; feedback.enemyStatusLight = lamp;
        BindDeviceHighlights(session, feedback);
        AddWayfinding(font, session.player.viewCamera);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("CHAPTER_SCENE_CREATED " + scenePath + "; layout/nav retained; formal UI and pulse enabled; not runtime acceptance");
        Debug.Log("CHAPTER_FONT_PREWARM characters=" + font.characterTable.Count + " atlases=" + font.atlasTextures.Length);
    }

    [CliCommand("chapter", "repair_highlights", "Bind complete chapter devices to their interaction highlights")]
    public static string RepairHighlights()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Highlight repair requires edit mode");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve all unsaved scene changes before highlight repair");
        if (!File.Exists(ScenePath)) throw new FileNotFoundException("Generate the chapter scene before repairing highlights", ScenePath);
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (scene.path != ScenePath) throw new InvalidOperationException("Only the independent chapter scene may be repaired");
        var sessions = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ExpandedSession>(true)).ToArray();
        var visuals = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ChapterVisualFeedback>(true)).ToArray();
        if (sessions.Length != 1 || visuals.Length != 1 || visuals[0].session != sessions[0])
            throw new InvalidOperationException("Expected one correctly associated chapter session and visual feedback component");

        string backup = Path.Combine(Path.GetTempPath(), "LastCell-ChapterHighlights-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(backup);
        File.Copy(ScenePath, Path.Combine(backup, Path.GetFileName(ScenePath)));
        if (File.Exists(ScenePath + ".meta")) File.Copy(ScenePath + ".meta", Path.Combine(backup, Path.GetFileName(ScenePath) + ".meta"));
        BindDeviceHighlights(sessions[0], visuals[0]);
        EditorUtility.SetDirty(visuals[0]);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save repaired chapter highlights");
        string summary = string.Join("; ", visuals[0].highlightBindings.Select(binding => binding.target.name + "=" + binding.renderers.Length + " model renderers"));
        Debug.Log("CHAPTER_DEVICE_HIGHLIGHTS_REPAIRED " + summary + "; backup=" + backup);
        return "Bound complete chapter devices: " + summary + ". Scene backup: " + backup;
    }

    static void BindDeviceHighlights(ExpandedSession session, ChapterVisualFeedback feedback)
    {
        if (!session || !feedback || !session.interaction || session.interaction.targets == null || session.interaction.targets.Length == 0)
            throw new InvalidOperationException("Cannot bind device highlights without session/interaction references");
        var scene = session.gameObject.scene;
        if (!scene.path.StartsWith("Assets/ChapterOne/", StringComparison.Ordinal) || feedback.gameObject.scene != scene)
            throw new InvalidOperationException("Highlight binding is restricted to independent ChapterOne scenes");
        var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        var bindings = new List<ChapterVisualFeedback.HighlightBinding>();
        var uniqueTargets = new HashSet<EscapeInteractable>();
        var uniqueRenderers = new HashSet<Renderer>();
        foreach (var target in session.interaction.targets)
        {
            if (!target || !uniqueTargets.Add(target)) throw new InvalidOperationException("Missing or duplicate chapter interaction target");
            string modelName;
            if (target.kind == InteractionKind.Charger)
            {
                const string prefix = "Charger_";
                if (!target.name.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(target.name.Substring(prefix.Length), out int index) || index < 0)
                    throw new InvalidOperationException("Cannot resolve charging device for " + target.name);
                modelName = "ChargingDock_" + index;
            }
            else if (target.kind == InteractionKind.Terminal) modelName = "SupplyTerminal";
            else throw new InvalidOperationException("Unsupported interaction target kind: " + target.kind);
            var models = transforms.Where(t => t && t.name == modelName).ToArray();
            if (models.Length != 1) throw new InvalidOperationException("Expected exactly one device model " + modelName + ", found " + models.Length);
            var renderers = models[0].GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Device has no model renderers: " + modelName);
            foreach (var renderer in renderers)
                if (!renderer || !uniqueRenderers.Add(renderer)) throw new InvalidOperationException("Missing or shared renderer in device highlight bindings: " + modelName);
            bindings.Add(new ChapterVisualFeedback.HighlightBinding { target = target, renderers = renderers });
        }
        // Commit once, after every target has a valid complete-model binding.
        feedback.highlightBindings = bindings.ToArray();
        EditorUtility.SetDirty(feedback);
    }

    /// <summary>
    /// AssetDatabase.ImportPackage schedules work rather than completing all resource
    /// imports before returning. Await this from the headless command before Create.
    /// </summary>
    public static Task EnsurePrerequisitesAsync()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Prepare TMP resources in edit mode");
        if (TmpEssentialsReady()) return Task.CompletedTask;
        if (prerequisiteTask != null && !prerequisiteTask.IsCompleted) return prerequisiteTask;

        const string packageName = "TMP Essential Resources";
        bool existingDirectory = Directory.Exists("Assets/TextMesh Pro");
        string essentials = null;
        string packageDescription = "existing TMP resources";
        if (!existingDirectory)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_FontAsset).Assembly);
            if (package == null) throw new InvalidOperationException("Cannot locate installed uGUI/TMP package");
            essentials = Path.Combine(package.resolvedPath, "Package Resources/" + packageName + ".unitypackage");
            if (!File.Exists(essentials)) throw new FileNotFoundException("Installed TMP Essential Resources package missing", essentials);
            packageDescription = package.name + " " + package.version;
        }

        var completion = new TaskCompletionSource<bool>();
        prerequisiteTask = completion.Task;
        double deadline = EditorApplication.timeSinceStartup + 60;
        bool finished = false;
        bool completionObserved = false;
        bool IsEssentials(string name) => string.Equals(Path.GetFileNameWithoutExtension(name), packageName, StringComparison.OrdinalIgnoreCase);
        void Cleanup()
        {
            AssetDatabase.importPackageCompleted -= Completed;
            AssetDatabase.importPackageFailed -= Failed;
            AssetDatabase.importPackageCancelled -= Cancelled;
            EditorApplication.update -= Poll;
            EditorApplication.quitting -= Quitting;
            AssemblyReloadEvents.beforeAssemblyReload -= Reloading;
        }
        void Fail(Exception error)
        {
            if (finished) return;
            finished = true; Cleanup(); completion.TrySetException(error);
        }
        void Completed(string name)
        {
            if (IsEssentials(name)) completionObserved = true;
            // Continue only from a later idle Editor update, not from an import callback.
        }
        void Failed(string name, string error)
        {
            if (IsEssentials(name)) Fail(new InvalidOperationException("TMP Essentials import failed: " + error));
        }
        void Cancelled(string name)
        {
            if (IsEssentials(name)) Fail(new OperationCanceledException("TMP Essentials import was cancelled"));
        }
        void Quitting() { Fail(new OperationCanceledException("Editor quit during TMP resource preparation")); }
        void Reloading() { Fail(new InvalidOperationException("Assembly reload interrupted TMP preparation; retry after Editor becomes ready")); }
        void Poll()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("TMP Settings/SDF shader did not become loadable within 60 seconds. Existing resources were not overwritten; import completion observed=" + completionObserved);
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("PlayMode began during TMP resource preparation");
                if (!TmpEssentialsReady()) return;
                finished = true; Cleanup();
                Debug.Log("CHAPTER_TMP_ESSENTIALS_READY from " + packageDescription + "; Examples/Extras not imported");
                completion.TrySetResult(true);
            }
            catch (Exception e) { Fail(e); }
        }

        AssetDatabase.importPackageCompleted += Completed;
        AssetDatabase.importPackageFailed += Failed;
        AssetDatabase.importPackageCancelled += Cancelled;
        EditorApplication.update += Poll;
        EditorApplication.quitting += Quitting;
        AssemblyReloadEvents.beforeAssemblyReload += Reloading;
        try
        {
            if (!existingDirectory) AssetDatabase.ImportPackage(essentials, false);
            // With an existing directory, allow a pending import to settle, but never
            // re-import over a partial installation. Poll diagnoses it on timeout.
        }
        catch (Exception e) { Fail(e); }
        return prerequisiteTask;
    }

    static bool TmpEssentialsReady()
    {
        if (EditorApplication.isUpdating || EditorApplication.isCompiling) return false;
        if (!Resources.Load<TMP_Settings>("TMP Settings")) return false;
        if (!TMP_Settings.LoadDefaultSettings()) return false;
        return Shader.Find("TextMeshPro/Mobile/Distance Field") != null;
    }

    static void EnsureTmpEssentials()
    {
        if (!TmpEssentialsReady())
            throw new InvalidOperationException("TMP prerequisites are not ready; await ChapterOneBuilder.EnsurePrerequisitesAsync() before Create. No import was started by Create.");
    }

    static TMP_FontAsset CreateChineseFont(string assetPath)
    {
        var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (!source) throw new InvalidOperationException("Noto font has not imported as a Font asset");
        var font = TMP_FontAsset.CreateFontAsset(source, 72, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        if (!font) throw new InvalidOperationException("Noto font atlas creation failed; verify embedded font data");
        font.name = "ChapterChinese";
        AssetDatabase.CreateAsset(font, assetPath);
        var serialized = new SerializedObject(font);
        var clear = serialized.FindProperty("m_ClearDynamicDataOnBuild");
        if (clear == null) throw new InvalidOperationException("Installed TMP font serialization contract changed");
        clear.boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo();
        string characters = RequiredCharacters();
        if (!font.TryAddCharacters(characters, out string missing, true) && !string.IsNullOrEmpty(missing))
            throw new InvalidOperationException("Noto missing chapter characters: " + string.Join(",", missing.Distinct().Select(c => "U+" + ((int)c).ToString("X4"))));
        if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var atlas in font.atlasTextures)
            if (atlas && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
        EditorUtility.SetDirty(font); EditorUtility.SetDirty(font.material);
        foreach (var atlas in font.atlasTextures) if (atlas) EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets(); return font;
    }

    public static string RequiredCharacters()
    {
        var chars = new HashSet<char>(Enumerable.Range(32, 95).Select(i => (char)i));
        string labels = "01 维修起步区02 货架区03 设备区04 装卸出口区通道出口平台供电充电先补充电量并为撤离留出余量最后一格电前往设备区供电终端应急干扰";
        foreach (char c in labels) chars.Add(c);
        foreach (string folder in new[] { "Assets/ChapterOne/Runtime", "Assets/WarehouseExpanded/Runtime", "Assets/EscapeSample/Runtime" })
            foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                foreach (char c in File.ReadAllText(file))
                    if (!char.IsControl(c) && !char.IsSurrogate(c) && char.GetUnicodeCategory(c) != UnicodeCategory.Format) chars.Add(c);
        return new string(chars.OrderBy(c => c).ToArray());
    }

    static AudioClip LoadClip(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ChapterOne/Audio/" + name + ".wav");
        if (!clip || clip.length <= 0) throw new InvalidOperationException("Audio failed to import: " + name); return clip;
    }

    static Material MakeMaterial(string path, Color color, bool transparent)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader) throw new InvalidOperationException("URP Unlit shader missing");
        var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
        material.SetColor("_BaseColor", color);
        if (transparent)
        {
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_ZWrite", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent"); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = (int)RenderQueue.Transparent;
        }
        AssetDatabase.CreateAsset(material, path); return material;
    }

    static void AddWayfinding(TMP_FontAsset font, Camera camera)
    {
        var root = new GameObject("ChapterWayfinding").transform;
        Color repair = new Color(.32f, .86f, .69f), storage = new Color(.96f, .73f, .24f), equipment = new Color(.35f, .69f, .92f), exit = new Color(.85f, .96f, .89f);
        Sign(root, font, camera, new Vector3(-.23f, 1.5f, -4.6f), Vector3.left, "02 货架区\n通道 >", storage);
        Sign(root, font, camera, new Vector3(.23f, 1.5f, -4.6f), Vector3.right, "01 维修起步区\n< 通道", repair);
        Sign(root, font, camera, new Vector3(-.23f, 1.5f, 4.6f), Vector3.left, "04 装卸出口区\n< 通道", exit);
        Sign(root, font, camera, new Vector3(.23f, 1.5f, 4.6f), Vector3.right, "03 设备区\n通道 >", equipment);
        Sign(root, font, camera, new Vector3(-4.6f, 1.5f, -.23f), Vector3.back, "03 设备区\n< 通道", equipment);
        Sign(root, font, camera, new Vector3(-4.6f, 1.5f, .23f), Vector3.forward, "01 维修起步区\n通道 >", repair);
        Sign(root, font, camera, new Vector3(4.6f, 1.5f, -.23f), Vector3.back, "04 装卸出口区\n通道 >", exit);
        Sign(root, font, camera, new Vector3(4.6f, 1.5f, .23f), Vector3.forward, "02 货架区\n< 通道", storage);
        Sign(root, font, camera, new Vector3(8, 2.0f, 11.93f), Vector3.back, "04 出口\n先前往设备区供电", exit, 2.2f);
    }

    static void Sign(Transform parent, TMP_FontAsset font, Camera camera, Vector3 position, Vector3 viewerDirection, string label, Color accent, float width = 2)
    {
        var go = new GameObject("Sign_" + label.Replace('\n', '_'), typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-viewerDirection, Vector3.up));
        go.transform.localScale = Vector3.one * .005f;
        var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(width / .005f, 120);
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
        var background = new GameObject("Backing", typeof(RectTransform), typeof(Image)); background.transform.SetParent(go.transform, false);
        Stretch(background.GetComponent<RectTransform>()); background.GetComponent<Image>().color = new Color(.035f, .065f, .085f, 1); background.GetComponent<Image>().raycastTarget = false;
        var stripe = new GameObject("ZoneBand", typeof(RectTransform), typeof(Image)); stripe.transform.SetParent(go.transform, false);
        var band = stripe.GetComponent<RectTransform>(); band.anchorMin = Vector2.zero; band.anchorMax = new Vector2(0, 1); band.offsetMin = Vector2.zero; band.offsetMax = new Vector2(12, 0);
        stripe.GetComponent<Image>().color = accent; stripe.GetComponent<Image>().raycastTarget = false;
        var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(go.transform, false);
        var textRect = text.GetComponent<RectTransform>(); Stretch(textRect); textRect.offsetMin = new Vector2(26, 8); textRect.offsetMax = new Vector2(-12, -8);
        var tmp = text.GetComponent<TextMeshProUGUI>(); tmp.font = font; tmp.text = label; tmp.fontSize = 32; tmp.enableAutoSizing = true; tmp.fontSizeMin = 25; tmp.fontSizeMax = 32;
        tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.MidlineLeft; tmp.raycastTarget = false;
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
}

