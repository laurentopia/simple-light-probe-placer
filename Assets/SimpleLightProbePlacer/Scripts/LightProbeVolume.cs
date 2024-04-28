using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;

namespace SimpleLightProbePlacer
{
	public enum LightProbeVolumeType { Fixed, Float }
	[AddComponentMenu("Rendering/Light Probe Volume")]
	public class LightProbeVolume : TransformVolume
	{
		[Tooltip("If you want to remove all probes within colliders, set this layer to the collider layer.")]
		public LayerMask layer;
		[SerializeField]
		[HideInInspector]
		LightProbeVolumeType m_type = LightProbeVolumeType.Fixed;
		[SerializeField]
		[HideInInspector]
		Vector3 m_densityFixed = Vector3.one;
		[SerializeField]
		[HideInInspector]
		Vector3 m_densityFloat = Vector3.one;
		static   List<Vector3>        _posList = new List<Vector3>();
		internal bool                 needsRefresh;
		public   LightProbeVolumeType Type { get => m_type; set => m_type = value; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		public static void Init()
		{
			_posList.Clear();
		}

		public Vector3 Density
		{
			get => m_type == LightProbeVolumeType.Fixed ? m_densityFixed : m_densityFloat;
			set {
				if (m_type == LightProbeVolumeType.Fixed) { m_densityFixed = value; } else { m_densityFloat = value; }
			}
		}
		public static Color EditorColor => new Color(1, 0, 0);

		public List<Vector3> GetPositions()
		{
			if (needsRefresh || _posList.Count == 0) {
				_posList = CreatePositions(m_type);
				needsRefresh = false;
				return _posList;
			} else { return _posList; }
		}

		public List<Vector3> CreatePositions(LightProbeVolumeType type) => type == LightProbeVolumeType.Fixed
			? CreatePositionsFixed(transform, Origin, Size, Density, layer)
			: CreatePositionsFloat(transform, Origin, Size, Density, layer);

		public static List<Vector3> CreatePositionsFixed(Transform volumeTransform, Vector3 origin, Vector3 size, Vector3 density, LayerMask layer)
		{
			var posList = new List<Vector3>();
			var offset = origin;
			var stepX = size.x / Mathf.FloorToInt(density.x);
			var stepY = size.y / Mathf.FloorToInt(density.y);
			var stepZ = size.z / Mathf.FloorToInt(density.z);
			offset -= size * 0.5f;
			for (var x = 0; x <= density.x; x++) {
				for (var y = 0; y <= density.y; y++) {
					for (var z = 0; z <= density.z; z++) {
						var probePos = offset + new Vector3(x * stepX, y * stepY, z * stepZ);
						if (IsInsideGeometry(ref probePos, density, layer)) { continue; }
						probePos = volumeTransform.TransformPoint(probePos);
						posList.Add(probePos);
					}
				}
			}
			return posList;
		}

		public static List<Vector3> CreatePositionsFloat(Transform volumeTransform, Vector3 origin, Vector3 size, Vector3 density, LayerMask layer)
		{
			var posList = new List<Vector3>();
			var offset = origin;
			var stepX = Mathf.FloorToInt(size.x / density.x);
			var stepY = Mathf.FloorToInt(size.y / density.y);
			var stepZ = Mathf.FloorToInt(size.z / density.z);
			offset -= size * 0.5f;
			offset.x += (size.x - stepX * density.x) * 0.5f;
			offset.y += (size.y - stepY * density.y) * 0.5f;
			offset.z += (size.z - stepZ * density.z) * 0.5f;
			for (var x = 0; x <= stepX; x++) {
				for (var y = 0; y <= stepY; y++) {
					for (var z = 0; z <= stepZ; z++) {
						var probePos = offset + new Vector3(x * density.x, y * density.y, z * density.z);
						if (IsInsideGeometry(ref probePos, density, layer)) { continue; }
						probePos = volumeTransform.TransformPoint(probePos);
						posList.Add(probePos);
					}
				}
			}
			return posList;
		}

		internal static Vector3[] _wiggleDirections = new[] {Vector3.up, Vector3.back, Vector3.forward, Vector3.left, Vector3.right};

		public static bool IsInsideGeometry(ref Vector3 position, Vector3 density, LayerMask layer)
		{
			var tmp = Physics.queriesHitBackfaces;
			Physics.queriesHitBackfaces = true;
			bool hitting;
			hitting = Physics.OverlapSphere(position, .01f, layer).Length > 0;
			if (hitting) {
				for (var i = 0; i < 5; i++) {
					var wiggledPosition = position + _wiggleDirections[i] * density.magnitude * .5f;
					if (Physics.OverlapSphere(wiggledPosition, .01f, layer).Length == 0) {
						var direction = (position - wiggledPosition).normalized;
						Physics.Raycast(wiggledPosition, direction, out var hit, density.magnitude, layer);
						position = hit.point - direction * .1f;
						hitting = false;
						break;
					}
				}
			}
			Physics.queriesHitBackfaces = tmp;
			return hitting;
		}
	}
	[CanEditMultipleObjects]
	[CustomEditor(typeof(LightProbeVolume))]
	public class LightProbeVolumeEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();
			var volume = (LightProbeVolume) target;
			EditorGUI.BeginChangeCheck();
			GUILayout.Space(10);
			EditorGUILayout.LabelField("Volume", EditorStyles.boldLabel);
			var origin = EditorGUILayout.Vector3Field("Origin", volume.Origin);
			var size = EditorGUILayout.Vector3Field("Size", volume.Size);
			GUILayout.Space(10);
			EditorGUILayout.LabelField("Density", EditorStyles.boldLabel);
			var   type = (LightProbeVolumeType) EditorGUILayout.EnumPopup("Density Type", volume.Type);
			var   densityMin = volume.Type == LightProbeVolumeType.Fixed ? 1 : 0.1f;
			float densityMax = volume.Type == LightProbeVolumeType.Fixed ? 100 : 50;
			var   density = volume.Density;
			density.x = EditorGUILayout.Slider("DensityX", volume.Density.x, densityMin, densityMax);
			density.y = EditorGUILayout.Slider("DensityY", volume.Density.y, densityMin, densityMax);
			density.z = EditorGUILayout.Slider("DensityZ", volume.Density.z, densityMin, densityMax);
			if (EditorGUI.EndChangeCheck() || volume.transform.hasChanged) {
				Undo.RecordObject(target, "Light Probe Volume changes");
				volume.Density = density;
				volume.Type = type;
				volume.Volume = new Volume(origin, size);
				volume.needsRefresh = true;
				EditorUtility.SetDirty(target);
			}
		}

		void OnSceneGUI()
		{
			var lightProbeVolume = (LightProbeVolume) target;
			var volume = TransformVolume.EditorVolumeControl(lightProbeVolume, 0.1f, LightProbeVolume.EditorColor);
			if (volume != lightProbeVolume.Volume) {
				Undo.RecordObject(target, "Light Probe Volume changes");
				// lightProbeVolume.needsRefresh = true;
				lightProbeVolume.Volume = volume;
				EditorUtility.SetDirty(target);
			}
		}

		[DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy | GizmoType.Active)]
		static void DrawGizmoVolume(LightProbeVolume volume, GizmoType gizmoType)
		{
			var color = LightProbeVolume.EditorColor;
			Gizmos.color = color;
			Gizmos.matrix = Matrix4x4.TRS(volume.transform.position, volume.transform.rotation, Vector3.one);
			Gizmos.DrawWireCube(volume.Origin, volume.Size);
			if (gizmoType != (GizmoType.Selected | GizmoType.InSelectionHierarchy | GizmoType.Active)) { return; }
			color.a = 0.25f;
			Gizmos.color = color;
			Gizmos.DrawCube(volume.Origin, volume.Size);
			var probes = volume.GetPositions();
			Gizmos.color = color;
			for (var i = 0; i < probes.Count; i++) { Gizmos.DrawIcon(probes[i], "NONE", false); }
		}

		[MenuItem("GameObject/Light/Light Probe Volume")]
		static void CreateLightProbeVolume(MenuCommand menuCommand)
		{
			var go = new GameObject("Light Probe Volume");
			go.AddComponent<LightProbeVolume>();
			GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
			Undo.RegisterCreatedObjectUndo(go, "Create Light Probe Volume");
			Selection.activeGameObject = go;
		}
	}
}
#endif