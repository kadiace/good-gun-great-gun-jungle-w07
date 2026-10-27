#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WeaponWorkshop;

[InitializeOnLoad]
internal static class RemoveCustomInvestigation
{
    const string Request = "Library/RemoveCustomInvestigation.request";
    const string Result = "Library/RemoveCustomInvestigation.txt";
    static RemoveCustomInvestigation() { EditorApplication.update += Poll; }
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
    static object Field(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        var report = new StringBuilder(command + "\n");
        try
        {
            report.AppendLine("playing=" + EditorApplication.isPlaying + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            if (command == "play") { EditorApplication.isPlaying = true; }
            else if (command == "stop") { EditorApplication.isPlaying = false; }
            else if (command == "setup")
            {
                if (!Application.isPlaying) throw new Exception("Play mode required");
                var tutorial = Find<TutorialManager>();
                if (tutorial != null) UnityEngine.Object.DestroyImmediate(tutorial.gameObject);
                foreach (var skip in UnityEngine.Object.FindObjectsByType<TutorialSkipUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) skip.enabled = false;
                var feedback = Find<FeedbackUI>(); if (feedback.IsChoiceOpen) feedback.Cancel();
                GameManager.Instance.SetDialogue(false); GameManager.Instance.Resume();
                var session = Find<WorkshopSession>(); var bench = Find<Workbench>();
                if (bench.CurrentCore == null) bench.SpawnCore();
                report.AppendLine("enter=" + session.Enter(bench, false));
                var builder = session.WorkshopCamera.GetComponent<CubeBuilder>();
                report.AppendLine("beginCustom=" + builder.BeginCustomMode());
            }
            else
            {
                var palette = Find<ModulePaletteUI>();
                var button = (Button)Field(palette, "customRemoveButton");
                var builder = (CubeBuilder)Field(palette, "builder");
                report.AppendLine("builder=" + (builder != null) + " custom=" + (builder != null && builder.IsCustomMode) + " blocked=" + GameManager.GameplayBlocked);
                report.AppendLine("button=" + PathOf(button.transform) + " active=" + button.gameObject.activeInHierarchy + " interactable=" + button.IsInteractable());
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)button.transform;
                var center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                report.AppendLine("screen=" + Screen.width + "x" + Screen.height + " buttonCenter=" + center);
                if (EventSystem.current != null)
                {
                    var data = new PointerEventData(EventSystem.current) { position = center, button = PointerEventData.InputButton.Left };
                    var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
                    foreach (var hit in hits.Take(12)) report.AppendLine("hit=" + PathOf(hit.gameObject.transform));
                    if (command == "click")
                    {
                        if (hits.Count == 0) throw new Exception("No raycast hit");
                        var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                        report.AppendLine("clickTarget=" + (target == null ? "none" : PathOf(target.transform)));
                        if (target != null) ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
                    }
                }
                if (command == "invoke") button.onClick.Invoke();
                if (command == "accept") Call(Find<FeedbackUI>(), "Confirm");
                report.AppendLine("choiceOpen=" + Find<FeedbackUI>().IsChoiceOpen + " customAfter=" + (builder != null && builder.IsCustomMode));
            }
        }
        catch (Exception e) { report.AppendLine(e.ToString()); }
        File.WriteAllText(Result, report.ToString());
    }
}
#endif
