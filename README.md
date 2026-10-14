# Unity Standalone File Browser
[![openupm](https://img.shields.io/npm/v/io.github.gkngkc.unity-standalone-file-browser?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/io.github.gkngkc.unity-standalone-file-browser/)

A simple wrapper for native file dialogs on Windows/Mac/Linux, plus a dedicated WebGL browser file API.

- Works in editor and runtime.
- Open file/folder, save file dialogs supported.
- Multiple file selection.
- File extension filter.
- Mono/IL2CPP backends supported.
- Linux support by [Ricardo Rodrigues](https://github.com/RicardoEPRodrigues).
- Dedicated WebGL browser file API.
- Merged
  - https://github.com/gkngkc/UnityStandaloneFileBrowser/pull/76
  - https://github.com/gkngkc/UnityStandaloneFileBrowser/pull/91
  - https://github.com/gkngkc/UnityStandaloneFileBrowser/pull/97
  - https://github.com/gkngkc/UnityStandaloneFileBrowser/pull/100
  - https://github.com/gkngkc/UnityStandaloneFileBrowser/pull/106
  - https://github.com/gkngkc/UnityStandaloneFileBrowser/pull/116 
  - https://github.com/gkngkc/UnityStandaloneFileBrowser/pull/127
  - https://github.com/gkngkc/UnityStandaloneFileBrowser/pull/137
  - https://github.com/gkngkc/UnityStandaloneFileBrowser/issues/135#issuecomment-1987555796
  - https://github.com/gkngkc/UnityStandaloneFileBrowser/pull/146 

## Installation

<details>
<summary>Add from OpenUPM <em>| via scoped registry, recommended</em></summary>

To add OpenUPM to your project:

- open `Edit/Project Settings/Package Manager`
- add a new Scoped Registry:
```
Name: OpenUPM
URL:  https://package.openupm.com/
Scope(s): io.github.gkngkc.unity-standalone-file-browser
```
- click <kbd>Save</kbd>
- open Package Manager
- Select ``My Registries`` in dropdown top left
- Select ``Unity Standalone File Browser`` and click ``Install``
</details>

<details>
<summary>Add from GitHub | <em>not recommended, no updates through PackMan</em></summary>

You can also add it directly from GitHub on Unity 2019.4+. Note that you won't be able to receive updates through Package Manager this way, you'll have to update manually.

- open Package Manager
- click <kbd>+</kbd>
- select <kbd>Add from Git URL</kbd>
- paste `https://github.com/shiena/UnityStandaloneFileBrowser.git?path=Packages/StandaloneFileBrowser#upm`
- click <kbd>Add</kbd>
</details>


Example usage:

```csharp
// Open file
var paths = StandaloneFileBrowser.OpenFilePanel("Open File", "", "", false);

// Open file async
StandaloneFileBrowser.OpenFilePanelAsync("Open File", "", "", false, (string[] paths) => {  });

// Open file with filter
var extensions = new [] {
    new ExtensionFilter("Image Files", "png", "jpg", "jpeg" ),
    new ExtensionFilter("Sound Files", "mp3", "wav" ),
    new ExtensionFilter("All Files", "*" ),
};
var paths = StandaloneFileBrowser.OpenFilePanel("Open File", "", extensions, true);

// Save file
var path = StandaloneFileBrowser.SaveFilePanel("Save File", "", "", "");

// Save file async
StandaloneFileBrowser.SaveFilePanelAsync("Save File", "", "", "", (string path) => {  });

// Save file with filter
var extensionList = new [] {
    new ExtensionFilter("Binary", "bin"),
    new ExtensionFilter("Text", "txt"),
};
var path = StandaloneFileBrowser.SaveFilePanel("Save File", "", "MySaveFile", extensionList);
```
See Sample/BasicSampleScene.unity for more detailed examples.

Mac Screenshot
![Alt text](/Images/sfb_mac.jpg?raw=true "Mac")

Windows Screenshot
![Alt text](/Images/sfb_win.jpg?raw=true "Win")

Linux Screenshot
![Alt text](/Images/sfb_linux.jpg?raw=true "Win")

Notes:
- Windows
    * Requires .NET 3.5 or higher api compatibility level 
    * Async dialog opening not implemented, ..Async methods simply calls regular sync methods.
    * Plugin import settings should be like this;
    
    ![Alt text](/Images/win_import_1.jpg?raw=true "Plugin Import Ookii") ![Alt text](/Images/win_import_2.jpg?raw=true "Plugin Import System.Forms")
    
- Mac
    * Sync calls are throws an exception at development build after native panel loses and gains focus. Use async calls to avoid this.

WebGL:
 - WebGL uses the dedicated `StandaloneFileBrowserWebGL` API instead of the desktop `StandaloneFileBrowser` path-based API.
 - The desktop `StandaloneFileBrowser` API is intentionally unavailable in WebGL builds; use explicit platform branching.
 - Opening files returns temporary browser file references (`WebGLFileReference`) with `ObjectUrl`, `Name`, `Size`, `MimeType`, and `LastModifiedUnixMs` metadata.
 - Recommended read path is `ObjectUrl + UnityWebRequest` / `UnityWebRequestTexture`, then explicit `Release(...)` after the read completes.
 - Saving uses `SaveFileAsync(fileName, data[, mimeType])` and triggers a browser download rather than a native save dialog.
 - File filters are picker hints only.
 - Browser limitations still apply: meaningful `title` / default-directory semantics are not available. For opening and saving files, call the API from `IPointerDownHandler.OnPointerDown`, as in the canvas samples; the JS bridge arms the request and triggers the picker or download on the next native `pointerup` event. Do not use `Button.onClick`, keyboard activation, or delayed/programmatic calls with this implementation.
 - When the browser exposes `navigator.userActivation`, both open and save check transient activation in the native `pointerup` handler immediately before invoking the picker or download, allowing touch activation to become available on release. A failed check returns `BlockedByBrowser` for either operation. Browser policy can also block a picker or download without reporting an error to the plugin.
 - Save copies the supplied bytes and prepares the Blob synchronously; only activation checking and download triggering wait for `pointerup`. `Success` means the download was triggered, not that the file was written to disk. If a new pointerdown occurs before the expected release, the armed save returns `Cancelled` and releases its resources so another request can start.
 - First-version concurrency is one open request and one save request at a time.

 ### Result status

 Both `WebGLFileSelectionResult.Status` and `WebGLSaveResult.Status` use the shared `WebGLFileBrowserStatus` enum:

 | Status | Open | Save |
 | --- | --- | --- |
 | `Success` | Files were selected. | The download was triggered; completion is not observed. |
 | `Cancelled` | The picker was cancelled, or the request was abandoned before the picker opened. | The Editor save dialog was cancelled, or the request was abandoned before the download was triggered. |
 | `BlockedByBrowser` | The activation check failed before opening the picker. | The activation check failed before triggering the download. |
 | `Busy` | Another open request is pending. | Another save request is pending. |
 | `Error` | A file operation, bridge call, or response failed. | Data preparation, download triggering, or a bridge call/response failed. |

 A browser download's cancellation after triggering cannot be observed by this API. `BlockedByBrowser` identifies a detected activation failure, not every possible browser restriction; some restrictions can silently prevent an operation.

 ### Browser compatibility

 File-picker cancellation relies on the [`HTMLInputElement` `cancel` event](https://developer.mozilla.org/en-US/docs/Web/API/HTMLInputElement/cancel_event). The supported minimum versions for this cancellation behavior are Chrome / Edge 113, Firefox 91, and Safari / iOS Safari 16.4 (Chrome for Android: 113). These are the minimum versions for this event, not a guarantee of compatibility with every Unity WebGL build; the Unity version and other browser requirements also apply.

 Older browsers are not supported for reliable file-picker cancellation. Without the `cancel` event, closing the picker without selecting a file may leave the request pending, and subsequent open requests can return `Busy`. `ReleaseAll()` releases file references and object URLs only; it does not cancel or reset pending requests. There is currently no cancellation fallback or public request-reset API. The new-pointerdown guard only cancels a request whose pointer release was missed **before** the picker opens; it is not a fallback for cancelling an already-open picker.

 Example WebGL usage:
 ```csharp
 using System.Collections;
 using SFB;
 using UnityEngine;
 using UnityEngine.Networking;

 public void OpenTextFile() {
     StandaloneFileBrowserWebGL.OpenFilePanelAsync("txt", false, result => {
         if (result.Status == WebGLFileBrowserStatus.Cancelled) {
             return;
         }

         if (result.Status == WebGLFileBrowserStatus.BlockedByBrowser) {
             Debug.LogWarning(result.ErrorMessage);
             return;
         }

         if (result.Status != WebGLFileBrowserStatus.Success || result.Files.Length == 0) {
             return;
         }

         var file = result.Files[0];
         StartCoroutine(ReadTextAndRelease(file));
     });
 }

 private IEnumerator ReadTextAndRelease(WebGLFileReference file) {
     var request = UnityWebRequest.Get(file.ObjectUrl);
     yield return request.SendWebRequest();

     if (request.result == UnityWebRequest.Result.Success) {
         Debug.Log(request.downloadHandler.text);
     }

     StandaloneFileBrowserWebGL.Release(file);
 }

 public void SaveTextFile(byte[] bytes) {
     StandaloneFileBrowserWebGL.SaveFileAsync("sample.txt", bytes, result => {
         Debug.Log(result.Status);
     });
 }
 ```

 Live Demo: https://gkngkc.github.io/
