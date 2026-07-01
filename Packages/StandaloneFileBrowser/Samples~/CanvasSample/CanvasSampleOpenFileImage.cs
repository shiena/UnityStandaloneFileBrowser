using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using SFB;

[RequireComponent(typeof(Button))]
public class CanvasSampleOpenFileImage : MonoBehaviour, IPointerDownHandler {
    public RawImage output;

    private static readonly ExtensionFilter[] ImageExtensions = {
        new ExtensionFilter("Image Files", "png", "jpg", "jpeg")
    };

#if UNITY_WEBGL
    public void OnPointerDown(PointerEventData eventData) {
        StandaloneFileBrowserWebGL.OpenFilePanelAsync(ImageExtensions, false, OnFilesSelected);
    }

    private void OnFilesSelected(WebGLFileSelectionResult result) {
        if (result.Status == WebGLFileSelectionStatus.Success && result.Files.Length > 0) {
            var file = result.Files[0];
            Debug.Log($"File selected: {file.Name} , {file.Size} bytes , {file.MimeType} , {DateTimeOffset.FromUnixTimeMilliseconds(file.LastModifiedUnixMs)}");
            StartCoroutine(OutputRoutine(file.ObjectUrl, delegate {
                StandaloneFileBrowserWebGL.Release(file);
            }));
            return;
        }

        if (result.Status == WebGLFileSelectionStatus.Cancelled) {
            Debug.Log("File selection cancelled");
            return;
        }

        if (result.Status == WebGLFileSelectionStatus.Busy) {
            Debug.LogWarning("Another file-open request is already in progress");
            return;
        }

        Debug.LogError(string.IsNullOrEmpty(result.ErrorMessage) ? "Browser file open failed" : result.ErrorMessage);
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
        var paths = StandaloneFileBrowser.OpenFilePanel("Title", "", ImageExtensions, false);
        if (paths.Length > 0) {
            StartCoroutine(OutputRoutine(new System.Uri(paths[0]).AbsoluteUri));
        }
    }
#endif

    private IEnumerator OutputRoutine(string url, System.Action onCompleted = null) {
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(www.error);
        }
        else
        {
            output.texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
        }

        if (onCompleted != null) {
            onCompleted();
        }
    }
}
