using System;
using System.IO;
using MysteryGame.Core;
using UnityEngine;

/// <summary>
/// One save slot on disk (Application.persistentDataPath/save.json) holding
/// the whole GameState snapshot. Written automatically on every room entry
/// and on F5; read by "เล่นต่อ" on the title menu and by F9.
/// </summary>
public static class SaveSystem
{
    public const int Version = 1;
    public static bool PersistenceEnabled { get; set; } = true;
    private const string FileName = "save.json";

    [Serializable]
    private class SaveFile
    {
        public int version = Version;
        public string savedAt;
        public StateSnapshot state;
    }

    public static string SavePath
    {
        get
        {
            var game = GameDefinition.Current;
            string id = game != null ? game.gameId : "mystery";
            if (string.IsNullOrWhiteSpace(id) || id == "mystery")
                return Path.Combine(Application.persistentDataPath, FileName); // existing saves
            string safeId = System.Text.RegularExpressions.Regex.Replace(id, "[^a-zA-Z0-9_-]", "_");
            return Path.Combine(Application.persistentDataPath, safeId + "_" + FileName);
        }
    }

    public static bool HasSave
    {
        get { return File.Exists(SavePath); }
    }

    /// <summary>When the save on disk was written, for the title menu.</summary>
    public static string LastSavedAt()
    {
        SaveFile file = Read();
        return file != null ? file.savedAt : string.Empty;
    }

    public static bool Save()
    {
        if (!PersistenceEnabled) return false;
        if (GameState.Instance == null)
        {
            return false;
        }

        try
        {
            SaveFile file = new SaveFile
            {
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                state = GameState.Instance.CreateSnapshot(),
            };
            File.WriteAllText(SavePath, JsonUtility.ToJson(file, true));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Could not write the save file: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Restores the saved state and moves to the saved room. Returns false,
    /// changing nothing, when there is no readable save.
    /// </summary>
    public static bool Load()
    {
        SaveFile file = Read();
        if (file == null || file.state == null || GameState.Instance == null)
        {
            return false;
        }

        GameState.Instance.RestoreSnapshot(file.state);
        string scene = string.IsNullOrWhiteSpace(file.state.CurrentSceneId)
            ? GameSession.CreateDefault().CurrentSceneId
            : file.state.CurrentSceneId;
        RoomTransitionManager.Instance.TransitionToRoom(
            scene,
            "กำลังกลับไปยังจุดที่บันทึกไว้...");
        return true;
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Could not delete the save file: " + ex.Message);
        }
    }

    private static SaveFile Read()
    {
        try
        {
            if (!File.Exists(SavePath))
            {
                return null;
            }

            SaveFile file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(SavePath));
            if (file == null || file.version > Version)
            {
                return null;
            }

            return file;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Save file is unreadable: " + ex.Message);
            return null;
        }
    }
}
