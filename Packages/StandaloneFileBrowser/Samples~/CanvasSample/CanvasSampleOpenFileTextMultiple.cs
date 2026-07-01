using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using SFB;

[RequireComponent(typeof(Button))]
public class CanvasSampleOpenFileTextMultiple : MonoBehaviour, IPointerDownHandler {
    public Text output;

#if UNITY_WEBGL
    public void OnPointerDown(PointerEventData eventData) {
        StandaloneFileBrowserWebGL.OpenFilePanelAsync("txt", true, OnFilesSelected);
    }

    private void OnFilesSelected(WebGLFileSelectionResult result) {
        if (result.Status == WebGLFileSelectionStatus.Success && result.Files.Length > 0) {
            StartCoroutine(OutputRoutine(result.Files));
            return;
        }

        if (result.Status == WebGLFileSelectionStatus.Cancelled) {
            output.text = "File selection cancelled";
            return;
        }

        if (result.Status == WebGLFileSelectionStatus.Busy) {
            output.text = "Another file-open request is already in progress";
            return;
        }

        output.text = string.IsNullOrEmpty(result.ErrorMessage) ? "Browser file open failed" : result.ErrorMessage;
    }

        private IEnumerator OutputRoutine(WebGLFileReference[] files) {
        var outputText = "";
        for (int i = 0; i < files.Length; i++)
        {
            var file = files[i];
            UnityWebRequest www = UnityWebRequest.Get(file.ObjectUrl);
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(www.error);
            }
            else
            {
                outputText += www.downloadHandler.text;
            }

            StandaloneFileBrowserWebGL.Release(file);
        }
        output.text = outputText;
    }
#else
    //
    // Standalone platforms & editor
    //
    public void OnPointerDown(PointerEventData eventData) { }

    void Start() {
        var button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnClick() {
        var paths = StandaloneFileBrowser.OpenFilePanel("Open File", "", "txt", true);
        if (paths.Length > 0) {
            var urlArr = new List<string>(paths.Length);
            for (int i = 0; i < paths.Length; i++) {
                urlArr.Add(new System.Uri(paths[i]).AbsoluteUri);
            }
            StartCoroutine(OutputRoutine(urlArr.ToArray()));
        }
    }
#endif

    private IEnumerator OutputRoutine(string[] urlArr) {
        var outputText = "";
        for (int i = 0; i < urlArr.Length; i++)
        {
            UnityWebRequest www = UnityWebRequest.Get(urlArr[i]);
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(www.error);
            }
            else
            {
                outputText += www.downloadHandler.text;
            }
        }
        output.text = outputText;
    }
}
