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
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class PSDEditorWindow : EditorWindow
{
	public enum PivotPos
	{
		Center,
		TopLeft,
		Top,
		TopRight,
		Left,
		Right,
		BottomLeft,
		Bottom,
		BottomRight,
		Custom
	}

	private Texture2D image;
	private Vector2 scrollPos;
	private PsdFile psd;
	private int atlassize = 4096;
	private float pixelsToUnitSize = 100.0f;
	private string fileName;
	private int scaleBy = 0;

	private bool showAtlas = false;
	private bool showSprite = false;
	private PivotPos pivot;
	private Vector2 pivotCustom;

	private bool imageChanged = false;

	private bool createAtSelection = false;
	private PivotPos createPivot = PivotPos.TopLeft;
	private int createSortLayer = 0;

	public Texture2D Image
	{
		get { return image; }
		set
		{
			image = value;
			AssetDatabase.GetLabels(image);
			imageChanged = true;
		}
	}

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

	private static string[] _sortingLayerNames;

	void SetupSortingLayerNames()
	{
		if (_sortingLayerNames == null)
		{
			var internalEditorUtilityType = Type.GetType("UnityEditorInternal.InternalEditorUtility, UnityEditor");
			var sortingLayersProperty = internalEditorUtilityType.GetProperty("sortingLayerNames", BindingFlags.Static | BindingFlags.NonPublic);
			_sortingLayerNames = sortingLayersProperty.GetValue(null, new object[0]) as string[];
		}
	}

	void OnEnable()
	{
		SetupSortingLayerNames();
	}

	private bool LoadImage()
	{
		string path = AssetDatabase.GetAssetPath(image);
		bool valid = path.ToUpper().EndsWith(".PSD");
		if (valid)
		{
			psd = new PsdFile(path, Encoding.Default);
			fileName = Path.GetFileNameWithoutExtension(path);
			LoadMetaData();
		}
		else
		{
			psd = null;
		}

		if (imageChanged)
			imageChanged = false;

		return valid;
	}

	private void LoadMetaData()
	{
		string[] nameStrings = Enum.GetNames(typeof(PivotPos));
		Array nameVals = Enum.GetValues(typeof(PivotPos));

		string[] labels = AssetDatabase.GetLabels(image);
		foreach (var label in labels)
		{
			switch (label)
			{
				case "ImportX1":
					scaleBy = 0;
					break;
				case "ImportX2":
					scaleBy = 1;
					break;
				case "ImportX4":
					scaleBy = 2;
					break;
			}

			if (label.StartsWith("ImportAnchor"))
			{
				string pivotType = label.Substring(12);
				for (int i = 0; i < nameStrings.Length; i++)
				{
					if (pivotType == nameStrings[i])
					{
						pivot = (PivotPos) nameVals.GetValue(i);
					}
				}
			}
		}
	}

	private void SaveMetaData()
	{
		string[] labels = new string[2];
		if (scaleBy == 0)
			labels[0] = "ImportX1";
		if (scaleBy == 1)
			labels[0] = "ImportX2";
		if (scaleBy == 2)
			labels[0] = "ImportX4";
		labels[1] = "ImportAnchor" + pivot.ToString();
		AssetDatabase.SetLabels(image, labels);
	}

	public void OnGUI()
	{
		EditorGUI.BeginChangeCheck();
		image = (Texture2D)EditorGUILayout.ObjectField("PSD File", image, typeof(Texture2D), true);
		bool changed = EditorGUI.EndChangeCheck() || imageChanged;

		if (image != null)
		{
			if (changed)
			{
				LoadImage();
			}

			if (psd != null)
			{
				DrawPsdLayers();
				DrawExportEntry();

				DrawSpriteEntry();
				DrawAtlasEntry();
			}
			else
			{
				EditorGUILayout.HelpBox("This texture is not a PSD file.", MessageType.Error);
			}
		}
	}

	private void DrawExportEntry()
	{
		scaleBy = GUILayout.Toolbar(scaleBy, new string[] { "1X", "2X", "4X" });

		pixelsToUnitSize = EditorGUILayout.FloatField("Pixels To Unit Size", pixelsToUnitSize);
		if (pixelsToUnitSize <= 0)
		{
			EditorGUILayout.HelpBox("Pixels To Unit Size should be greater than 0.", MessageType.Warning);
		}

		pivot = (PivotPos) EditorGUILayout.EnumPopup("Pivot", pivot);
		if (pivot == PivotPos.Custom)
		{
			pivotCustom = EditorGUILayout.Vector2Field("Custom Pivot", pivotCustom);
		}
		else
		{
			pivotCustom = new Vector2(0.5f, 0.5f);
			if (pivot == PivotPos.Top || pivot == PivotPos.TopLeft || pivot == PivotPos.TopRight)
				pivotCustom.y = 1;
			if (pivot == PivotPos.Bottom || pivot == PivotPos.BottomLeft || pivot == PivotPos.BottomRight)
				pivotCustom.y = 0f;

			if (pivot == PivotPos.Left || pivot == PivotPos.TopLeft || pivot == PivotPos.BottomLeft)
				pivotCustom.x = 0f;
			if (pivot == PivotPos.Right || pivot == PivotPos.TopRight || pivot == PivotPos.BottomRight)
				pivotCustom.x = 1f;
		}

		if (GUILayout.Button("Export visible layers"))
		{
			ExportLayers();
		}
	}

	private void DrawSpriteEntry()
	{
		showSprite = EditorGUILayout.Foldout(showSprite, "Sprite Creation");
		if (!showSprite)
			return;

		createPivot = (PivotPos)EditorGUILayout.EnumPopup("Create Pivot", createPivot);

		if (_sortingLayerNames != null)
			createSortLayer = EditorGUILayout.Popup("Sorting Layer", createSortLayer, _sortingLayerNames);

		if (GUILayout.Button("Create at Selection"))
		{
			createAtSelection = true;
			CreateSprites();
		}

		if (GUILayout.Button("Create sprites"))
		{
			createAtSelection = false;
			CreateSprites();
		}
	}

	private void DrawAtlasEntry()
	{
		showAtlas = EditorGUILayout.Foldout(showAtlas, "Atlas");
		if (!showAtlas)
			return;

		atlassize = EditorGUILayout.IntField("Max. atlas size", atlassize);
		if (!((atlassize != 0) && ((atlassize & (atlassize - 1)) == 0)))
		{
			EditorGUILayout.HelpBox("Atlas size should be a power of 2", MessageType.Warning);
		}

		if (GUILayout.Button("Create atlas"))
		{
			CreateAtlas();
		}
	}

	private void DrawPsdLayers()
	{
		GUIStyle header = new GUIStyle(GUI.skin.GetStyle("label"))
		{
			fontStyle = FontStyle.Bold
		};
		EditorGUILayout.LabelField("Layers", header);

		scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

		// Make the labels draw on the left
		GUIStyle leftLabel = new GUIStyle(GUI.skin.GetStyle("label"))
		{
			alignment = TextAnchor.MiddleLeft,
			padding = new RectOffset(0, 0, 0, 0)
		};

		int indentLevel = 0;
		List<bool> groupVisible = new List<bool>();

		for (int i = psd.Layers.Count - 1; i >= 0; i--)
		{
			Layer layer = psd.Layers[i];

			// Get the section info for this layer
			var secInfo = layer.AdditionalInfo
								.Where(info => info.GetType() == typeof(LayerSectionInfo))
								.ToArray();
			bool isOpen = false;
			bool isGroup = false;
			bool closeGroup = false;
			if (secInfo.Any())
			{
				foreach (var layerSecInfo in secInfo)
				{
					LayerSectionInfo info = (LayerSectionInfo)layerSecInfo;
					isOpen = info.SectionType == LayerSectionType.OpenFolder;
					isGroup = info.SectionType == LayerSectionType.ClosedFolder | isOpen;
					closeGroup = info.SectionType == LayerSectionType.SectionDivider;
					if (isGroup || closeGroup)
						break;
				}
			}

			// If close group, just continue to the next layer
			if (closeGroup)
			{
				indentLevel--;
				continue;
			}

			if (layer.Name != "</Layer set>")
			{
				EditorGUILayout.BeginHorizontal();

				// When inside a layer group, check if it is visible. If not, set to false
				if (indentLevel > 0 && !groupVisible[indentLevel - 1])
					layer.Visible = false;

				// Draw layer visibility toggle
				layer.Visible = EditorGUILayout.Toggle(layer.Visible, GUILayout.MaxWidth(15f));
				GUILayout.Space(indentLevel * 20f);

				if (isGroup)
				{
					// Draw the layer group name
					EditorGUILayout.Foldout(isOpen, layer.Name);
				}
				else
				{
					// Draw the layer name
					GUILayout.Label(layer.Name, leftLabel);
				}

				EditorGUILayout.EndHorizontal();
			}

			// If entering a layer group, indent and save the visibility of the layer group
			if (isGroup)
			{
				indentLevel++;
				if (indentLevel >= groupVisible.Count)
					groupVisible.Add(true);
				groupVisible[indentLevel - 1] = layer.Visible;
			}

		}
		EditorGUILayout.EndScrollView();
	}

	private Texture2D CreateTexture(Layer layer)
	{
		if ((int)layer.Rect.width == 0 || (int)layer.Rect.height == 0)
			return null;

		//int fileWidth = psd.ColumnCount;
		//int fileHeight = psd.RowCount;

		//int textureWidth = (int) layer.Rect.width;
		//int textureHeight = (int) layer.Rect.height;

		Texture2D tex = new Texture2D((int)layer.Rect.width, (int)layer.Rect.height, TextureFormat.RGBA32, true);
		Color32[] pixels = new Color32[tex.width * tex.height];

		Channel red = (from l in layer.Channels where l.ID == 0 select l).First();
		Channel green = (from l in layer.Channels where l.ID == 1 select l).First();
		Channel blue = (from l in layer.Channels where l.ID == 2 select l).First();
		Channel alpha = layer.AlphaChannel;

		for (int i = 0; i < pixels.Length; i++)
		{
			byte r = red.ImageData[i];
			byte g = green.ImageData[i];
			byte b = blue.ImageData[i];
			byte a = 255;

			if (alpha != null)
				a = alpha.ImageData[i];

			int mod = i % tex.width;
			int n = ((tex.width - mod - 1) + i) - mod;
			pixels[pixels.Length - n - 1] = new Color32(r, g, b, a);
		}

		tex.SetPixels32(pixels);
		tex.Apply();

		return tex;
	}

	private void ExportLayers()
	{
		SaveMetaData();
		foreach (Layer layer in psd.Layers)
		{
			if (layer.Visible)
			{
				Texture2D tex = CreateTexture(layer);
				if (tex == null) continue;
				SaveAsset(tex, "_" + layer.Name);
				DestroyImmediate(tex);
			}
		}
	}

	private void CreateAtlas()
	{
		// Texture2D[] textures = (from layer in psd.Layers where layer.Visible select CreateTexture(layer) into tex where tex != null select tex).ToArray();

		List<Texture2D> textures = new List<Texture2D>();

		// Track the spriteRenderers created via a List
		List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();

		int zOrder = 0;
		GameObject root = new GameObject(fileName);
		foreach (var layer in psd.Layers)
		{
			if (layer.Visible && layer.Rect.width > 0 && layer.Rect.height > 0)
			{
				Texture2D tex = CreateTexture(layer);
				// Add the texture to the Texture Array
				textures.Add(tex);

				GameObject go = new GameObject(layer.Name);
				SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
				go.transform.position = new Vector3((layer.Rect.width / 2 + layer.Rect.x) / pixelsToUnitSize, (-layer.Rect.height / 2 - layer.Rect.y) / pixelsToUnitSize, 0);
				// Add the sprite renderer to the SpriteRenderer Array
				spriteRenderers.Add(sr);
				sr.sortingOrder = zOrder++;
				go.transform.parent = root.transform;
			}
		}

		// The output of PackTextures returns a Rect array from which we can create our sprites
		Rect[] rects;
		Texture2D atlas = new Texture2D(atlassize, atlassize);
		Texture2D[] textureArray = textures.ToArray();
		rects = atlas.PackTextures(textureArray, 2, atlassize);
		List<SpriteMetaData> Sprites = new List<SpriteMetaData>();

		// For each rect in the Rect Array create the sprite and assign to the SpriteMetaData
		for (int i = 0; i < rects.Length; i++)
		{
			// add the name and rectangle to the dictionary
			SpriteMetaData smd = new SpriteMetaData();
			smd.name = spriteRenderers[i].name;
			smd.rect = new Rect(rects[i].xMin * atlas.width, rects[i].yMin * atlas.height, rects[i].width * atlas.width, rects[i].height * atlas.height);
			smd.pivot = new Vector2(0.5f, 0.5f); // Center is default otherwise layers will be misaligned
			smd.alignment = (int)SpriteAlignment.Center;
			Sprites.Add(smd);
		}

		// Need to load the image first
		string assetPath = AssetDatabase.GetAssetPath(image);
		string path = Path.Combine(Path.GetDirectoryName(assetPath),
			Path.GetFileNameWithoutExtension(assetPath) + "_atlas" + ".png");

		byte[] buf = atlas.EncodeToPNG();
		File.WriteAllBytes(path, buf);
		AssetDatabase.Refresh();

		// Get our texture that we loaded
		atlas = (Texture2D)AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));
		TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
		// Make sure the size is the same as our atlas then create the spritesheet
		textureImporter.maxTextureSize = atlassize;
		textureImporter.spritesheet = Sprites.ToArray();
		textureImporter.textureType = TextureImporterType.Sprite;
		textureImporter.spriteImportMode = SpriteImportMode.Multiple;
		textureImporter.spritePivot = new Vector2(0.5f, 0.5f);
		textureImporter.spritePixelsToUnits = pixelsToUnitSize;
		AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

		// For each rect in the Rect Array create the sprite and assign to the SpriteRenderer
		for (int j = 0; j < textureImporter.spritesheet.Length; j++)
		{
			// Debug.Log(textureImporter.spritesheet[j].rect);
			Sprite spr = Sprite.Create(atlas, textureImporter.spritesheet[j].rect, textureImporter.spritesheet[j].pivot, pixelsToUnitSize);  // The 100.0f is for the pixels to unit, maybe make that a public variable for the user to change before hand?

			// Add the sprite to the sprite renderer
			spriteRenderers[j].sprite = spr;
		}

		foreach (Texture2D tex in textureArray)
		{
			DestroyImmediate(tex);
		}
	}

	private void CreateSprites()
	{
		int zOrder = 0;

		// Find scaling factor
		float posScale = 1f;
		switch (scaleBy)
		{
			case 1:
				posScale = 0.5f;
				break;
			case 2:
				posScale = 0.25f;
				break;
		}

		GameObject root = new GameObject(fileName);

		// Create the offset vector
		Vector3 createOffset = Vector3.zero;
		if (createPivot != PivotPos.TopLeft)
		{
			Vector2 docSize = new Vector2(psd.ColumnCount, psd.RowCount);
			docSize *= posScale;

			if (createPivot == PivotPos.Center || createPivot == PivotPos.Left || createPivot == PivotPos.Right)
				createOffset.y = (docSize.y / 2) / pixelsToUnitSize;
			if (createPivot == PivotPos.Bottom || createPivot == PivotPos.BottomLeft || createPivot == PivotPos.BottomRight)
				createOffset.y = docSize.y / pixelsToUnitSize;

			if (createPivot == PivotPos.Center || createPivot == PivotPos.Top || createPivot == PivotPos.Bottom)
				createOffset.x = -(docSize.x / 2) / pixelsToUnitSize;
			if (createPivot == PivotPos.Right || createPivot == PivotPos.TopRight || createPivot == PivotPos.BottomRight)
				createOffset.x = -(docSize.x) / pixelsToUnitSize;
		}

		// Loop through the layers
		foreach (var layer in psd.Layers)
		{
			if (layer.Visible && layer.Rect.width > 0 && layer.Rect.height > 0)
			{
				// Try to get the sprite from the asset database first
				string assetPath = AssetDatabase.GetAssetPath(image);
				string path = Path.Combine(Path.GetDirectoryName(assetPath),
					Path.GetFileNameWithoutExtension(assetPath) + "_" + layer.Name + ".png");

				// Sprites doesn't exist, create it
				Sprite spr = (Sprite) AssetDatabase.LoadAssetAtPath(path, typeof(Sprite));
				if (spr == null)
				{
					Texture2D tex = CreateTexture(layer);
					spr = SaveAsset(tex, "_" + layer.Name);
					DestroyImmediate(tex);
				}

				// Get the pivot settings for the sprite
				TextureImporter spriteSettings = (TextureImporter) AssetImporter.GetAtPath(path);

				GameObject go = new GameObject(layer.Name);
				SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
				sr.sprite = spr;
				sr.sortingOrder = zOrder++;
				if (_sortingLayerNames != null)
				{
					sr.sortingLayerName = _sortingLayerNames[createSortLayer];
				}

				Vector3 goPos = Vector3.zero;
				goPos.x = ((layer.Rect.width*spriteSettings.spritePivot.x) + layer.Rect.x) / pixelsToUnitSize;
				goPos.y = (-(layer.Rect.height * (1-spriteSettings.spritePivot.y)) - layer.Rect.y) / pixelsToUnitSize;
				goPos.x *= posScale;
				goPos.y *= posScale;

				goPos += createOffset;

				go.transform.position = goPos;
				go.transform.parent = root.transform;
			}
		}

		if (createAtSelection && Selection.activeGameObject != null)
		{
		}
	}

	private Sprite SaveAsset(Texture2D tex, string suffix)
	{
		string assetPath = AssetDatabase.GetAssetPath(image);
		string path = Path.Combine(Path.GetDirectoryName(assetPath),
			Path.GetFileNameWithoutExtension(assetPath) + suffix + ".png");

		if (scaleBy > 0)
		{
			var resize = new TextureResize(tex);
			int width = Mathf.RoundToInt(tex.width / 2);
			int height = Mathf.RoundToInt(tex.height / 2);
			if (scaleBy == 2)
			{
				width = Mathf.RoundToInt(tex.width / 4);
				height = Mathf.RoundToInt(tex.height / 4);
			}
			tex = resize.Resize(width, height);
		}

		byte[] buf = tex.EncodeToPNG();
		File.WriteAllBytes(path, buf);
		AssetDatabase.Refresh();
		// Load the texture so we can change the type
		var textureObj = AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));
		TextureImporter textureImporter = (TextureImporter) AssetImporter.GetAtPath(path);

		textureImporter.textureType = TextureImporterType.Sprite;
		textureImporter.spriteImportMode = SpriteImportMode.Single;
		textureImporter.spritePivot = pivotCustom;
		textureImporter.spritePixelsToUnits = pixelsToUnitSize;
		AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
		EditorUtility.SetDirty(textureObj);

		return (Sprite)AssetDatabase.LoadAssetAtPath(path, typeof(Sprite));
	}
}

