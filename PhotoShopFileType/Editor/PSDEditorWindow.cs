/*
The MIT License (MIT)

Copyright (c) 2013-2015 Banbury & Play-Em

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

using PhotoshopFile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public class PSDEditorWindow : EditorWindow {
    private Texture2D image;
    private Vector2 scrollPos;
    private PsdFile psd;
    private int atlassize = 4096;
    private float pixelsToUnitSize = 100.0f;
    private bool importIntoSelected = false;
    private string fileName;
	private bool useSizeDelta;

    private Transform selectedTransform;

    [MenuItem("Sprites/PSD Import")]
    public static void ShowWindow() {
        var wnd = GetWindow<PSDEditorWindow>();
        wnd.title = "PSD Import";
        wnd.Show();
    }

    public void OnGUI() {
        EditorGUI.BeginChangeCheck();
        image = (Texture2D)EditorGUILayout.ObjectField("PSD File", image, typeof(Texture2D), true);
        bool changed = EditorGUI.EndChangeCheck();

        if (image != null) {
            if (changed) {
                string path = AssetDatabase.GetAssetPath(image);

                if (path.ToUpper().EndsWith(".PSD")) {
                    psd = new PsdFile(path, Encoding.Default);
                    fileName = Path.GetFileNameWithoutExtension(path);
                }
                else {
                    psd = null;
                }
            }
            if (psd != null) {
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

                foreach (Layer layer in psd.Layers) {
                    if (layer.Name != "</Layer set>" && layer.Name != "</Layer group>") {
                        layer.Visible = EditorGUILayout.ToggleLeft(layer.Name, layer.Visible);
                    }
                }

                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("Export visible layers")) {
                    ExportLayers();
                }

                atlassize = EditorGUILayout.IntField("Max. atlas size", atlassize);

                if (!((atlassize != 0) && ((atlassize & (atlassize - 1)) == 0))) {
                    EditorGUILayout.HelpBox("Atlas size should be a power of 2", MessageType.Warning);
                }

                pixelsToUnitSize = EditorGUILayout.FloatField("Pixels To Unit Size", pixelsToUnitSize);

                if (pixelsToUnitSize <= 0) {
                    EditorGUILayout.HelpBox("Pixels To Unit Size should be greater than 0.", MessageType.Warning);
                }
                importIntoSelected = EditorGUILayout.Toggle("Import into selected object", importIntoSelected);
                useSizeDelta = EditorGUILayout.Toggle("Use Size Delta", useSizeDelta);
                if (GUILayout.Button("Create atlas")) {
                    CreateAtlas();
                }
                if (GUILayout.Button("Create sprites")) {
                    CreateSprites();
                }
                if (GUILayout.Button("Create images")) {
                    CreateImages();
                }
            }
            else {
                EditorGUILayout.HelpBox("This texture is not a PSD file.", MessageType.Error);
            }
        }
    }

    private Texture2D CreateTexture(Layer layer) {
        if ((int)layer.Rect.width == 0 || (int)layer.Rect.height == 0)
            return null;

        Texture2D tex = new Texture2D((int)layer.Rect.width, (int)layer.Rect.height, TextureFormat.RGBA32, true);
        Color32[] pixels = new Color32[tex.width * tex.height];

        Channel red = (from l in layer.Channels where l.ID == 0 select l).First();
        Channel green = (from l in layer.Channels where l.ID == 1 select l).First();
        Channel blue = (from l in layer.Channels where l.ID == 2 select l).First();
        Channel alpha = layer.AlphaChannel;

        for (int i = 0; i < pixels.Length; i++) {
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

    private void ExportLayers() {
        foreach (Layer layer in psd.Layers) {
            if (layer.Visible) {
                Texture2D tex = CreateTexture(layer);
                if (tex == null) continue;
                SaveAsset(tex, "_" + layer.Name);
                DestroyImmediate(tex);
            }
        }
    }

    private void CreateAtlas() {
        // Texture2D[] textures = (from layer in psd.Layers where layer.Visible select CreateTexture(layer) into tex where tex != null select tex).ToArray();

        List<Texture2D> textures = new List<Texture2D>();

        // Track the spriteRenderers created via a List
        List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();

        int zOrder = 0;
        GameObject root = new GameObject(fileName);
        foreach (var layer in psd.Layers) {
            if (layer.Visible && layer.Rect.width > 0 && layer.Rect.height > 0) {
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
        for (int i = 0; i < rects.Length; i++) {
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
        textureImporter.spritePixelsPerUnit = pixelsToUnitSize;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        // For each rect in the Rect Array create the sprite and assign to the SpriteRenderer
        for (int j = 0; j < textureImporter.spritesheet.Length; j++) {
            // Debug.Log(textureImporter.spritesheet[j].rect);
            Sprite spr = Sprite.Create(atlas, textureImporter.spritesheet[j].rect, textureImporter.spritesheet[j].pivot, pixelsToUnitSize);  // The 100.0f is for the pixels to unit, maybe make that a public variable for the user to change before hand?

            // Add the sprite to the sprite renderer
            spriteRenderers[j].sprite = spr;
        }

        foreach (Texture2D tex in textureArray) {
            DestroyImmediate(tex);
        }
    }

    private void CreateSprites() {
        if (importIntoSelected) {
            selectedTransform = Selection.activeTransform;
        }
        int zOrder = 0;
        GameObject root = new GameObject(fileName);
        if (importIntoSelected && selectedTransform != null) {
            root.transform.parent = selectedTransform;
        }
        foreach (var layer in psd.Layers) {
            if (layer.Visible && layer.Rect.width > 0 && layer.Rect.height > 0) {
                Texture2D tex = CreateTexture(layer);
                Sprite spr = SaveAsset(tex, "_" + layer.Name);
                DestroyImmediate(tex);

                GameObject go = new GameObject(layer.Name);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = spr;
                sr.sortingOrder = zOrder++;
                go.transform.position = new Vector3((layer.Rect.width / 2 + layer.Rect.x) / pixelsToUnitSize, (-layer.Rect.height / 2 - layer.Rect.y) / pixelsToUnitSize, 0);
                go.transform.parent = root.transform;
            }
        }
    }
    private void CreateImages() {
        if (importIntoSelected) {
            selectedTransform = Selection.activeTransform;
        }
        int zOrder = 0;
        GameObject root = new GameObject(fileName);
        root.transform.localPosition = new Vector3(0, 0, 0);
        var rtransf = root.AddComponent<RectTransform>();
        if (importIntoSelected && selectedTransform != null)
            root.transform.parent = selectedTransform;
        rtransf.anchorMin = new Vector2(0f, 0f);
        rtransf.anchorMax = new Vector2(1f, 1f);
        rtransf.pivot = new Vector2(0.5f, 0.5f);
        rtransf.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0);
        rtransf.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0);
        rtransf.sizeDelta = Vector2.zero;
        rtransf.localPosition = Vector3.zero;

        foreach (var layer in psd.Layers) {
            if (layer.Visible && layer.Rect.width > 0 && layer.Rect.height > 0) {
                var targetOrder = zOrder++;
                Texture2D tex = CreateTexture(layer);
                Sprite spr = SaveAsset(tex, "_" + layer.Name);
                DestroyImmediate(tex);

                GameObject go = new GameObject(layer.Name);
                go.transform.parent = root.transform;
                go.transform.SetSiblingIndex(targetOrder);
                UnityEngine.UI.Image image = go.AddComponent<UnityEngine.UI.Image>();
                image.sprite = spr;
                image.rectTransform.localPosition = new Vector3((layer.Rect.x), (layer.Rect.y * -1) - layer.Rect.height, targetOrder * 5);
                image.rectTransform.anchorMax = new Vector2(0, 1);
                image.rectTransform.anchorMin = new Vector2(0, 1);
                image.rectTransform.pivot = new Vector2(0f, 0f);
                image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, layer.Rect.width);
                image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, layer.Rect.height);
            }
        }
    }

    private Sprite SaveAsset(Texture2D tex, string suffix) {
        string assetPath = AssetDatabase.GetAssetPath(image);
        string path = Path.Combine(Path.GetDirectoryName(assetPath),
            Path.GetFileNameWithoutExtension(assetPath) + suffix + ".png");

        byte[] buf = tex.EncodeToPNG();
        File.WriteAllBytes(path, buf);
        AssetDatabase.Refresh();
        // Load the texture so we can change the type
        AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));
        TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
        textureImporter.textureType = TextureImporterType.Sprite;
        textureImporter.spriteImportMode = SpriteImportMode.Single;
        textureImporter.spritePivot = new Vector2(0.5f, 0.5f);
        textureImporter.spritePixelsPerUnit = pixelsToUnitSize;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        return (Sprite)AssetDatabase.LoadAssetAtPath(path, typeof(Sprite));
    }

}

