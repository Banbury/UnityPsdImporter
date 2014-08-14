/*
The MIT License (MIT)

Copyright (c) 2013 Banbury

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Reflection;
using PhotoshopFile;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using PivotPos = kontrabida.psdexport.PSDExporter.PivotPos;

namespace kontrabida.psdexport
{
	public class PSDEditorWindow : EditorWindow
	{
		#region Static/Menus
		private static PSDEditorWindow GetPSDEditor()
		{
			var wnd = GetWindow<PSDEditorWindow>();
			wnd.title = "PSD Import";
			wnd.Show();
			return wnd;
		}

		[MenuItem("Sprites/PSD Import")]
		public static void ShowWindow()
		{
			GetPSDEditor();
		}

		[MenuItem("Assets/Sprites/PSD Import")]
		static void ImportPsdWindow()
		{
			var wnd = GetPSDEditor();
			wnd.Image = (Texture2D)Selection.objects[0];
			EditorUtility.SetDirty(wnd);
		}

		[MenuItem("Assets/Sprites/PSD Import", true)]
		static bool ImportPsd()
		{
			Object[] arr = Selection.objects;

			if (arr.Length != 1)
				return false;

			string assetPath = AssetDatabase.GetAssetPath(arr[0]);
			return assetPath.ToUpper().EndsWith(".PSD");
		}
		#endregion

		private PsdExportSettings settings;
		private PsdFileInfo fileInfo;

		private Vector2 scrollPos = Vector2.zero;

		private PSDExporter.PivotPos createPivot;
		private bool createAtSelection = false;
		private int createSortLayer = 0;

		private GUIStyle styleHeader, styleLabelLeft;

		private Texture2D image;
		public Texture2D Image
		{
			get { return image; }
			set
			{
				image = value;
				LoadImage();
			}
		}


		private static string[] _sortingLayerNames;

		void OnEnable()
		{
			SetupSortingLayerNames();
			if (image != null)
				LoadImage();
		}

		void SetupSortingLayerNames()
		{
			if (_sortingLayerNames == null)
			{
				var internalEditorUtilityType = Type.GetType("UnityEditorInternal.InternalEditorUtility, UnityEditor");
				var sortingLayersProperty = internalEditorUtilityType.GetProperty("sortingLayerNames", BindingFlags.Static | BindingFlags.NonPublic);
				_sortingLayerNames = sortingLayersProperty.GetValue(null, new object[0]) as string[];
			}
		}

		private bool LoadImage()
		{
			settings = new PsdExportSettings(image);
			bool valid = (settings.Psd != null);
			if (valid)
			{
				// Parse the layer info
				fileInfo = new PsdFileInfo(settings.Psd);
			}
			return valid;
		}

		void SetupStyles()
		{
			if (styleHeader == null)
			{
				styleHeader = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontStyle = FontStyle.Bold
				};
			}
			if (styleLabelLeft == null)
			{
				styleLabelLeft = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleLeft,
					padding = new RectOffset(0, 0, 0, 0)
				};
			}
		}

		public void OnGUI()
		{
			SetupStyles();

			EditorGUI.BeginChangeCheck();
			var img = (Texture2D)EditorGUILayout.ObjectField("PSD File", image, typeof(Texture2D), true);
			bool changed = EditorGUI.EndChangeCheck();
			if (changed)
				Image = img;

			if (settings.Psd != null)
			{
				DrawPsdLayers();

				DrawExportEntry();

				DrawSpriteEntry();
			}
			else
			{
				EditorGUILayout.HelpBox("This texture is not a PSD file.", MessageType.Error);
			}
		}

		private void DrawExportEntry()
		{
			GUILayout.Label("Export Settings", styleHeader);

			settings.ScaleBy = GUILayout.Toolbar(settings.ScaleBy, new string[] { "1X", "2X", "4X" });
			settings.PixelsToUnitSize = EditorGUILayout.FloatField("Pixels To Unit Size", settings.PixelsToUnitSize);
			if (settings.PixelsToUnitSize <= 0)
			{
				EditorGUILayout.HelpBox("Pixels To Unit Size should be greater than 0.", MessageType.Warning);
			}

			settings.Pivot = (PSDExporter.PivotPos)EditorGUILayout.EnumPopup("Pivot", settings.Pivot);
			if (settings.Pivot == PSDExporter.PivotPos.Custom)
			{
				settings.PivotVector = EditorGUILayout.Vector2Field("Custom Pivot", settings.PivotVector);
			}

			if (GUILayout.Button("Export Visible Layers"))
			{
				ExportLayers();
			}
		}

		private void DrawSpriteEntry()
		{
			GUILayout.Label("Sprite Creation", styleHeader);

			createPivot = (PSDExporter.PivotPos)EditorGUILayout.EnumPopup("Create Pivot", createPivot);

			if (_sortingLayerNames != null)
				createSortLayer = EditorGUILayout.Popup("Sorting Layer", createSortLayer, _sortingLayerNames);

			if (GUILayout.Button("Create at Selection"))
			{
				createAtSelection = true;
				CreateSprites();
			}

			if (GUILayout.Button("Create Sprites"))
			{
				createAtSelection = false;
				CreateSprites();
			}
		}

		private void DrawPsdLayers()
		{
			EditorGUILayout.LabelField("Layers", styleHeader);

			scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

			int indentLevel = 0;

			PsdFile psd = settings.Psd;
			for (int i = psd.Layers.Count - 1; i >= 0; i--)
			{
				Layer layer = psd.Layers[i];

				var groupInfo = fileInfo.GetGroupByLayerIndex(i);
				bool inGroup = groupInfo != null;

				bool startGroup = false;
				bool closeGroup = false;

				if (inGroup)
				{
					closeGroup = groupInfo.start == i;
					startGroup = groupInfo.end == i;
				}

				// If entering a layer group, indent
				if (closeGroup)
				{
					indentLevel--;
					continue;
				}

				if (inGroup && !startGroup)
				{
					// Skip contents if group folder closed
					if (!groupInfo.opened)
						continue;
					if (!groupInfo.visible)
						GUI.enabled = false;
				}

				if (layer.Name != "</Layer set>")
				{
					EditorGUILayout.BeginHorizontal();

					bool visToggle = true;
					if (startGroup)
						visToggle = groupInfo.visible;
					else
						visToggle = fileInfo.LayerVisibility[i];

					// Draw layer visibility toggle
					visToggle = EditorGUILayout.Toggle(visToggle, GUILayout.MaxWidth(15f));
					GUILayout.Space(indentLevel * 20f);

					if (startGroup)
					{
						// Draw the layer group name
						groupInfo.opened = EditorGUILayout.Foldout(groupInfo.opened, layer.Name);
						groupInfo.visible = visToggle;
						fileInfo.LayerVisibility[i] = visToggle;
					}
					else
					{
						// Draw the layer name
						GUILayout.Label(layer.Name, styleLabelLeft);
						fileInfo.LayerVisibility[i] = visToggle;
					}

					EditorGUILayout.EndHorizontal();
				}

				// If close group, just continue to the next layer
				if (startGroup)
				{
					indentLevel++;
				}

				GUI.enabled = true;
			} // End layer loop
			EditorGUILayout.EndScrollView();
		}

		private void ExportLayers()
		{
			PSDExporter.Export(settings, fileInfo);
		}

		private void CreateSprites()
		{
			int zOrder = settings.Psd.Layers.Count;

			// Find scaling factor
			float posScale = 1f;
			switch (settings.ScaleBy)
			{
				case 1:
					posScale = 0.5f;
					break;
				case 2:
					posScale = 0.25f;
					break;
			}

			GameObject root = new GameObject(settings.Filename);

			// Create the offset vector
			Vector3 createOffset = Vector3.zero;
			if (createPivot != PivotPos.TopLeft)
			{
				Vector2 docSize = new Vector2(settings.Psd.ColumnCount, settings.Psd.RowCount);
				docSize *= posScale;

				if (createPivot == PivotPos.Center || createPivot == PivotPos.Left || createPivot == PivotPos.Right)
					createOffset.y = (docSize.y / 2) / settings.PixelsToUnitSize;
				if (createPivot == PivotPos.Bottom || createPivot == PivotPos.BottomLeft || createPivot == PivotPos.BottomRight)
					createOffset.y = docSize.y / settings.PixelsToUnitSize;

				if (createPivot == PivotPos.Center || createPivot == PivotPos.Top || createPivot == PivotPos.Bottom)
					createOffset.x = -(docSize.x / 2) / settings.PixelsToUnitSize;
				if (createPivot == PivotPos.Right || createPivot == PivotPos.TopRight || createPivot == PivotPos.BottomRight)
					createOffset.x = -(docSize.x) / settings.PixelsToUnitSize;
			}

			// Loop through the layers
			Dictionary<LayerGroupInfo, GameObject> groupHeaders = new Dictionary<LayerGroupInfo, GameObject>();
			GameObject lastParent = root;
			for (int i = settings.Psd.Layers.Count - 1; i >= 0; i--)
			{
				var groupInfo = fileInfo.GetGroupByLayerIndex(i);
				if (groupInfo != null && !groupInfo.visible)
					continue;

				if (!fileInfo.LayerVisibility[i])
					continue;

				Layer layer = settings.Psd.Layers[i];

				bool inGroup = groupInfo != null;

				if (inGroup)
				{
					bool startGroup = groupInfo.end == i;
					bool closeGroup = groupInfo.start == i;

					if (startGroup)
					{
						GameObject groupRoot = new GameObject(layer.Name);
						groupRoot.transform.parent = lastParent.transform;
						groupRoot.transform.localPosition = Vector3.zero;
						groupRoot.transform.localScale = Vector3.one;

						lastParent = groupRoot;
						groupHeaders.Add(groupInfo, groupRoot);
						continue;
					}
					if (closeGroup)
					{
						lastParent = groupHeaders[groupInfo].transform.parent.gameObject;
						continue;
					}
				}

				// Try to get the sprite from the asset database first
				string assetPath = AssetDatabase.GetAssetPath(image);
				string path = Path.Combine(Path.GetDirectoryName(assetPath),
					Path.GetFileNameWithoutExtension(assetPath) + "_" + layer.Name + ".png");

				// Sprites doesn't exist, create it
				Sprite spr = (Sprite)AssetDatabase.LoadAssetAtPath(path, typeof(Sprite));
				if (spr == null)
				{
					spr = PSDExporter.CreateSprite(settings, layer);
				}

				// Get the pivot settings for the sprite
				TextureImporter spriteSettings = (TextureImporter)AssetImporter.GetAtPath(path);

				GameObject go = new GameObject(layer.Name);
				SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
				sr.sprite = spr;
				sr.sortingOrder = zOrder--;
				if (_sortingLayerNames != null)
				{
					sr.sortingLayerName = _sortingLayerNames[createSortLayer];
				}

				Vector3 goPos = Vector3.zero;
				goPos.x = ((layer.Rect.width * spriteSettings.spritePivot.x) + layer.Rect.x) / settings.PixelsToUnitSize;
				goPos.y = (-(layer.Rect.height * (1 - spriteSettings.spritePivot.y)) - layer.Rect.y) / settings.PixelsToUnitSize;
				goPos.x *= posScale;
				goPos.y *= posScale;

				goPos += createOffset;

				go.transform.parent = lastParent.transform;
				go.transform.localScale = Vector3.one;
				go.transform.localPosition = goPos;

				if (createAtSelection && Selection.activeGameObject != null)
				{
					go.layer = Selection.activeGameObject.layer;
				}
			}

			if (createAtSelection && Selection.activeGameObject != null)
			{
				root.transform.parent = Selection.activeGameObject.transform;
				root.transform.localScale = Vector3.one;
				root.transform.localPosition = Vector3.zero;
				root.layer = Selection.activeGameObject.layer;
			}
		}
	}
}