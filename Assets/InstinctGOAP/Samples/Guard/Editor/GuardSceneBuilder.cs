using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Instinct.GOAP.Samples.Guard.EditorTools
{

    public static class GuardSceneBuilder
    {
        private const string ScenePath = "Assets/InstinctGOAP/Samples/Guard/Guard_Patrol_Demo.unity";

        [MenuItem("GOAP/Samples/Guard/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(new Vector3(0f, 15f, -15f), Quaternion.Euler(45f, 0f, 0f), 9.5f);

            var root = new GameObject("Guard Patrol Demo");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            SetMaterial(ground, new Color(0.3f, 0.42f, 0.28f));

            var waypoints = new List<Transform>
            {
                CreateWaypoint(root.transform, "WP 1", new Vector3(4f, 0.1f, 4f)),
                CreateWaypoint(root.transform, "WP 2", new Vector3(-4f, 0.1f, 4f)),
                CreateWaypoint(root.transform, "WP 3", new Vector3(-4f, 0.1f, -4f)),
                CreateWaypoint(root.transform, "WP 4", new Vector3(4f, 0.1f, -4f)),
            };

            CreateWall(root.transform, "Wall A", new Vector3(6.5f, 1f, -2f), new Vector3(4f, 2f, 0.5f));
            CreateWall(root.transform, "Wall B", new Vector3(-6f, 1f, -5.5f), new Vector3(0.5f, 2f, 5f));
            CreateWall(root.transform, "Wall C", new Vector3(1f, 1f, 7f), new Vector3(5f, 2f, 0.5f));

            Transform intruder = CreateMarker(root.transform, "Intruder", PrimitiveType.Sphere,
                new Vector3(7f, 0.5f, 0f), new Vector3(0.7f, 0.7f, 0.7f), new Color(1f, 0.2f, 0.2f));
            ReplaceColliderWithController(intruder.gameObject, 0.5f, 1f);

            Transform guard = CreateGuard(root.transform, new Vector3(4f, 1f, -4f));
            ReplaceColliderWithController(guard.gameObject, 0.5f, 2f);
            var host = guard.GetComponent<GuardAgentHost>();
            var so = new SerializedObject(host);
            so.FindProperty("intruder").objectReferenceValue = intruder;

            var waypointProp = so.FindProperty("waypoints");
            waypointProp.arraySize = waypoints.Count;
            for (int i = 0; i < waypoints.Count; i++)
                waypointProp.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = guard.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Guard] Demo scene saved to {ScenePath}. Press Play and watch the intruder skulk, flee and get caught.");
        }

        private static Transform CreateWaypoint(Transform parent, string name, Vector3 pos)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, 0.25f, pos.z);
            go.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            SetMaterial(go, new Color(0.6f, 0.85f, 0.35f));
            AddLabel(parent, name, go.transform.position, go.transform.localScale.y, 38f);
            return go.transform;
        }

        private static Transform CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            SetMaterial(go, new Color(0.5f, 0.5f, 0.54f));
            return go.transform;
        }

        private static Transform CreateMarker(Transform parent, string name, PrimitiveType primitive, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            SetMaterial(go, color);
            return go.transform;
        }

        private static Transform CreateGuard(Transform parent, Vector3 pos)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Guard";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one;
            SetMaterial(go, new Color(0.25f, 0.5f, 0.95f));
            go.AddComponent<GuardAgentHost>();
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

        private static void ReplaceColliderWithController(GameObject go, float radius, float height)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            var controller = go.AddComponent<CharacterController>();
            controller.radius = radius;
            controller.height = height;
            controller.center = Vector3.zero;
            controller.skinWidth = 0.05f;
            controller.minMoveDistance = 0f;
            controller.enableOverlapRecovery = true;
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
