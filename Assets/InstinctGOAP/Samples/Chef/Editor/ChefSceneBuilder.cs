using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Instinct.GOAP.Samples.Chef.EditorTools
{

    public static class ChefSceneBuilder
    {
        private const string ScenePath = "Assets/InstinctGOAP/Samples/Chef/Chef_Kitchen_Demo.unity";

        [MenuItem("GOAP/Samples/Chef/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(new Vector3(0f, 12f, -12f), Quaternion.Euler(45f, 0f, 0f), 6.5f);

            var root = new GameObject("Chef Kitchen Demo");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localScale = new Vector3(1.8f, 1f, 1.4f);
            SetMaterial(ground, new Color(0.62f, 0.58f, 0.52f));

            Transform stove = CreateZone(root.transform, "Stove", PrimitiveType.Cube,
                new Vector3(3f, 0.5f, 0f), new Vector3(1.2f, 1.2f, 1.2f), new Color(1f, 0.45f, 0.15f));
            Transform storage = CreateZone(root.transform, "Storage", PrimitiveType.Cube,
                new Vector3(-3f, 0.5f, 0f), new Vector3(1.2f, 1.2f, 1.2f), new Color(0.3f, 0.55f, 1f));
            Transform client = CreateZone(root.transform, "Client", PrimitiveType.Sphere,
                new Vector3(4f, 0.5f, 2f), new Vector3(0.8f, 0.8f, 0.8f), new Color(1f, 0.8f, 0.25f));
            Transform entrance = CreateZone(root.transform, "Entrance", PrimitiveType.Cube,
                new Vector3(6f, 0.5f, -1f), new Vector3(0.9f, 1f, 0.9f), new Color(0.35f, 0.85f, 0.4f));
            Transform breakSpot = CreateZone(root.transform, "Break", PrimitiveType.Cube,
                new Vector3(-5f, 0.5f, -3f), new Vector3(1.2f, 1f, 1.2f), new Color(0.3f, 0.85f, 0.75f));

            Transform chef = CreateChef(root.transform, new Vector3(0f, 1f, -2.5f));
            var host = chef.GetComponent<ChefAgentHost>();
            var so = new SerializedObject(host);
            so.FindProperty("stove").objectReferenceValue = stove;
            so.FindProperty("storage").objectReferenceValue = storage;
            so.FindProperty("client").objectReferenceValue = client;
            so.FindProperty("entrance").objectReferenceValue = entrance;
            so.FindProperty("breakSpot").objectReferenceValue = breakSpot;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = chef.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Chef] Demo scene saved to {ScenePath}. Press Play and watch the chef cook, serve and rest.");
        }

        private static Transform CreateZone(Transform parent, string name, PrimitiveType primitive, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            SetMaterial(go, color);
            AddLabel(parent, name, go.transform.position, scale.y, 30f);
            return go.transform;
        }

        private static Transform CreateChef(Transform parent, Vector3 pos)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Chef";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one;
            SetMaterial(go, new Color(0.92f, 0.92f, 0.9f));

            go.AddComponent<ChefAgentHost>();
            return go.transform;
        }

        private static void AddLabel(Transform parent, string text, Vector3 worldPosition, float zoneHeight, float tilt)
        {
            GameObject label = new GameObject("Label");
            label.transform.SetParent(parent, false);
            label.transform.position = worldPosition + Vector3.up * (zoneHeight * 0.5f + 1.1f);
            label.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            label.transform.localScale = Vector3.one * 0.12f;

            var tm = label.AddComponent<TextMeshPro>();
            tm.font = TMP_Settings.defaultFontAsset;
            tm.text = text;
            tm.fontSize = 32;
            tm.alignment = TextAlignmentOptions.Center;
            tm.overflowMode = TextOverflowModes.Overflow;
            tm.rectTransform.sizeDelta = new Vector2(20f, 5f);
#if UNITY_6000_0_OR_NEWER
            tm.textWrappingMode = TextWrappingModes.NoWrap;
#else
            tm.enableWordWrapping = false;
#endif
            tm.color = Color.white;
            tm.fontStyle = FontStyles.Bold;
            tm.outlineWidth = 0.12f;
            tm.outlineColor = Color.black;
        }

        private static void CreateCamera(Vector3 position, Quaternion rotation, float orthographicSize)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetPositionAndRotation(position, rotation);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
            go.AddComponent<AudioListener>();
        }

        private static void SetMaterial(GameObject go, Color color)
        {
            Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            if (shader == null) return;

            var mat = new Material(shader);
            mat.color = color;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = mat;
        }
    }
}
