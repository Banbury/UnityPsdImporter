using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PhotoshopFile;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace kontrabida.psdexport
{

	public class PsdFileInfo
	{
		public class InstancedLayerInfo
		{
			public int instanceLayer;
			public List<int> duplicateLayers;
		}

		public LayerGroupInfo[] LayerGroups { get; protected set; }

		/// <summary>
		/// Layer visibility data, indexed by layer
		/// </summary>
		public bool[] LayerVisibility { get; protected set; }

		public PsdFileInfo(PsdFile psd)
		{
			List<LayerGroupInfo> layerGroups = new List<LayerGroupInfo>();
			List<LayerGroupInfo> openGroupStack = new List<LayerGroupInfo>();
			List<bool> layerVisibility = new List<bool>();
			for (int i = psd.Layers.Count - 1; i >= 0; i--)
			{
				Layer layer = psd.Layers[i];

				layerVisibility.Add(layer.Visible);

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

				if (isGroup)
				{
					// Open a new layer group info
					openGroupStack.Add(new LayerGroupInfo(layer.Name, i, layer.Visible, isOpen));
				}
				else if (closeGroup)
				{
					// Set the end index of the latest LayerGroupInfo
					var closeInfo = openGroupStack.Last();
					closeInfo.start = i;
					// Add it to the layerGroup list
					layerGroups.Add(closeInfo);
					// And remove it from the open group stack 
					openGroupStack.RemoveAt(openGroupStack.Count - 1);
				}
				else
				{
					// Normal layer, look for instances	
					if (layer.Name.Contains(" Copy"))
					{

					}
				}
			} // End layer loop

			layerVisibility.Reverse();
			LayerVisibility = layerVisibility.ToArray();

			LayerGroups = layerGroups.ToArray();
		}

		public InstancedLayerInfo GetInstancedLayer(int layerindex)
		{
			return null;
		}

		public LayerGroupInfo GetGroupByLayerIndex(int layerIndex)
		{
			List<LayerGroupInfo> candidates = new List<LayerGroupInfo>();
			// Might be a nested layer group
			foreach (var layerGroupInfo in LayerGroups)
			{
				if (layerGroupInfo.ContainsLayer(layerIndex))
					candidates.Add(layerGroupInfo);
			}
			return candidates.OrderBy(info => info.end - info.start).FirstOrDefault();
		}

		public LayerGroupInfo GetGroupByStartIndex(int startIndex)
		{
			return LayerGroups.FirstOrDefault(info => info.end == startIndex);
		}
	}

	public class LayerGroupInfo
	{
		public string name;
		public float scale;
		public int end, start;
		public bool visible, opened;

		public LayerGroupInfo(string name, int end, bool visible, bool opened)
		{
			this.name = name;
			this.end = end;
			this.visible = visible;
			this.opened = opened;

			start = -1;
			scale = 1;
		}

		public bool ContainsLayer(int layerIndex)
		{
			return (layerIndex <= end) && (layerIndex >= start);
		}
	}

	public class PsdExportSettings
	{
		public List<bool> exportFlags = new List<bool>();

		public PsdFile Psd { get; protected set; }
		public string Filename { get; protected set; }
		public Texture2D Image { get; protected set; }

		public float PixelsToUnitSize { get; set; }
		public int ScaleBy { get; set; }
		public Vector2 PivotVector { get; set; }

		private PSDExporter.PivotPos _pivot;
		public PSDExporter.PivotPos Pivot
		{
			get { return _pivot; }
			set
			{
				_pivot = value;
				if (_pivot == PSDExporter.PivotPos.Custom)
					return;

				Vector2 pivotCustom = new Vector2(0.5f, 0.5f);
				if (_pivot == PSDExporter.PivotPos.Top || _pivot == PSDExporter.PivotPos.TopLeft || _pivot == PSDExporter.PivotPos.TopRight)
					pivotCustom.y = 1;
				if (_pivot == PSDExporter.PivotPos.Bottom || _pivot == PSDExporter.PivotPos.BottomLeft || _pivot == PSDExporter.PivotPos.BottomRight)
					pivotCustom.y = 0f;

				if (_pivot == PSDExporter.PivotPos.Left || _pivot == PSDExporter.PivotPos.TopLeft || _pivot == PSDExporter.PivotPos.BottomLeft)
					pivotCustom.x = 0f;
				if (_pivot == PSDExporter.PivotPos.Right || _pivot == PSDExporter.PivotPos.TopRight || _pivot == PSDExporter.PivotPos.BottomRight)
					pivotCustom.x = 1f;
				PivotVector = pivotCustom;
			}
		}

		public PsdExportSettings(Texture2D image)
		{
			string path = AssetDatabase.GetAssetPath(image);
			if (!path.ToUpper().EndsWith(".PSD"))
				return;

			Psd = new PsdFile(path, Encoding.Default);
			Filename = Path.GetFileNameWithoutExtension(path);
			Image = image;

			ScaleBy = 0;
			Pivot = PSDExporter.PivotPos.Center;
			PixelsToUnitSize = 100f;

			LoadMetaData();
		}

		private void LoadMetaData()
		{
			string[] nameStrings = Enum.GetNames(typeof(PSDExporter.PivotPos));
			Array nameVals = Enum.GetValues(typeof(PSDExporter.PivotPos));

			string[] labels = AssetDatabase.GetLabels(Image);
			foreach (var label in labels)
			{
				if (label.Equals("ImportX1"))
					ScaleBy = 0;
				if (label.Equals("ImportX2"))
					ScaleBy = 1;
				if (label.Equals("ImportX4"))
					ScaleBy = 2;

				if (label.StartsWith("ImportAnchor"))
				{
					string pivotType = label.Substring(12);
					if (pivotType.StartsWith("Custom"))
					{
						//string values = pivotType.Substring(pivotType.IndexOf("["), pivotType.IndexOf("]"));
						//string[] vals = values.Split(',');
						//PivotVector = new Vector2(float.Parse(vals[0]), float.Parse(vals[1]));
						Pivot = PSDExporter.PivotPos.Custom;
					}
					else
					{
						// Find by enum
						for (int i = 0; i < nameStrings.Length; i++)
						{
							if (pivotType == nameStrings[i])
								Pivot = (PSDExporter.PivotPos)nameVals.GetValue(i);
						}
					}
				} // End import anchor if

				if (label.StartsWith("ImportPTU|"))
				{
					string ptuVal = label.Substring(10);
					PixelsToUnitSize = Single.Parse(ptuVal);
				}
			} // End label loop
		}

		public void SaveMetaData()
		{
			string[] labels = new string[3];

			if (ScaleBy == 0)
				labels[0] = "ImportX1";
			if (ScaleBy == 1)
				labels[0] = "ImportX2";
			if (ScaleBy == 2)
				labels[0] = "ImportX4";

			labels[1] = "ImportAnchor" + Pivot.ToString();
			if (Pivot == PSDExporter.PivotPos.Custom)
			{
				labels[1] = "ImportAnchorCustom[" + PivotVector.x + "," + PivotVector.y + "]";
			}

			labels[2] = "ImportPTU|" + PixelsToUnitSize;

			AssetDatabase.SetLabels(Image, labels);
		}
	}

	public class PSDExporter
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

		public static void Export(PsdExportSettings settings, PsdFileInfo fileInfo)
		{
			for (int i = 0; i < settings.Psd.Layers.Count; i++)
			{
				var groupInfo = fileInfo.GetGroupByLayerIndex(i);
				if (groupInfo != null && !groupInfo.visible)
					continue;

				if (!fileInfo.LayerVisibility[i])
					continue;
					
				var layer = settings.Psd.Layers[i];
				CreateSprite(settings, layer);
			}
			settings.SaveMetaData();
		}

		public static Sprite CreateSprite(PsdExportSettings settings, Layer layer)
		{
			Texture2D tex = CreateTexture(layer);
			if (tex == null)
				return null;
			Sprite sprite = SaveAsset(settings, tex, "_" + layer.Name);
			Object.DestroyImmediate(tex);
			return sprite;
		}

		private static Texture2D CreateTexture(Layer layer)
		{
			if ((int)layer.Rect.width == 0 || (int)layer.Rect.height == 0)
				return null;

			// For possible clip to document functionality
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

		private static Sprite SaveAsset(PsdExportSettings settings, Texture2D tex, string suffix)
		{
			string assetPath = AssetDatabase.GetAssetPath(settings.Image);
			string path = Path.Combine(Path.GetDirectoryName(assetPath),
				Path.GetFileNameWithoutExtension(assetPath) + suffix + ".png");

			if (settings.ScaleBy > 0)
			{
				int width = Mathf.RoundToInt(tex.width / 2);
				int height = Mathf.RoundToInt(tex.height / 2);
				int mipLevel = 1;
				if (settings.ScaleBy == 2)
				{
					width = Mathf.RoundToInt(tex.width / 4);
					height = Mathf.RoundToInt(tex.height / 4);
					mipLevel = 2;
				}
				// Scaling by abusing mip maps
				Texture2D resized = new Texture2D(width, height);
				resized.SetPixels32(tex.GetPixels32(mipLevel));
				resized.Apply();
				tex = resized;
			}

			byte[] buf = tex.EncodeToPNG();
			File.WriteAllBytes(path, buf);
			AssetDatabase.Refresh();

			// Load the texture so we can change the type
			var textureObj = AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));
			TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(path);

			textureImporter.textureType = TextureImporterType.Sprite;
			textureImporter.spriteImportMode = SpriteImportMode.Single;
			textureImporter.spritePivot = settings.PivotVector;
			textureImporter.spritePixelsToUnits = settings.PixelsToUnitSize;
			EditorUtility.SetDirty(textureObj);
			AssetDatabase.WriteImportSettingsIfDirty(path);
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

			return (Sprite)AssetDatabase.LoadAssetAtPath(path, typeof(Sprite));
		}
	}
}
