using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AmbientNpcProfileBuilder
{
    private enum DirectionKey
    {
        Down,
        Up,
        Left,
        Right
    }

    private sealed class DirectionSprites
    {
        public Sprite idle;
        public readonly List<OrderedSprite> walkFrames = new List<OrderedSprite>();
    }

    private readonly struct OrderedSprite
    {
        public OrderedSprite(Sprite sprite, int order)
        {
            this.sprite = sprite;
            this.order = order;
        }

        public Sprite sprite { get; }
        public int order { get; }
    }

    [MenuItem("Tools/NPCs/Create Ambient NPC Profile From Selected Folder")]
    private static void CreateProfileFromSelectedFolder()
    {
        string folderPath = ResolveSelectedFolderPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            EditorUtility.DisplayDialog("Ambient NPC Profile", "Select an NPC sprite folder in the Project window first.", "OK");
            return;
        }

        AmbientNpcVisualProfile profile = CreateOrUpdateProfile(folderPath);
        if (profile == null)
            return;

        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
    }

    [MenuItem("Tools/NPCs/Create Ambient NPC Profile From Selected Folder", true)]
    private static bool CanCreateProfileFromSelectedFolder()
    {
        return !string.IsNullOrWhiteSpace(ResolveSelectedFolderPath());
    }

    public static AmbientNpcVisualProfile CreateOrUpdateProfile(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError($"[AmbientNpcProfileBuilder] Invalid folder path: {folderPath}");
            return null;
        }

        Dictionary<DirectionKey, DirectionSprites> spriteMap = BuildSpriteMap(folderPath);
        if (spriteMap.Values.All(entry => entry.idle == null && entry.walkFrames.Count == 0))
        {
            Debug.LogWarning($"[AmbientNpcProfileBuilder] No sprites found in folder: {folderPath}");
            return null;
        }

        string folderName = Path.GetFileName(folderPath.TrimEnd('/', '\\'));
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{folderName}_AmbientNpcVisualProfile.asset");

        AmbientNpcVisualProfile profile = AssetDatabase.LoadAssetAtPath<AmbientNpcVisualProfile>(assetPath);
        if (profile == null)
        {
            string existingAssetPath = $"{folderPath}/{folderName}_AmbientNpcVisualProfile.asset";
            profile = AssetDatabase.LoadAssetAtPath<AmbientNpcVisualProfile>(existingAssetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<AmbientNpcVisualProfile>();
                AssetDatabase.CreateAsset(profile, existingAssetPath);
            }
        }

        profile.down = BuildAnimation(spriteMap[DirectionKey.Down]);
        profile.up = BuildAnimation(spriteMap[DirectionKey.Up]);
        profile.left = BuildAnimation(spriteMap[DirectionKey.Left]);
        profile.right = BuildAnimation(spriteMap[DirectionKey.Right]);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AmbientNpcProfileBuilder] Profile ready: {AssetDatabase.GetAssetPath(profile)}");
        return profile;
    }

    private static AmbientNpcVisualProfile.DirectionalSpriteAnimation BuildAnimation(DirectionSprites sprites)
    {
        AmbientNpcVisualProfile.DirectionalSpriteAnimation animation = new AmbientNpcVisualProfile.DirectionalSpriteAnimation();
        animation.idle = sprites.idle;
        animation.walkFrames = sprites.walkFrames
            .OrderBy(entry => entry.order)
            .Select(entry => entry.sprite)
            .Where(sprite => sprite != null)
            .ToArray();
        return animation;
    }

    private static Dictionary<DirectionKey, DirectionSprites> BuildSpriteMap(string folderPath)
    {
        Dictionary<DirectionKey, DirectionSprites> spriteMap = new Dictionary<DirectionKey, DirectionSprites>
        {
            [DirectionKey.Down] = new DirectionSprites(),
            [DirectionKey.Up] = new DirectionSprites(),
            [DirectionKey.Left] = new DirectionSprites(),
            [DirectionKey.Right] = new DirectionSprites()
        };

        string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        for (int index = 0; index < spriteGuids.Length; index++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(spriteGuids[index]);
            if (string.IsNullOrWhiteSpace(assetPath) || assetPath.Contains("/Materials/", StringComparison.OrdinalIgnoreCase))
                continue;

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
                continue;

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (!TryResolveDirection(fileName, out DirectionKey direction))
                continue;

            DirectionSprites target = spriteMap[direction];
            if (IsWalkFrame(fileName))
            {
                target.walkFrames.Add(new OrderedSprite(sprite, ExtractSortOrder(fileName)));
            }
            else if (target.idle == null)
            {
                target.idle = sprite;
            }
        }

        return spriteMap;
    }

    private static bool TryResolveDirection(string fileName, out DirectionKey direction)
    {
        string normalized = fileName.ToLowerInvariant();

        if (normalized.Contains("front") || normalized.Contains("down"))
        {
            direction = DirectionKey.Down;
            return true;
        }

        if (normalized.Contains("back") || normalized.Contains("up"))
        {
            direction = DirectionKey.Up;
            return true;
        }

        if (normalized.Contains("left"))
        {
            direction = DirectionKey.Left;
            return true;
        }

        if (normalized.Contains("right"))
        {
            direction = DirectionKey.Right;
            return true;
        }

        direction = DirectionKey.Down;
        return false;
    }

    private static bool IsWalkFrame(string fileName)
    {
        string normalized = fileName.ToLowerInvariant();
        if (normalized.Contains("walk"))
            return true;

        for (int index = normalized.Length - 1; index >= 0; index--)
        {
            if (!char.IsDigit(normalized[index]))
                break;

            return true;
        }

        return false;
    }

    private static int ExtractSortOrder(string fileName)
    {
        int multiplier = 1;
        int value = 0;

        for (int index = fileName.Length - 1; index >= 0; index--)
        {
            if (!char.IsDigit(fileName[index]))
                break;

            value += (fileName[index] - '0') * multiplier;
            multiplier *= 10;
        }

        return value;
    }

    private static string ResolveSelectedFolderPath()
    {
        UnityEngine.Object selectedObject = Selection.activeObject;
        if (selectedObject == null)
            return null;

        string assetPath = AssetDatabase.GetAssetPath(selectedObject);
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        if (AssetDatabase.IsValidFolder(assetPath))
            return assetPath;

        string directory = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        return directory.Replace('\\', '/');
    }
}