using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using Unity.EditorCoroutines.Editor;

namespace Game.Ed
{
	public class IconMakerWindow : EditorWindow
	{
		[SerializeField] private string iconsRootPath;
		[SerializeField] private int iconSize = 128;
		[SerializeField] private LayerMask renderMask = -1;

		[SerializeField] private int antiAliasing = 1; // 2,4,8
		[SerializeField] private bool markTextureNonReadable = true;
		[SerializeField] private Vector3 camDirection = new Vector3(-1, -0.7f, -1);
		[SerializeField] private Vector3 camOffset = Vector3.zero;
		[SerializeField] private float padding = 0.01f;
		[SerializeField] private Color backColor = new Color(0.15f, 0.15f, 0.15f, 1f);
		[SerializeField] private bool orthographic = false;

		[SerializeField] private bool useLight = true;
		[SerializeField] private Color lightColor = new Color(1f, 0.9568627f, 0.8392157f, 1f);
		[SerializeField] private float lightIntensity = 1f;
		[SerializeField] private LightShadows lightShadows = LightShadows.None;

		[SerializeField] private bool useFloor = false;
		[SerializeField] private Color floorColor = new Color(0.15f, 0.15f, 0.15f, 1f);
		[SerializeField] private Vector3 floorSize = Vector3.one;
		[SerializeField] private Vector3 floorPos = Vector3.zero;

		[SerializeField] private bool commonSettingsFold = true;
		[SerializeField] private bool settingsFold = true;
		[SerializeField] private int previewSize = 128;

		[System.NonSerialized] private Vector3[] boundingBoxPoints = new Vector3[8];
		[System.NonSerialized] private List<Renderer> renderersList = new List<Renderer>(64);
		[System.NonSerialized] private List<Light> lightsList = new List<Light>(64);
		[System.NonSerialized] private List<ObjData> objects = new List<ObjData>();

		[System.NonSerialized] private Camera renderCam;
		[System.NonSerialized] private Transform renderCamTr;
		[System.NonSerialized] private Vector3 renderCamPos;
		[System.NonSerialized] private Light renderLight;
		[System.NonSerialized] private Transform floorTr;
		[System.NonSerialized] private Material floorMat;
		[System.NonSerialized] private GameObject renderObj;
		[System.NonSerialized] private Bounds renderObjBounds;
		[System.NonSerialized] private int genIconIdx = -1;

		public class ObjData
		{
			public GameObject prefab;
			public Texture2D texture;
		}

		// ------------------------------------------------------------------------------------------------------------
		#region system

		[MenuItem("Window/Icon Maker")]
		public static void Open()
		{
			GetWindow<IconMakerWindow>("Icon Maker");
		}

		protected void OnEnable()
		{
			LoadSettings();

			if (string.IsNullOrWhiteSpace(iconsRootPath))
			{
				iconsRootPath = $"{Application.dataPath}/_temp/";
			}

			if (objects.Count == 0 || objects[^1].prefab != null)
			{
				objects.Add(new ObjData());
			}
		}

		protected void OnDisable()
		{
			Dispose();
		}

		protected void OnDestroy()
		{
			Dispose();
		}

		private void LoadSettings()
		{
			var projectName = Application.dataPath.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries)[^2];
			iconsRootPath = EditorPrefs.GetString($"IconMaker.iconsRootPath.{projectName}", iconsRootPath);
			iconSize = EditorPrefs.GetInt($"IconMaker.iconSize.{projectName}", iconSize);
			renderMask = EditorPrefs.GetInt($"IconMaker.renderMask.{projectName}", renderMask);
			antiAliasing = EditorPrefs.GetInt($"IconMaker.antiAliasing.{projectName}", antiAliasing);
			markTextureNonReadable = EditorPrefs.GetBool($"IconMaker.markTextureNonReadable.{projectName}", markTextureNonReadable);
			camDirection = EdPrefsGetVector($"IconMaker.camDirection.{projectName}", camDirection);
			camOffset = EdPrefsGetVector($"IconMaker.camOffset.{projectName}", camOffset);
			padding = EditorPrefs.GetFloat($"IconMaker.padding.{projectName}", padding);
			backColor = EdPrefsGetVector($"IconMaker.backColor.{projectName}", backColor);
			orthographic = EditorPrefs.GetBool($"IconMaker.orthographic.{projectName}", orthographic);
			useLight = EditorPrefs.GetBool($"IconMaker.useLight.{projectName}", useLight);
			lightColor = EdPrefsGetVector($"IconMaker.lightColor.{projectName}", lightColor);
			lightIntensity = EditorPrefs.GetFloat($"IconMaker.lightIntensity.{projectName}", lightIntensity);
			lightShadows = (LightShadows)EditorPrefs.GetInt($"IconMaker.lightShadows.{projectName}", (int)lightShadows);
			useFloor = EditorPrefs.GetBool($"IconMaker.useFloor.{projectName}", useFloor);
			floorColor = EdPrefsGetVector($"IconMaker.floorColor.{projectName}", floorColor);
			floorSize = EdPrefsGetVector($"IconMaker.floorSize.{projectName}", floorSize);
			floorPos = EdPrefsGetVector($"IconMaker.floorY.{projectName}", floorPos);
			previewSize = EditorPrefs.GetInt($"IconMaker.previewSize.{projectName}", previewSize);
		}

		private void SaveSettings()
		{
			var projectName = Application.dataPath.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries)[^2];
			EditorPrefs.SetString($"IconMaker.iconsRootPath.{projectName}", iconsRootPath);
			EditorPrefs.SetInt($"IconMaker.iconSize.{projectName}", iconSize);
			EditorPrefs.SetInt($"IconMaker.renderMask.{projectName}", renderMask);
			EditorPrefs.SetInt($"IconMaker.antiAliasing.{projectName}", antiAliasing);
			EditorPrefs.SetBool($"IconMaker.markTextureNonReadable.{projectName}", markTextureNonReadable);
			EdPrefsSetVector($"IconMaker.camDirection.{projectName}", camDirection);
			EdPrefsSetVector($"IconMaker.camOffset.{projectName}", camOffset);
			EditorPrefs.SetFloat($"IconMaker.padding.{projectName}", padding);
			EdPrefsSetVector($"IconMaker.backColor.{projectName}", backColor);
			EditorPrefs.SetBool($"IconMaker.orthographic.{projectName}", orthographic);
			EditorPrefs.SetBool($"IconMaker.useLight.{projectName}", useLight);
			EdPrefsSetVector($"IconMaker.lightColor.{projectName}", lightColor);
			EditorPrefs.SetFloat($"IconMaker.lightIntensity.{projectName}", lightIntensity);
			EditorPrefs.SetInt($"IconMaker.lightShadows.{projectName}", (int)lightShadows);
			EditorPrefs.SetBool($"IconMaker.useFloor.{projectName}", useFloor);
			EdPrefsSetVector($"IconMaker.floorColor.{projectName}", floorColor);
			EdPrefsSetVector($"IconMaker.floorSize.{projectName}", floorSize);
			EdPrefsSetVector($"IconMaker.floorY.{projectName}", floorPos);
			EditorPrefs.SetInt($"IconMaker.previewSize.{projectName}", previewSize);
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region ui

		protected void OnGUI()
		{
			EditorGUIUtility.wideMode = true;

			EditorGUILayout.Space();
			GUILayout.BeginHorizontal();
			{
				GUILayout.Label($"Dest Folder: {iconsRootPath}");
				if (GUILayout.Button("Browse", GUILayout.Width(120)))
				{
					iconsRootPath = EditorUtility.OpenFolderPanel("Destination Folder", iconsRootPath, null);
				}
			}
			GUILayout.EndHorizontal();

			commonSettingsFold = EditorGUILayout.Foldout(commonSettingsFold, "Common");
			if (commonSettingsFold)
			{
				EditorGUI.indentLevel++;
				iconSize = EditorGUILayout.IntField("Icon Size", iconSize);
				markTextureNonReadable = EditorGUILayout.Toggle("Make NonReadable", markTextureNonReadable);
				antiAliasing = EditorGUILayout.IntField("Antiliasing", antiAliasing);
				backColor = EditorGUILayout.ColorField("Background Colour", backColor);
				orthographic = EditorGUILayout.Toggle("Orthograpic", orthographic);
				useLight = EditorGUILayout.Toggle("Use Light", useLight);
				if (useLight)
				{
					EditorGUI.indentLevel++;
					lightColor = EditorGUILayout.ColorField("Colour", lightColor);
					lightIntensity = EditorGUILayout.FloatField("Intensity", lightIntensity);
					lightShadows = (LightShadows)EditorGUILayout.EnumPopup("Shadow", lightShadows);
					EditorGUI.indentLevel--;
				}
				EditorGUI.indentLevel--;
			}

			settingsFold = EditorGUILayout.Foldout(settingsFold, "Settings");
			if (settingsFold)
			{
				EditorGUI.indentLevel++;
				previewSize = EditorGUILayout.IntField("Preview Size", previewSize);
				renderMask = LayerMaskDrawer.LayerMaskField("Render Mask", renderMask);

				EditorGUI.BeginChangeCheck();
				camDirection = EditorGUILayout.Vector3Field("Direction", camDirection);
				camOffset = EditorGUILayout.Vector3Field("Offset", camOffset);
				padding = EditorGUILayout.FloatField("Padding", padding);
				if (EditorGUI.EndChangeCheck() && renderCamTr != null && renderObj != null)
				{
					CalculateCameraPosition();
				}

				useFloor = EditorGUILayout.Toggle("Floor", useFloor);
				if (useFloor)
				{
					EditorGUI.indentLevel++;
					EditorGUI.BeginChangeCheck();
					floorColor = EditorGUILayout.ColorField("Colour", floorColor);
					floorSize = EditorGUILayout.Vector3Field("Size", floorSize);
					floorPos = EditorGUILayout.Vector3Field("Offset", floorPos);
					if (EditorGUI.EndChangeCheck() && floorTr != null)
					{
						floorTr.position = floorPos;
						floorTr.localScale = floorSize;
						if (floorMat != null) floorMat.SetColor("_Color", floorColor);
					}

					EditorGUI.indentLevel--;
				}
				EditorGUI.indentLevel--;
			}

			EditorGUILayout.Space();
			GUILayout.Label("Note: This will create objects in open currently open scene.");
			GUILayout.BeginHorizontal();
			{
				if (GUILayout.Button("Add from Folder", GUILayout.Width(120)))
				{
					var path = EditorUtility.OpenFolderPanel("Import from", Application.dataPath, null);
					if (!string.IsNullOrWhiteSpace(path))
					{
						var guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" + path.Replace(Application.dataPath, "") });
						foreach (var guid in guids)
						{
							var fab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
							if (fab != null && objects.Find(a => a.prefab == fab) == null)
							{
								objects.Add(new ObjData { prefab = fab, texture = null });
							}
						}
					}
				}
				if (GUILayout.Button("Clear", GUILayout.Width(60)))
				{
					genIconIdx = -1;
					objects.Clear();
					objects.Add(new ObjData());
				}

				GUILayout.FlexibleSpace();
				GUILayout.Label("Generate:");
				if (GUILayout.Button("Refresh", GUILayout.Width(60)))
				{
					if (genIconIdx < 0 || genIconIdx >= objects.Count - 1) genIconIdx = 0;
					GenerateIcons();
				}
				if (GUILayout.Button("Prev", GUILayout.Width(50)))
				{
					genIconIdx--; if (genIconIdx < 0) genIconIdx = Mathf.Max(0, objects.Count - 2);
					GenerateIcons();
				}
				if (GUILayout.Button("First", GUILayout.Width(50)))
				{
					genIconIdx = 0;
					GenerateIcons();
				}
				if (GUILayout.Button("Next", GUILayout.Width(50)))
				{
					genIconIdx++; if (genIconIdx >= objects.Count - 1) genIconIdx = 0;
					GenerateIcons();
				}
				if (GUILayout.Button("All", GUILayout.Width(50)))
				{
					genIconIdx = -1;
					GenerateIcons();
				}
			}
			GUILayout.EndHorizontal();

			EditorGUILayout.Space();

			var mainRect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			Rect rect = new Rect(mainRect.x + 5, mainRect.y, previewSize, previewSize);
			var toRemove = new List<int>();
			for (int i = 0; i < objects.Count; i++)
			{
				if (i < objects.Count - 1 && objects[i].prefab == null)
				{
					toRemove.Add(i);
				}

				if (objects[i] != null)
				{
					DrawObject(rect, objects[i], i);
				}

				rect.x += previewSize + 5;
				if (rect.x + previewSize > mainRect.xMax)
				{
					rect.x = mainRect.x + 5;
					rect.y += previewSize + 5 + EditorGUIUtility.singleLineHeight;
				}
			}

			// remove empty entries
			for (int i = toRemove.Count - 1; i >= 0; i--)
			{
				objects.RemoveAt(i);
			}

			// last is always a new empty one that can be used for new object
			if (objects.Count == 0 || objects[^1].prefab != null)
			{
				objects.Add(new ObjData());
			}
		}

		private void DrawObject(Rect rect, ObjData obj, int idx)
		{
			if (genIconIdx == idx)
			{
				EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), Color.yellow);
			}

			rect.height = previewSize;
			if (obj.texture == null)
			{
				EditorGUI.DrawRect(rect, Color.black);
			}
			else
			{
				EditorGUI.DrawPreviewTexture(rect, obj.texture);
			}

			rect.y += previewSize;
			rect.height = EditorGUIUtility.singleLineHeight;
			obj.prefab = EditorGUI.ObjectField(rect, obj.prefab, typeof(GameObject), allowSceneObjects: false) as GameObject;
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region init/close

		private void Initialize()
		{
			if (string.IsNullOrWhiteSpace(iconsRootPath))
			{
				iconsRootPath = $"{Application.dataPath}/_temp/";
			}

			if (renderCam == null)
			{
				renderCam = new GameObject("[MapEdIconsGenCamera]").AddComponent<Camera>();
				renderCam.gameObject.tag = "EditorOnly";
				renderCam.fieldOfView = 45;
				renderCam.aspect = (float)iconSize / iconSize;
				renderCam.clearFlags = CameraClearFlags.Color;
				renderCam.backgroundColor = backColor;
				renderCam.cullingMask = renderMask;
				renderCam.depth = -999;
				renderCam.nearClipPlane = 0.01f;
				renderCam.orthographic = orthographic;
				renderCamTr = renderCam.transform;

				if (useLight)
				{   // attach light component to camera gameobject
					renderLight = renderCam.gameObject.AddComponent<Light>();
					renderLight.type = LightType.Directional;
					renderLight.cullingMask = renderMask;
					renderLight.color = lightColor;
					renderLight.intensity = lightIntensity;
					renderLight.shadows = lightShadows;
				}
			}

			if (useFloor && floorTr == null)
			{
				floorTr = GameObject.CreatePrimitive(PrimitiveType.Plane).transform;
				floorTr.gameObject.tag = "EditorOnly";
				floorTr.transform.position = floorPos;
				floorTr.transform.localScale = floorSize;

				var ren = floorTr.GetComponent<MeshRenderer>();
				floorMat = Instantiate(ren.sharedMaterial);
				floorMat.name = "temp-mat";
				floorMat.hideFlags = HideFlags.HideAndDontSave;
				floorMat.SetColor("_Color", floorColor);
				ren.sharedMaterials = new[] { floorMat };
			}
		}

		private void Dispose()
		{
			if (renderObj != null)
			{
				DestroyImmediate(renderObj);
				renderObj = null;
			}

			if (floorTr != null)
			{
				DestroyImmediate(floorTr.gameObject);
				floorTr = null;
			}

			if (floorMat != null)
			{
				DestroyImmediate(floorMat);
				floorMat = null;
			}

			if (renderCam != null)
			{
				DestroyImmediate(renderCam.gameObject);
				renderCam = null;
				renderCamTr = null;
			}
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region icon gen

		private void GenerateIcons()
		{
			SaveSettings();

			// make sure the save/load dir exists
			if (string.IsNullOrWhiteSpace(iconsRootPath))
			{
				Debug.LogError("Invalid destination path.");
				return;
			}

			try { Directory.CreateDirectory(iconsRootPath); }
			catch (System.Exception ex)
			{
				Debug.LogError($"Invalid destination path: {iconsRootPath}");
				Debug.LogException(ex);
				return;
			}

			Dispose();
			Initialize();

			// a single?
			if (genIconIdx >= 0)
			{
				var obj = objects[genIconIdx];
				if (obj.prefab != null)
				{
					var path = Path.Combine(iconsRootPath, $"icon-{obj.prefab.name}.png");
					obj.texture = GenerateIcon(path, obj.prefab, false);
				}
			}

			// else all
			else
			{
				foreach (var obj in objects)
				{
					if (renderCam == null)
					{   // in case it was destroyed/Dispose was called before this method completed
						Debug.LogError("Generator interrupted.");
						break;
					}

					if (obj.prefab != null)
					{
						var path = Path.Combine(iconsRootPath, $"icon-{obj.prefab.name}.png");
						obj.texture = GenerateIcon(path, obj.prefab, true);
					}
				}

				Dispose();
			}

			AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
		}

		private Texture2D LoadIcon(string path)
		{
			try
			{
				if (File.Exists(path))
				{
					var bytes = File.ReadAllBytes(path);
					var texture = new Texture2D(iconSize, iconSize, TextureFormat.RGB24, false);
					texture.LoadImage(bytes, markTextureNonReadable);
					if (texture != null) texture.hideFlags = HideFlags.HideAndDontSave;
					return texture;
				}
			}
			catch { }
			return null;
		}

		private void SaveIcon(string path, Texture2D texture)
		{
			if (texture == null) return;

			try
			{
				var bytes = texture.EncodeToPNG();
				File.WriteAllBytes(path, bytes);
			}
			catch (System.Exception ex)
			{
				Debug.LogException(ex);
			}
		}

		private Texture2D GenerateIcon(string savePath, GameObject prefab, bool destroyWhenDone)
		{
			Texture2D result = null;

			if (renderObj != null)
			{
				DestroyImmediate(renderObj);
				renderObj = null;
			}

			renderObj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
			renderObj.tag = "EditorOnly";

			// disable lights in object
			renderObj.GetComponentsInChildren(lightsList);
			foreach (var l in lightsList) l.enabled = false;
			lightsList.Clear();

			// position camera
			if (!CalculateBounds())
			{
				Debug.LogError("Could not calculate bounds for object.");
				DestroyImmediate(renderObj);
				renderObj = null;
				return null;
			}

			CalculateCameraPosition();

			// render
			RenderTexture activeRT = RenderTexture.active;
			RenderTexture renderTexture = null;

			try
			{
				renderTexture = RenderTexture.GetTemporary(iconSize, iconSize);
				renderTexture.antiAliasing = antiAliasing;

				RenderTexture.active = renderTexture;
				renderCam.targetTexture = renderTexture;
				renderCam.Render();

				result = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
				result.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0, false);

				SaveIcon(savePath, result);

				result.Apply(false, markTextureNonReadable);
			}
			finally
			{
				RenderTexture.active = activeRT;
				renderCam.targetTexture = null;
				if (renderTexture != null)
				{
					RenderTexture.ReleaseTemporary(renderTexture);
				}
			}

			// done with the temporary object
			if (destroyWhenDone)
			{
				DestroyImmediate(renderObj);
				renderObj = null;
			}

			return result;
		}

		private void CalculateCameraPosition()
		{
			var targetObjTr = renderObj.transform;
			var r = targetObjTr.rotation * camDirection;
			renderCamTr.rotation = r == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(r, targetObjTr.up);

			Bounds bounds = renderObjBounds;
			Vector3 cameraDirection = renderCamTr.forward;
			float aspect = renderCam.aspect;

			if (padding != 0f)
			{
				bounds.size *= 1f + padding * 2f; // Padding applied to both edges, hence multiplied by 2
			}

			Vector3 boundsCenter = bounds.center;
			Vector3 boundsExtents = bounds.extents;
			Vector3 boundsSize = 2f * boundsExtents;

			// Calculate corner points of the Bounds
			Vector3 point = boundsCenter + boundsExtents;
			boundingBoxPoints[0] = point;
			point.x -= boundsSize.x;
			boundingBoxPoints[1] = point;
			point.y -= boundsSize.y;
			boundingBoxPoints[2] = point;
			point.x += boundsSize.x;
			boundingBoxPoints[3] = point;
			point.z -= boundsSize.z;
			boundingBoxPoints[4] = point;
			point.x -= boundsSize.x;
			boundingBoxPoints[5] = point;
			point.y += boundsSize.y;
			boundingBoxPoints[6] = point;
			point.x += boundsSize.x;
			boundingBoxPoints[7] = point;

			if (renderCam.orthographic)
			{
				renderCamPos = boundsCenter; //renderCamTr.position = boundsCenter;

				float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
				float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;

				for (int i = 0; i < boundingBoxPoints.Length; i++)
				{
					Vector3 localPoint = renderCamTr.InverseTransformPoint(boundingBoxPoints[i]);
					if (localPoint.x < minX) minX = localPoint.x;
					if (localPoint.x > maxX) maxX = localPoint.x;
					if (localPoint.y < minY) minY = localPoint.y;
					if (localPoint.y > maxY) maxY = localPoint.y;
				}

				float distance = boundsExtents.magnitude + 1f;
				renderCam.orthographicSize = Mathf.Max(maxY - minY, (maxX - minX) / aspect) * 0.5f;
				renderCamPos = boundsCenter - cameraDirection * distance; // renderCamTr.position = boundsCenter - cameraDirection * distance;
			}
			else
			{
				Vector3 cameraUp = renderCamTr.up, cameraRight = renderCamTr.right;

				float verticalFOV = renderCam.fieldOfView * 0.5f;
				float horizontalFOV = Mathf.Atan(Mathf.Tan(verticalFOV * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;

				// Normals of the camera's frustum planes
				Vector3 topFrustumPlaneNormal = Quaternion.AngleAxis(90f + verticalFOV, -cameraRight) * cameraDirection;
				Vector3 bottomFrustumPlaneNormal = Quaternion.AngleAxis(90f + verticalFOV, cameraRight) * cameraDirection;
				Vector3 rightFrustumPlaneNormal = Quaternion.AngleAxis(90f + horizontalFOV, cameraUp) * cameraDirection;
				Vector3 leftFrustumPlaneNormal = Quaternion.AngleAxis(90f + horizontalFOV, -cameraUp) * cameraDirection;

				// Credit for algorithm: https://stackoverflow.com/a/66113254/2373034
				// 1. Find edge points of the bounds using the camera's frustum planes
				// 2. Create a plane for each edge point that goes through the point and has the corresponding frustum plane's normal
				// 3. Find the intersection line of horizontal edge points' planes (horizontalIntersection) and vertical edge points' planes (verticalIntersection)
				//    If we move the camera along horizontalIntersection, the bounds will always with the camera's width perfectly (similar effect goes for verticalIntersection)
				// 4. Find the closest line segment between these two lines (horizontalIntersection and verticalIntersection) and place the camera at the farthest point on that line
				int leftmostPoint = -1, rightmostPoint = -1, topmostPoint = -1, bottommostPoint = -1;
				for (int i = 0; i < boundingBoxPoints.Length; i++)
				{
					if (leftmostPoint < 0 && IsOutermostPointInDirection(i, leftFrustumPlaneNormal)) leftmostPoint = i;
					if (rightmostPoint < 0 && IsOutermostPointInDirection(i, rightFrustumPlaneNormal)) rightmostPoint = i;
					if (topmostPoint < 0 && IsOutermostPointInDirection(i, topFrustumPlaneNormal)) topmostPoint = i;
					if (bottommostPoint < 0 && IsOutermostPointInDirection(i, bottomFrustumPlaneNormal)) bottommostPoint = i;
				}

				Ray horizontalIntersection = GetPlanesIntersection(new Plane(leftFrustumPlaneNormal, boundingBoxPoints[leftmostPoint]), new Plane(rightFrustumPlaneNormal, boundingBoxPoints[rightmostPoint]));
				Ray verticalIntersection = GetPlanesIntersection(new Plane(topFrustumPlaneNormal, boundingBoxPoints[topmostPoint]), new Plane(bottomFrustumPlaneNormal, boundingBoxPoints[bottommostPoint]));

				FindClosestPointsOnTwoLines(horizontalIntersection, verticalIntersection, out Vector3 closestPoint1, out Vector3 closestPoint2);

				renderCamPos = Vector3.Dot(closestPoint1 - closestPoint2, cameraDirection) < 0 ? closestPoint1 : closestPoint2; // renderCamTr.position = Vector3.Dot(closestPoint1 - closestPoint2, cameraDirection) < 0 ? closestPoint1 : closestPoint2;
			}

			renderCamTr.position = renderCamPos + new Vector3(camOffset.x, camOffset.y, camOffset.z);
		}

		// Calculates AABB bounds of the target object (AABB will include its child objects)
		private bool CalculateBounds()
		{
			renderersList.Clear();
			renderObj.GetComponentsInChildren(renderersList);

			renderObjBounds = new Bounds();
			bool hasBounds = false;
			for (int i = 0; i < renderersList.Count; i++)
			{
				if (!renderersList[i].enabled)
				{
					continue;
				}

				if (renderMask != (renderMask | (1 << renderersList[i].gameObject.layer)))
				{
					continue;
				}

				if (!hasBounds)
				{
					renderObjBounds = renderersList[i].bounds;
					hasBounds = true;
				}
				else
				{
					renderObjBounds.Encapsulate(renderersList[i].bounds);
				}

				if (floorTr != null)
				{   // set floor to a layer that will be drawn while layer is known
					floorTr.gameObject.layer = renderersList[i].gameObject.layer;
				}
			}

			return hasBounds;
		}

		// Returns whether or not the given point is the outermost point in the given direction among all points of the bounds
		private bool IsOutermostPointInDirection(int pointIndex, Vector3 direction)
		{
			Vector3 point = boundingBoxPoints[pointIndex];
			for (int i = 0; i < boundingBoxPoints.Length; i++)
			{
				if (i != pointIndex && Vector3.Dot(direction, boundingBoxPoints[i] - point) > 0)
					return false;
			}

			return true;
		}

		// Returns the intersection line of the 2 planes
		private Ray GetPlanesIntersection(Plane p1, Plane p2)
		{
			// Credit: https://stackoverflow.com/a/32410473/2373034
			Vector3 p3Normal = Vector3.Cross(p1.normal, p2.normal);
			float det = p3Normal.sqrMagnitude;

			return new Ray(((Vector3.Cross(p3Normal, p2.normal) * p1.distance) + (Vector3.Cross(p1.normal, p3Normal) * p2.distance)) / det, p3Normal);
		}

		// Returns the edge points of the closest line segment between 2 lines
		private void FindClosestPointsOnTwoLines(Ray line1, Ray line2, out Vector3 closestPointLine1, out Vector3 closestPointLine2)
		{
			// Credit: http://wiki.unity3d.com/index.php/3d_Math_functions
			Vector3 line1Direction = line1.direction;
			Vector3 line2Direction = line2.direction;

			float a = Vector3.Dot(line1Direction, line1Direction);
			float b = Vector3.Dot(line1Direction, line2Direction);
			float e = Vector3.Dot(line2Direction, line2Direction);

			float d = a * e - b * b;

			Vector3 r = line1.origin - line2.origin;
			float c = Vector3.Dot(line1Direction, r);
			float f = Vector3.Dot(line2Direction, r);

			float s = (b * f - c * e) / d;
			float t = (a * f - c * b) / d;

			closestPointLine1 = line1.origin + line1Direction * s;
			closestPointLine2 = line2.origin + line2Direction * t;
		}

		private void DeleteAllFiles(string path)
		{
			DirectoryInfo di = new DirectoryInfo(path);
			foreach (FileInfo file in di.GetFiles())
			{
				try { file.Delete(); } catch { }
			}
		}

		//private void SetLayerAndFlagsRecursively(Transform transform)
		//{
		//	transform.gameObject.layer = renderMask;
		//	GameObjectUtility.SetStaticEditorFlags(transform.gameObject, 0);
		//	int count = transform.childCount;
		//	for (int i = 0; i < count; i++)
		//	{
		//		SetLayerAndFlagsRecursively(transform.GetChild(i));
		//	}
		//}

		private static void EdPrefsSetVector(string key, Vector4 value)
		{
			EditorPrefs.SetString(key, VectorToString(value));
		}

		private static Vector4 EdPrefsGetVector(string key, Vector4 defaultValue)
		{
			var s = EditorPrefs.GetString(key, null);
			return StringToVector(s, defaultValue);
		}

		private static string VectorToString(Vector4 c)
		{
			return $"{c.x};{c.y};{c.z};{c.w}";
		}

		private static Vector4 StringToVector(string s, Vector4 defaultValue)
		{
			if (s != null)
			{
				var vals = s.Split(';');
				if (vals.Length == 4 &&
					float.TryParse(vals[0], out float x) &&
					float.TryParse(vals[1], out float y) &&
					float.TryParse(vals[2], out float z) &&
					float.TryParse(vals[3], out float w))
				{
					return new Vector4(x, y, z, w);
				}
			}
			return defaultValue;
		}

		#endregion
		// ===================================================================================================================
	}
}