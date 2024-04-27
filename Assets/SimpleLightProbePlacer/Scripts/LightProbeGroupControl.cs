using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;

namespace SimpleLightProbePlacer
{
	[RequireComponent(typeof(LightProbeGroup))]
	[AddComponentMenu("Rendering/Light Probe Group Control")]
	public class LightProbeGroupControl : MonoBehaviour
	{
		[SerializeField]
		float m_mergeDistance = 0.5f;
		[SerializeField]
		bool m_usePointLights = true;
		[SerializeField]
		float m_pointLightRange = 1;
		public float MergeDistance   { get => m_mergeDistance; set => m_mergeDistance = value; }
		public int   MergedProbes    => m_mergedProbes;
		public bool  UsePointLights  { get => m_usePointLights;  set => m_usePointLights = value; }
		public float PointLightRange { get => m_pointLightRange; set => m_pointLightRange = value; }
		public LightProbeGroup LightProbeGroup
		{
			get {
				if (m_lightProbeGroup != null) { return m_lightProbeGroup; }
				return m_lightProbeGroup = GetComponent<LightProbeGroup>();
			}
		}
		int             m_mergedProbes;
		LightProbeGroup m_lightProbeGroup;

		public void DeleteAll()
		{
			LightProbeGroup.probePositions = null;
			m_mergedProbes = 0;
		}

		public void Create()
		{
			DeleteAll();
			var positions = CreatePositions();
			positions.AddRange(CreateAroundPointLights(m_pointLightRange));
			positions = MergeClosestPositions(positions, m_mergeDistance, out m_mergedProbes);
			ApplyPositions(positions);
		}

		public void Merge()
		{
			if (LightProbeGroup.probePositions == null) { return; }
			var positions = MergeClosestPositions(LightProbeGroup.probePositions.ToList(), m_mergeDistance, out m_mergedProbes);
			positions = positions.Select(x => transform.TransformPoint(x)).ToList();
			ApplyPositions(positions);
		}

		void ApplyPositions(List<Vector3> positions)
		{
			LightProbeGroup.probePositions = positions.Select(x => transform.InverseTransformPoint(x)).ToArray();
		}

		static List<Vector3> CreatePositions()
		{
			var lightProbeVolumes = FindObjectsOfType<LightProbeVolume>();
			if (lightProbeVolumes.Length == 0) { return new List<Vector3>(); }
			var probes = new List<Vector3>();
			for (var i = 0; i < lightProbeVolumes.Length; i++) { probes.AddRange(lightProbeVolumes[i].GetPositions()); }
			return probes;
		}

		static List<Vector3> CreateAroundPointLights(float range)
		{
			var lights = FindObjectsOfType<Light>().Where(x => x.type == LightType.Point).ToList();
			if (lights.Count == 0) { return new List<Vector3>(); }
			var probes = new List<Vector3>();
			for (var i = 0; i < lights.Count; i++) { probes.AddRange(CreatePositionsAround(lights[i].transform, range)); }
			return probes;
		}

		static List<Vector3> MergeClosestPositions(List<Vector3> positions, float distance, out int mergedCount)
		{
			if (positions == null) {
				mergedCount = 0;
				return new List<Vector3>();
			}
			var exist = positions.Count;
			var done = false;
			while (!done) {
				var closest = new Dictionary<Vector3, List<Vector3>>();
				for (var i = 0; i < positions.Count; i++) {
					var points = positions.Where(x => (x - positions[i]).magnitude < distance).ToList();
					if (points.Count > 0 && !closest.ContainsKey(positions[i])) { closest.Add(positions[i], points); }
				}
				positions.Clear();
				var keys = closest.Keys.ToList();
				for (var i = 0; i < keys.Count; i++) {
					var center = closest[keys[i]].Aggregate(Vector3.zero, (result, target) => result + target) / closest[keys[i]].Count;
					if (!positions.Exists(x => x == center)) { positions.Add(center); }
				}
				done = positions.Select(x => positions.Where(y => y != x && (y - x).magnitude < distance)).All(x => !x.Any());
			}
			mergedCount = exist - positions.Count;
			return positions;
		}

		public static List<Vector3> CreatePositionsAround(Transform transform, float range)
		{
			Vector3[] corners = {
														new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, -0.5f),
														new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, -0.5f)
													};
			return corners.Select(x => transform.TransformPoint(x * range)).ToList();
		}
	}
	[CustomEditor(typeof(LightProbeGroupControl))]
	public class LightProbeGroupControlEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			var control = (LightProbeGroupControl) target;
			if (GUILayout.Button("Delete All Light Probes")) {
				Undo.RecordObject(control.LightProbeGroup, "Light Probe Group - delete all");
				control.DeleteAll();
			}
			if (control.LightProbeGroup != null) {
				var message = "Light Probes count: {0}\nMerged Probes: {1}";
				message = string.Format(message, control.LightProbeGroup.probePositions.Length, control.MergedProbes);
				EditorGUILayout.HelpBox(message, MessageType.Info);
			}
			if (GUILayout.Button("Create Light Probes")) {
				Undo.RecordObject(control.LightProbeGroup, "Light Probe Group - create");
				control.Create();
			}
			GUILayout.Space(10);
			if (GUILayout.Button("Merge Closest Light Probes")) {
				Undo.RecordObject(control.LightProbeGroup, "Light Probe Group - merge");
				control.Merge();
			}
			EditorGUI.BeginChangeCheck();
			var mergeDist = EditorGUILayout.Slider("Merge distance", control.MergeDistance, 0, 10);
			GUILayout.Space(20);
			EditorGUILayout.LabelField("Point Light Settings", EditorStyles.boldLabel);
			var useLights = EditorGUILayout.Toggle("Use Point Lights", control.UsePointLights);
			GUI.enabled = control.UsePointLights;
			var lightRange = EditorGUILayout.FloatField("Range", control.PointLightRange);
			GUI.enabled = true;
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(control, "Light Probe Group Control changes");
				control.MergeDistance = mergeDist;
				control.UsePointLights = useLights;
				control.PointLightRange = lightRange;
				EditorUtility.SetDirty(target);
			}
		}

		[MenuItem("GameObject/Light/Light Probe Group Control")]
		static void CreateLightProbeGroupControl(MenuCommand menuCommand)
		{
			var go = new GameObject("Light Probe Group Control");
			go.AddComponent<LightProbeGroupControl>();
			GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
			Undo.RegisterCreatedObjectUndo(go, "Create Light Probe Group Control");
			Selection.activeGameObject = go;
		}

		[DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy | GizmoType.Active)]
		static void DrawGizmoPointLight(Light light, GizmoType gizmoType)
		{
			var control = FindObjectOfType<LightProbeGroupControl>();
			if (control == null || !control.UsePointLights || light.type != LightType.Point) { return; }
			var probes = LightProbeGroupControl.CreatePositionsAround(light.transform, control.PointLightRange);
			for (var i = 0; i < probes.Count; i++) { Gizmos.DrawIcon(probes[i], "NONE", false); }
		}
	}
}
#endif