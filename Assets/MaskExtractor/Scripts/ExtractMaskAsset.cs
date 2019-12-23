using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

#if UNITY_EDITOR

using UnityEditor;

#endif

namespace m039.MaskExtractor
{
    [CreateAssetMenu(fileName = "MaskExtractor", menuName = Consts.ContextMenuRoot + "/CreateMaskExtractor", order = 1)]
    public class ExtractMaskAsset : ScriptableObject
    {
        [System.Serializable]
        public class MaskInfo
        {
            [System.Serializable]
            public class PickedColor
            {
                public Color color;

                [Range(0f, 1f)]
                public float threshold;

                [Range(0f, 1f)]
                public float hCoeff = 1f;

                [Range(0f, 1f)]
                public float sCoeff = 0.1f;

                [Range(0f, 1f)]
                public float vCoeff = 0.1f;

                public PickedColor()
                {
                }

                public PickedColor(PickedColor other)
                {
                    color = other.color;
                    threshold = other.threshold;
                    hCoeff = other.hCoeff;
                    sCoeff = other.sCoeff;
                    vCoeff = other.vCoeff;
                }

                public override bool Equals(object obj)
                {
                    if ((obj == null) || !this.GetType().Equals(obj.GetType()))
                    {
                        return false;
                    }
                    else
                    {
                        PickedColor other = (PickedColor)obj;

                        return color.Equals(other.color) &&
                            threshold.Equals(other.threshold) &&
                            hCoeff.Equals(other.hCoeff) &&
                            sCoeff.Equals(other.sCoeff) &&
                            vCoeff.Equals(other.vCoeff);
                    }
                }

                public override int GetHashCode()
                {
                    var first = color.GetHashCode();
                    var second = threshold.GetHashCode();

                    return first ^ second; // Just in case.
                }
            }

            public string name;

            public PickedColor[] pickedColors;

            public MaskInfo()
            {
            }

            public MaskInfo(MaskInfo other)
            {
                name = other.name;
                var list = new List<PickedColor>();
                if (other.pickedColors != null)
                {
                    foreach (var pickedColor in other.pickedColors)
                    {
                        list.Add(new PickedColor(pickedColor));
                    }
                }

                pickedColors = list.ToArray();
            }

            public override bool Equals(object obj)
            {
                //Check for null and compare run-time types.
                if ((obj == null) || !this.GetType().Equals(obj.GetType()))
                {
                    return false;
                }
                else
                {
                    MaskInfo other = (MaskInfo)obj;

                    if (string.IsNullOrEmpty(name) != string.IsNullOrEmpty(other.name))
                        return false;

                    if (!name.Equals(other.name))
                        return false;

                    if ((pickedColors == null || pickedColors.Length <= 0) 
                        != 
                        (other.pickedColors == null || other.pickedColors.Length <= 0))
                    {
                        return false;
                    }

                    if (pickedColors.Length != other.pickedColors.Length)
                        return false;

                    for (int i = 0; i < pickedColors.Length; i++)
                    {
                        if (!pickedColors[i].Equals(other.pickedColors[i]))
                            return false;
                    }

                    return true;
                }
            }

            public override int GetHashCode()
            {
                var first = name.GetHashCode();
                var second = pickedColors == null ? 0 : pickedColors.GetHashCode();

                return first ^ second; // Just in case.
            }
        }

        #region Inspector

        public Texture2D originalTexture;

        public List<MaskInfo> masks;

        public bool desaturate = false;

        [Range(20, 200)]
        public int previewHeight = 100;

        #endregion

#if UNITY_EDITOR

        static readonly Color InvisibleColor = new Color(0f, 0f, 0f, 0f);

        internal Texture2D GenerateMask(Texture2D texture, Texture2D reuseTexture, MaskInfo mask)
        {
            if (texture == null || string.IsNullOrEmpty(mask.name))
                return null;

            Texture2D t;
            bool useAlpha = false;

            if (reuseTexture == null)
            {
                t = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false);
            } else
            {
                t = reuseTexture;   
            }

            // Clear the texture.
            for (int y = 0; y < t.height; y++)
            {
                for (int x = 0; x < t.width; x++)
                {
                    t.SetPixel(x, y, InvisibleColor);
                }
            }

            foreach (var pickedColor in mask.pickedColors)
            {
                Color.RGBToHSV(pickedColor.color, out float originalH, out float originalS, out float originalV);

                for (int y = 0; y < t.height; y++)
                {
                    for (int x = 0; x < t.width; x++)
                    {
                        var color = texture.GetPixel(x, y);
                        var newColor = color;

                        Color.RGBToHSV(color, out float h, out float s, out float v);

                        var hCoeff = pickedColor.hCoeff;
                        var sCoeff = pickedColor.sCoeff;
                        var vCoeff = pickedColor.vCoeff;

                        var distance = Mathf.Pow((originalH - h) * hCoeff, 2) + Mathf.Pow((originalS - s) * sCoeff, 2) + Mathf.Pow((originalV - v) * vCoeff, 2);

                        if (distance <= Mathf.Pow(pickedColor.threshold, 2))
                        {
                            if (desaturate)
                            {
                                newColor = new Color(v, v, v, color.a);
                            }

                            t.SetPixel(x, y, newColor);
                        }                  
                    }
                }
            }

            t.Apply();

            return t;
        }

#endif
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(ExtractMaskAsset))]
    [CanEditMultipleObjects]
    public class CreateMaskAssetEditor : Editor
    {
        KeyValuePair<Texture2D, Texture2D> _originalTexture;

        readonly Dictionary<string, Texture2D> _generatedTextures = new Dictionary<string, Texture2D>();

        readonly List<ExtractMaskAsset.MaskInfo> _cachedMasks = new List<ExtractMaskAsset.MaskInfo>();

        bool _cachedDesaturate;

        int _cachedHeight;

        ExtractMaskAsset Asset => (ExtractMaskAsset)target;

        GUIStyle _separatorStyle;

        void OnEnable()
        {
            _originalTexture = default;
            _cachedDesaturate = false;
            _cachedHeight = -1;
            _cachedMasks.Clear();
            _generatedTextures.Clear();

            _separatorStyle = new GUIStyle();
            _separatorStyle.fontStyle = FontStyle.Bold;
            _separatorStyle.alignment = TextAnchor.MiddleCenter;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var asset = (ExtractMaskAsset)target;

            var texture = asset.originalTexture;

            if (texture != null)
            {
                if (_originalTexture.Key != texture || _cachedHeight != Asset.previewHeight)
                {
                    _originalTexture = new KeyValuePair<Texture2D, Texture2D>(texture, CommonUtils.Clone(texture, Asset.previewHeight));
                    ForceRegenerate();
                }

                if (IsSettingsChanged())
                {
                    GenerateNeededMasks();
                    MarkSettingsChanged();
                }

                var aspect = texture.width / (float)texture.height;
                var w = GUILayout.Width(Asset.previewHeight * aspect);
                var h = GUILayout.Height(Asset.previewHeight);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("=== Previews ===", _separatorStyle);
                EditorGUILayout.Space();

                GUILayout.Label("Preview:");
                GUILayout.Box(_originalTexture.Value, w, h);

                if (_generatedTextures.Count > 0)
                {
                    GUILayout.Label("Generated Masks:");
                    EditorGUILayout.BeginHorizontal();

                    foreach (var pair in _generatedTextures)
                    {
                        GUILayout.Box(pair.Value, w, h);
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();
                    if (GUILayout.Button("Save Masks"))
                    {
                        SaveTextures();
                    }
                }
            }
        }

        bool IsSettingsChanged()
        {
            if (_cachedDesaturate != Asset.desaturate || _cachedHeight != Asset.previewHeight)
            {
                ForceRegenerate();
                return true;
            }

            if (Asset.masks == null || _cachedMasks.Count != Asset.masks.Count)
            {
                return true;
            }

            // Values in masks have changed.
            foreach (var mask in Asset.masks)
            {
                if (!_cachedMasks.Exists((m) => m.Equals(mask)))
                {
                    return true;
                }
            }

            return false;
        }

        void GenerateNeededMasks()
        {
            var names = new List<string>();

            // Find all names and without doubles.
            names.AddRange(_generatedTextures.Keys);
            foreach (var n in Asset.masks.ConvertAll((m) => m.name))
            {
                if (!names.Contains(n))
                {
                    names.Add(n);
                }
            }
            names.RemoveAll(string.IsNullOrEmpty);

            // Regenerate only textures which settings are changed.
            for (int i = 0; i < names.Count; i++)
            {
                var name = names[i];
                var cachedMask = _cachedMasks.Find((m) => name.Equals(m.name));
                var originalMask = Asset.masks.Find((m) => name.Equals(m.name));
                var reuseTexture = (Texture2D) null;

                if (_generatedTextures.ContainsKey(name))
                {
                    reuseTexture = _generatedTextures[name];
                    _generatedTextures.Remove(name);
                }

                if (originalMask != null)
                {
                    if (reuseTexture == null || !originalMask.Equals(cachedMask))
                    {
                        _generatedTextures.Add(name, Asset.GenerateMask(_originalTexture.Value, reuseTexture, originalMask));
                    }
                    else
                    {
                        _generatedTextures.Add(name, reuseTexture);
                    }
                }
            }
        }

        void MarkSettingsChanged()
        {
            // Cache masks. Copy all of them, but only their values.

            _cachedMasks.Clear();
            foreach (var m in Asset.masks) {
                _cachedMasks.Add(new ExtractMaskAsset.MaskInfo(m));
            }

            // Cache other settings

            _cachedDesaturate = Asset.desaturate;
            _cachedHeight = Asset.previewHeight;
        }

        void ForceRegenerate()
        {
            _generatedTextures.Clear();
            _cachedMasks.Clear();
        }

        void SaveTextures()
        {
            if (Asset.originalTexture == null || Asset.masks == null)
                return;

            // Pick a folder.

            var path = AssetDatabase.GetAssetPath(Asset);

            var startIndex = path.IndexOf('/') + 1;
            var endIndex = path.LastIndexOf('/');

            var folder = path.Substring(startIndex, endIndex - startIndex);

            folder = EditorUtility.SaveFolderPanel("Select Save Folder", $"{Application.dataPath}/{folder}", "");

            // Save files into it.

            if (!string.IsNullOrEmpty(folder))
            {
                var originalTexture = CommonUtils.Clone(Asset.originalTexture);

                foreach (var m in Asset.masks)
                {
                    var generatedTexture = Asset.GenerateMask(originalTexture, null, m);

                    System.IO.File.WriteAllBytes(
                        $"{folder}/{Asset.originalTexture.name}_{m.name}.png",
                        generatedTexture.EncodeToPNG()
                        );
                }

                AssetDatabase.Refresh();
            }
        }

    }

#endif
}
