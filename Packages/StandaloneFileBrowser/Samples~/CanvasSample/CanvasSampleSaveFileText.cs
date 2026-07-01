using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SFB;

[RequireComponent(typeof(Button))]
public class CanvasSampleSaveFileText : MonoBehaviour, IPointerDownHandler {
    public Text output;

    // Sample text data
    private string _data = "Example text created by StandaloneFileBrowser";

#if UNITY_WEBGL
    // Browser plugin should be called in OnPointerDown.
    public void OnPointerDown(PointerEventData eventData) {
        var bytes = Encoding.UTF8.GetBytes(_data);
        StandaloneFileBrowserWebGL.SaveFileAsync("sample.txt", bytes, OnFileSaved);
    }

    private void OnFileSaved(WebGLSaveResult result) {
        if (result.Status == WebGLSaveStatus.Success) {
            output.text = "File successfully downloaded";
            return;
        }

        if (result.Status == WebGLSaveStatus.Busy) {
            output.text = "Another save request is already in progress";
            return;
        }

        if (result.Status == WebGLSaveStatus.BlockedByBrowser) {
            output.text = string.IsNullOrEmpty(result.ErrorMessage) ? "Browser blocked the download request" : result.ErrorMessage;
            return;
        }

        output.text = string.IsNullOrEmpty(result.ErrorMessage) ? "Browser file save failed" : result.ErrorMessage;
    }
#else
    //
    // Standalone platforms & editor
    //
    public void OnPointerDown(PointerEventData eventData) { }

    // Listen OnClick event in standlone builds
    void Start() {
        var button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    public void OnClick() {
        var path = StandaloneFileBrowser.SaveFilePanel("Title", "", "sample", "txt");
        if (!string.IsNullOrEmpty(path)) {
            File.WriteAllText(path, _data);
        }
    }
#endif
}
