using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;


public class TextureToolbox : EditorWindow
{
    private string _inputFolder = "Assets/Textures";
    private Vector2 _scroll;

    // operations
    private bool _doResize;
    private int _targetWidth = 1024;
    private int _targetHeight = 1024;

    private bool _doReformat;
    private TextureImporterFormat _targetFormat = TextureImporterFormat.DXT5;

    private bool _doChannelPack;
    private Texture2D _channelR, _channelG, _channelB, _channelA;

    private bool _doMakePOT;

    private bool _doMipmaps;
    private bool _mipmapsEnabled = true;

    // atlas
    private bool _doAtlas;
    private int _atlasMaxSize = 2048;
    private int _atlasPadding = 2;
    private List<Texture2D> _atlasTextures = new List<Texture2D>();

    // log
    private List<string> _log = new List<string>();

    [MenuItem("Tools/Texture Toolbox")]
    static void Open()
    {
        GetWindow<TextureToolbox>("Texture Toolbox");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Texture Toolbox", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        // input folder with picker
        EditorGUILayout.BeginHorizontal();
        _inputFolder = EditorGUILayout.TextField("Input Folder", _inputFolder);
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string picked = EditorUtility.OpenFolderPanel("Select Input Folder", "Assets", "");
            if (!string.IsNullOrEmpty(picked))
            {
                if (picked.StartsWith(Application.dataPath))
                    _inputFolder = "Assets" + picked.Substring(Application.dataPath.Length);
                else
                    Debug.LogWarning("Folder must be inside the Assets directory.");
            }
        }
        // drag-drop folder object
        var inputObj = EditorGUILayout.ObjectField(GUIContent.none, AssetDatabase.LoadAssetAtPath<DefaultAsset>(_inputFolder), typeof(DefaultAsset), false, GUILayout.Width(50));
        if (inputObj != null)
        {
            string p = AssetDatabase.GetAssetPath(inputObj);
            if (AssetDatabase.IsValidFolder(p)) _inputFolder = p;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Operations", EditorStyles.boldLabel);

        // resize
        _doResize = EditorGUILayout.ToggleLeft("Resize", _doResize);
        if (_doResize)
        {
            EditorGUI.indentLevel++;
            _targetWidth = EditorGUILayout.IntField("Width", _targetWidth);
            _targetHeight = EditorGUILayout.IntField("Height", _targetHeight);
            EditorGUI.indentLevel--;
        }

        // reformat
        _doReformat = EditorGUILayout.ToggleLeft("Change Format", _doReformat);
        if (_doReformat)
        {
            EditorGUI.indentLevel++;
            _targetFormat = (TextureImporterFormat)EditorGUILayout.EnumPopup("Format", _targetFormat);
            EditorGUI.indentLevel--;
        }

        // pad to POT
        _doMakePOT = EditorGUILayout.ToggleLeft("Pad to Power of Two", _doMakePOT);

        // mipmaps
        _doMipmaps = EditorGUILayout.ToggleLeft("Set Mipmaps", _doMipmaps);
        if (_doMipmaps)
        {
            EditorGUI.indentLevel++;
            _mipmapsEnabled = EditorGUILayout.Toggle("Generate Mipmaps", _mipmapsEnabled);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8);

        // channel packing (separate section)
        EditorGUILayout.LabelField("Channel Packing", EditorStyles.boldLabel);
        _doChannelPack = EditorGUILayout.ToggleLeft("Enable Channel Pack", _doChannelPack);
        if (_doChannelPack)
        {
            EditorGUI.indentLevel++;
            _channelR = (Texture2D)EditorGUILayout.ObjectField("R Channel", _channelR, typeof(Texture2D), false);
            _channelG = (Texture2D)EditorGUILayout.ObjectField("G Channel", _channelG, typeof(Texture2D), false);
            _channelB = (Texture2D)EditorGUILayout.ObjectField("B Channel", _channelB, typeof(Texture2D), false);
            _channelA = (Texture2D)EditorGUILayout.ObjectField("A Channel", _channelA, typeof(Texture2D), false);
            EditorGUI.indentLevel--;

            if (GUILayout.Button("Pack Channels"))
                DoChannelPack();
        }

        EditorGUILayout.Space(8);

        // texture atlas
        EditorGUILayout.LabelField("Texture Atlas", EditorStyles.boldLabel);
        _doAtlas = EditorGUILayout.ToggleLeft("Build Atlas from Folder", _doAtlas);
        if (_doAtlas)
        {
            EditorGUI.indentLevel++;
            _atlasMaxSize = EditorGUILayout.IntPopup("Max Atlas Size",
                _atlasMaxSize, new[] { "512", "1024", "2048", "4096", "8192" },
                new[] { 512, 1024, 2048, 4096, 8192 });
            _atlasPadding = EditorGUILayout.IntSlider("Padding", _atlasPadding, 0, 8);
            EditorGUI.indentLevel--;

            if (GUILayout.Button("Build Atlas"))
                BuildAtlas();
        }

        EditorGUILayout.Space(8);

        GUI.enabled = !string.IsNullOrEmpty(_inputFolder);
        if (GUILayout.Button("Process All Textures in Folder", GUILayout.Height(30)))
            ProcessFolder();
        GUI.enabled = true;

        // log
        if (_log.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(150));
            foreach (var msg in _log)
                EditorGUILayout.LabelField(msg, EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }
    }

    void ProcessFolder()
    {
        _log.Clear();

        if (!AssetDatabase.IsValidFolder(_inputFolder))
        {
            _log.Add("ERROR: Input folder doesn't exist.");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { _inputFolder });
        _log.Add($"Found {guids.Length} textures.");

        int processed = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;

            bool changed = false;

            // resize via importer
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            if (_doResize)
            {
                int maxDim = Mathf.Max(_targetWidth, _targetHeight);
                importer.maxTextureSize = NearestPOT(maxDim);
                changed = true;
            }

            if (_doReformat)
            {
                var settings = importer.GetDefaultPlatformTextureSettings();
                settings.overridden = true;
                settings.format = _targetFormat;
                importer.SetPlatformTextureSettings(settings);
                changed = true;
            }

            if (_doMakePOT)
            {
                importer.npotScale = TextureImporterNPOTScale.ToLarger;
                changed = true;
            }

            if (_doMipmaps)
            {
                importer.mipmapEnabled = _mipmapsEnabled;
                if (_mipmapsEnabled)
                    importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
                changed = true;
                _log.Add($"  Mipmaps {(_mipmapsEnabled ? "ON" : "OFF")}: {Path.GetFileName(path)} (was: {importer.mipmapEnabled})");
            }

            if (changed)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                _log.Add($"  Processed: {Path.GetFileName(path)}");
                processed++;
            }
        }

        _log.Add($"Done! Processed {processed} textures.");
        AssetDatabase.Refresh();
    }

    void DoChannelPack()
    {
        int w = 512, h = 512;
        // use the largest texture size
        foreach (var tex in new[] { _channelR, _channelG, _channelB, _channelA })
        {
            if (tex != null) { w = Mathf.Max(w, tex.width); h = Mathf.Max(h, tex.height); }
        }

        var result = new Texture2D(w, h, TextureFormat.RGBA32, false);

        var rPx = GetReadable(_channelR, w, h);
        var gPx = GetReadable(_channelG, w, h);
        var bPx = GetReadable(_channelB, w, h);
        var aPx = GetReadable(_channelA, w, h);

        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(
                rPx != null ? rPx[i].r : 0f,
                gPx != null ? gPx[i].g : 0f,
                bPx != null ? bPx[i].b : 0f,
                aPx != null ? aPx[i].a : 1f
            );
        }

        result.SetPixels(pixels);
        result.Apply();

        string path = EditorUtility.SaveFilePanelInProject("Save Packed Texture", "Packed", "png", "Save");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllBytes(path, result.EncodeToPNG());
            AssetDatabase.Refresh();
            _log.Add($"Saved channel-packed texture to {path}");
        }

        DestroyImmediate(result);
    }

    /// <summary>
    /// Get pixel array from a texture, making a readable copy via RenderTexture if needed.
    /// </summary>
    Color[] GetReadable(Texture2D tex, int w, int h)
    {
        if (tex == null) return null;

        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var readable = new Texture2D(w, h, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        readable.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var pixels = readable.GetPixels();
        DestroyImmediate(readable);
        return pixels;
    }

    void BuildAtlas()
    {
        _log.Clear();

        if (!AssetDatabase.IsValidFolder(_inputFolder))
        {
            _log.Add("ERROR: Input folder doesn't exist.");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { _inputFolder });
        if (guids.Length == 0)
        {
            _log.Add("No textures found in input folder.");
            return;
        }

        // create readable copies of all textures via RenderTexture blit
        // this avoids toggling the isReadable importer flag entirely
        var readableCopies = new List<Texture2D>();
        var names = new List<string>();

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var srcTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (srcTex == null) continue;

            // blit into a readable Texture2D copy
            int w = srcTex.width;
            int h = srcTex.height;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(srcTex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            readableCopies.Add(copy);
            names.Add(Path.GetFileNameWithoutExtension(path));
        }

        if (readableCopies.Count == 0)
        {
            _log.Add("No valid textures to pack.");
            return;
        }

        _log.Add($"Packing {readableCopies.Count} textures into atlas...");

        // pack the readable copies
        var atlas = new Texture2D(_atlasMaxSize, _atlasMaxSize, TextureFormat.RGBA32, false);
        var rects = atlas.PackTextures(readableCopies.ToArray(), _atlasPadding, _atlasMaxSize);

        // clean up temp copies
        foreach (var copy in readableCopies)
            DestroyImmediate(copy);

        if (rects == null)
        {
            _log.Add("ERROR: Atlas packing failed. Textures may be too large for the max atlas size.");
            DestroyImmediate(atlas);
            return;
        }

        // save atlas
        string savePath = EditorUtility.SaveFilePanelInProject("Save Atlas", "Atlas", "png", "Save atlas");
        if (!string.IsNullOrEmpty(savePath))
        {
            // write PNG
            File.WriteAllBytes(savePath, atlas.EncodeToPNG());

            // write JSON sidecar with UV mapping data
            string jsonPath = savePath.Replace(".png", ".json");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"atlas\": \"{Path.GetFileName(savePath)}\",");
            sb.AppendLine($"  \"width\": {atlas.width},");
            sb.AppendLine($"  \"height\": {atlas.height},");
            sb.AppendLine($"  \"sprites\": [");
            for (int i = 0; i < rects.Length; i++)
            {
                var r = rects[i];
                // pixel coords (easier to use in most engines/tools)
                int px = Mathf.RoundToInt(r.x * atlas.width);
                int py = Mathf.RoundToInt(r.y * atlas.height);
                int pw = Mathf.RoundToInt(r.width * atlas.width);
                int ph = Mathf.RoundToInt(r.height * atlas.height);
                bool last = (i == rects.Length - 1);
                sb.AppendLine($"    {{");
                sb.AppendLine($"      \"name\": \"{names[i]}\",");
                sb.AppendLine($"      \"uvRect\": {{ \"x\": {r.x:F6}, \"y\": {r.y:F6}, \"w\": {r.width:F6}, \"h\": {r.height:F6} }},");
                sb.AppendLine($"      \"pixelRect\": {{ \"x\": {px}, \"y\": {py}, \"w\": {pw}, \"h\": {ph} }}");
                sb.AppendLine(last ? $"    }}" : $"    }},");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(jsonPath, sb.ToString());

            AssetDatabase.Refresh();

            _log.Add($"Atlas saved: {savePath} ({atlas.width}x{atlas.height})");
            _log.Add($"JSON saved:  {jsonPath}");
            _log.Add($"Packed {names.Count} textures.");

            for (int i = 0; i < rects.Length; i++)
            {
                var r = rects[i];
                _log.Add($"  {names[i]}: x={r.x:F3} y={r.y:F3} w={r.width:F3} h={r.height:F3}");
            }
        }

        DestroyImmediate(atlas);
    }

    int NearestPOT(int value)
    {
        int pot = 1;
        while (pot < value) pot *= 2;
        return Mathf.Min(pot, 8192);
    }
}
