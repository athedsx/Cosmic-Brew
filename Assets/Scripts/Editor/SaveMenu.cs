using System.IO;
using UnityEditor;
using UnityEngine;

// Editor menu for testing: delete / open the Cosmic Brew save
public static class SaveMenu
{
    [MenuItem("Cosmic Brew/Delete Save")]
    static void DeleteSave()
    {
        if (!File.Exists(SaveSystem.SavePath)) { Debug.Log("[Save] No save to delete."); return; }
        if (!EditorUtility.DisplayDialog("Apagar save", "Apagar o progresso salvo (moedas e decorações)?", "Apagar", "Cancelar")) return;
        File.Delete(SaveSystem.SavePath);
        Debug.Log("[Save] Save deleted: " + SaveSystem.SavePath);
    }

    [MenuItem("Cosmic Brew/Open Save Folder")]
    static void OpenFolder() => EditorUtility.RevealInFinder(SaveSystem.SavePath);
}
