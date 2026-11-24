using UnityEngine;
using UnityEditor;
using System.IO;

public static class SetupUITextures
{
    [MenuItem("VR Assignment/Configure UI Sprites and Icons")]
    public static void ConfigureTextures()
    {
        string[] textureFiles = new string[]
        {
            "Assets/Textures/UI/Menu_Header_Banner.jpg",
            "Assets/Textures/UI/Menu_Panel_Frame.jpg",
            "Assets/Textures/UI/Icon_Play_Tutorial.jpg",
            "Assets/Textures/UI/Icon_Free_Play.jpg",
            "Assets/Textures/UI/Icon_Reset_Cube.jpg",
            "Assets/Textures/UI/Icon_Controls_Help.jpg",
            "Assets/Textures/UI/Icon_Exit_Quit.jpg",
            "Assets/Resources/UI/Menu_Header_Banner.jpg",
            "Assets/Resources/UI/Menu_Panel_Frame.jpg",
            "Assets/Resources/UI/Icon_Play_Tutorial.jpg",
            "Assets/Resources/UI/Icon_Free_Play.jpg",
            "Assets/Resources/UI/Icon_Reset_Cube.jpg",
            "Assets/Resources/UI/Icon_Controls_Help.jpg",
            "Assets/Resources/UI/Icon_Exit_Quit.jpg",
            "Assets/Resources/UI/Step_Badge.png",
            "Assets/Resources/UI/ProgressBar_Track.png",
            "Assets/Resources/UI/ProgressBar_Fill.png",
            "Assets/Resources/UI/Status_Box.png",
            "Assets/Resources/UI/Panel_Background.png"
        };

        bool changed = false;
        foreach (var path in textureFiles)
        {
            if (File.Exists(path))
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.alphaIsTransparency = true;
                        importer.SaveAndReimport();
                        changed = true;
                        Debug.Log($"[UI Setup] Converted to Sprite: {path}");
                    }
                }
            }
        }

        if (changed)
        {
            AssetDatabase.Refresh();
        }
    }
}
