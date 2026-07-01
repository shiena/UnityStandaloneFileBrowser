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
 - Browser limitations still apply: meaningful `title` / default-directory semantics are not available, and open/save should be triggered directly from a user gesture (OnPointerDown).
 - If open is triggered outside a valid user gesture, it returns `Error` with an explanatory message; save returns `BlockedByBrowser`.
 - First-version concurrency is one open request and one save request at a time.

 Example WebGL usage:
 ```csharp
 using System.Collections;
 using SFB;
 using UnityEngine;
 using UnityEngine.Networking;

 public void OpenTextFile() {
     StandaloneFileBrowserWebGL.OpenFilePanelAsync("txt", false, result => {
         if (result.Status != WebGLFileSelectionStatus.Success || result.Files.Length == 0) {
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
