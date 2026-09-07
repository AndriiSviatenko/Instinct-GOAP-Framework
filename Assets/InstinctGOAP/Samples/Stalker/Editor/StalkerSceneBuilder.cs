using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Instinct.GOAP.Samples.Stalker.EditorTools
{

    public static class StalkerSceneBuilder
    {
        private const string ScenePath = "Assets/InstinctGOAP/Samples/Stalker/Stalker_ALife_Demo.unity";

        [MenuItem("GOAP/Samples/Stalker/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(new Vector3(0f, 18f, -18f), Quaternion.Euler(45f, 0f, 0f), 10f);

            var root = new GameObject("Stalker A-Life Demo");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localScale = new Vector3(2.4f, 1f, 2.4f);
            SetMaterial(ground, new Color(0.35f, 0.42f, 0.3f));

            Transform campfire = CreateZone(root.transform, "Campfire", new Vector3(0f, 0f, 0f), new Color(1f, 0.55f, 0.15f));
            Transform trader = CreateZone(root.transform, "Trader", new Vector3(6f, 0f, 0f), new Color(0.25f, 1f, 0.4f));
            Transform stash = CreateZone(root.transform, "Stash", new Vector3(-6f, 0f, 0f), new Color(0.85f, 0.7f, 0.15f));
            Transform shelter = CreateZone(root.transform, "Shelter", new Vector3(0f, 0f, 6f), new Color(0.3f, 0.7f, 1f));
            Transform anomaly = CreateZone(root.transform, "Anomaly", new Vector3(0f, 0f, -8f), new Color(1f, 0.25f, 1f));

            Transform[] patrol =
            {
                CreatePatrolPoint(root.transform, "Patrol 1", new Vector3(4f, 0f, 4f)),
                CreatePatrolPoint(root.transform, "Patrol 2", new Vector3(-4f, 0f, 4f)),
                CreatePatrolPoint(root.transform, "Patrol 3", new Vector3(-4f, 0f, -4f)),
                CreatePatrolPoint(root.transform, "Patrol 4", new Vector3(4f, 0f, -4f)),
            };

            Transform stalker = CreateStalker(root.transform, new Vector3(2.5f, 0f, 2.5f));
            var host = stalker.GetComponent<StalkerAgentHost>();
            var so = new SerializedObject(host);
            so.FindProperty("campfire").objectReferenceValue = campfire;
            so.FindProperty("trader").objectReferenceValue = trader;
            so.FindProperty("stash").objectReferenceValue = stash;
            so.FindProperty("shelter").objectReferenceValue = shelter;
            so.FindProperty("anomaly").objectReferenceValue = anomaly;

            var patrolProp = so.FindProperty("patrolPoints");
            patrolProp.arraySize = patrol.Length;
            for (int i = 0; i < patrol.Length; i++)
                patrolProp.GetArrayElementAtIndex(i).objectReferenceValue = patrol[i];

            so.FindProperty("enemy").objectReferenceValue =
                CreateMarker(root.transform, "Enemy", new Vector3(6.5f, 0.25f, 2.5f), new Color(1f, 0.15f, 0.15f));
            so.FindProperty("mutant").objectReferenceValue =
                CreateMarker(root.transform, "Mutant", new Vector3(-6.5f, 0.25f, -2.5f), new Color(0.55f, 0.1f, 0.6f));

            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = stalker.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Stalker] Demo scene saved to {ScenePath}. Press Play and watch the stalker live its A-Life.");
        }

        private static Transform CreateZone(Transform parent, string name, Vector3 pos, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(1.8f, 0.08f, 1.8f);
            SetMaterial(go, color);
            AddLabel(parent, name, go.transform.position, go.transform.localScale.y);
            return go.transform;
        }

        private static Transform CreatePatrolPoint(Transform parent, string name, Vector3 pos)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(0.5f, 0.15f, 0.5f);
            SetMaterial(go, new Color(0.6f, 0.85f, 0.35f));
            AddLabel(parent, name, go.transform.position, go.transform.localScale.y);
            return go.transform;
        }

        private static Transform CreateMarker(Transform parent, string name, Vector3 pos, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            SetMaterial(go, color);
            AddLabel(parent, name, go.transform.position, go.transform.localScale.y);
            return go.transform;
        }

        private static Transform CreateStalker(Transform parent, Vector3 pos)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Stalker";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            SetMaterial(go, new Color(0.75f, 0.75f, 0.72f));

            var host = go.AddComponent<StalkerAgentHost>();
            var rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            return go.transform;
        }

        private static void AddLabel(Transform parent, string text, Vector3 worldPosition, float zoneHeight)
        {
            GameObject label = new GameObject("Label");
            label.transform.SetParent(parent, false);
            label.transform.position = worldPosition + Vector3.up * (zoneHeight * 0.5f + 1.1f);
            label.transform.localRotation = Quaternion.Euler(40f, 0f, 0f);
            label.transform.localScale = Vector3.one * 0.12f;

            var tm = label.AddComponent<TextMeshPro>();
            tm.font = TMP_Settings.defaultFontAsset;
            tm.text = text;
            tm.fontSize = 30;
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
