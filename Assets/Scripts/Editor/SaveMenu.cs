using System.IO;
using UnityEditor;
using UnityEngine;

// Menu do editor para testes: apagar / abrir o save do Cosmic Brew
public static class SaveMenu
{
    [MenuItem("Cosmic Brew/Apagar save")]
    static void DeleteSave()
    {
        if (!File.Exists(SaveSystem.SavePath)) { Debug.Log("[Save] Não há save para apagar."); return; }
        if (!EditorUtility.DisplayDialog("Apagar save", "Apagar o progresso salvo (moedas e decorações)?", "Apagar", "Cancelar")) return;
        File.Delete(SaveSystem.SavePath);
        Debug.Log("[Save] Save apagado: " + SaveSystem.SavePath);
    }

    [MenuItem("Cosmic Brew/Abrir pasta do save")]
    static void OpenFolder() => EditorUtility.RevealInFinder(SaveSystem.SavePath);
}
