using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Instinct.GOAP.Samples.Farmer.EditorTools
{

    public static class FarmerSceneBuilder
    {
        private const string ScenePath = "Assets/InstinctGOAP/Samples/Farmer/FarmerGoap.unity";

        [MenuItem("GOAP/Samples/Farmer/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(new Vector3(0f, 11f, -11f), Quaternion.Euler(45f, 0f, 0f), 7f);

            var root = new GameObject("Farmer Demo");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localScale = new Vector3(1.5f, 1f, 1.5f);
            SetMaterial(ground, new Color(0.42f, 0.45f, 0.3f));

            Transform home = CreateZone(root.transform, "Home", PrimitiveType.Cube,
                new Vector3(-4f, 0.75f, 0f), new Vector3(1.5f, 1.5f, 1.5f), new Color(0.8f, 0.45f, 0.2f));
            Transform field = CreateZone(root.transform, "Field", PrimitiveType.Cube,
                new Vector3(1.5f, 0.15f, 0f), new Vector3(4.5f, 0.3f, 2.8f), new Color(0.45f, 0.3f, 0.18f));

            CreateCrops(root.transform);

            Transform farmer = CreateFarmer(root.transform, new Vector3(0f, 1f, -3f));
            var host = farmer.GetComponent<FarmerHost>();
            var so = new SerializedObject(host);
            so.FindProperty("home").objectReferenceValue = home;
            so.FindProperty("field").objectReferenceValue = field;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = farmer.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Farmer] Demo scene saved to {ScenePath}. Press Play and watch the farmer work the ripening field.");
        }

        private static void CreateCrops(Transform parent)
        {
            var field = parent.Find("Field");
            if (field == null) return;

            float fieldTop = field.position.y + field.localScale.y * 0.5f;
            int index = 0;

            foreach (float x in new[] { 0.15f, 1.5f, 2.85f })
                foreach (float z in new[] { -0.7f, 0.7f })
                {
                    index++;
                    bool ripe = index <= 4;
                    float height = ripe ? 0.9f : 0.18f;

                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = $"Crop {index}";
                    go.transform.SetParent(parent, false);
                    go.transform.localScale = new Vector3(0.35f, height, 0.35f);
                    go.transform.localPosition = new Vector3(x, fieldTop + height * 0.5f, z);
                    SetMaterial(go, ripe ? new Color(0.95f, 0.78f, 0.25f) : new Color(0.2f, 0.5f, 0.22f));
                }
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

        private static Transform CreateFarmer(Transform parent, Vector3 pos)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Farmer";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one;
            SetMaterial(go, new Color(0.3f, 0.6f, 0.3f));
            go.AddComponent<FarmerHost>();
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
