using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SFB;

[RequireComponent(typeof(Button))]
public class CanvasSampleSaveFileImage : MonoBehaviour, IPointerDownHandler {
    public Text output;

    private byte[] _textureBytes;

    void Awake() {
        // Create red texture
        var width = 100;
        var height = 100;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        for (int i = 0; i < width; i++) {
            for (int j = 0; j < height; j++) {
                tex.SetPixel(i, j, Color.red);
            }
        }
        tex.Apply();
        _textureBytes = tex.EncodeToPNG();
        UnityEngine.Object.Destroy(tex);
    }

#if UNITY_WEBGL
    // Browser plugin should be called in OnPointerDown.
    public void OnPointerDown(PointerEventData eventData) {
        StandaloneFileBrowserWebGL.SaveFileAsync("sample.png", _textureBytes, "image/png", OnFileSaved);
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
        var path = StandaloneFileBrowser.SaveFilePanel("Title", "", "sample", "png");
        if (!string.IsNullOrEmpty(path)) {
            File.WriteAllBytes(path, _textureBytes);
        }
    }
#endif
}
