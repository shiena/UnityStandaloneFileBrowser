#if UNITY_WEBGL

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;


namespace SFB
{
    internal class StandaloneFileBrowserWebGLBridge : MonoBehaviour
    {
        private const string GameObjectName = "StandaloneFileBrowserWebGLBridge";
        private static StandaloneFileBrowserWebGLBridge _instance;

        [DllImport("__Internal")]
        private static extern void StandaloneFileBrowserWebGLOpenFilePanel(string gameObjectName, string methodName, string requestId, string filter, bool multiselect);

        [DllImport("__Internal")]
        private static extern void StandaloneFileBrowserWebGLSaveFile(string gameObjectName, string methodName, string requestId, string fileName, string mimeType, byte[] byteArray, int byteArraySize);

        [DllImport("__Internal")]
        private static extern void StandaloneFileBrowserWebGLReleaseFile(string handleId);

        [DllImport("__Internal")]
        private static extern void StandaloneFileBrowserWebGLReleaseAllFiles();

        private readonly Dictionary<string, Action<WebGLFileSelectionResult>> _openCallbacks = new Dictionary<string, Action<WebGLFileSelectionResult>>();
        private readonly Dictionary<string, Action<WebGLSaveResult>> _saveCallbacks = new Dictionary<string, Action<WebGLSaveResult>>();
        private readonly Dictionary<string, WebGLFileReference> _filesByHandle = new Dictionary<string, WebGLFileReference>();
        private int _requestIdCounter;

        public static bool HasInstance
        {
            get { return _instance != null; }
        }

        public static StandaloneFileBrowserWebGLBridge Instance
        {
            get
            {
                if (_instance == null)
                {
                    var gameObject = new GameObject(GameObjectName);
                    _instance = gameObject.AddComponent<StandaloneFileBrowserWebGLBridge>();
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            ReleaseAll();
            _instance = null;
        }

        private void OnApplicationQuit()
        {
            ReleaseAll();
        }

        public void OpenFilePanelAsync(ExtensionFilter[] extensions, bool multiselect, Action<WebGLFileSelectionResult> cb)
        {
            if (cb == null)
            {
                cb = delegate { };
            }

            if (_openCallbacks.Count > 0)
            {
                cb.Invoke(CreateBusyOpenResult());
                return;
            }

            var requestId = CreateRequestId("open");
            _openCallbacks[requestId] = cb;

            try
            {
                StandaloneFileBrowserWebGLOpenFilePanel(gameObject.name, nameof(OnOpenRequestCompleted), requestId, GetFilterFromFileExtensionList(extensions), multiselect);
            }
            catch (Exception exception)
            {
                _openCallbacks.Remove(requestId);
                cb.Invoke(CreateErrorOpenResult(exception.Message));
            }
        }

        public void SaveFileAsync(string fileName, byte[] data, string mimeType, Action<WebGLSaveResult> cb)
        {
            if (cb == null)
            {
                cb = delegate { };
            }

            if (_saveCallbacks.Count > 0)
            {
                cb.Invoke(CreateBusySaveResult());
                return;
            }

            if (string.IsNullOrEmpty(fileName))
            {
                cb.Invoke(CreateErrorSaveResult("File name cannot be empty."));
                return;
            }

            var buffer = data ?? new byte[0];
            var requestId = CreateRequestId("save");
            _saveCallbacks[requestId] = cb;

            try
            {
                StandaloneFileBrowserWebGLSaveFile(gameObject.name, nameof(OnSaveRequestCompleted), requestId, fileName, mimeType ?? string.Empty, buffer, buffer.Length);
            }
            catch (Exception exception)
            {
                _saveCallbacks.Remove(requestId);
                cb.Invoke(CreateErrorSaveResult(exception.Message));
            }
        }

        public void Release(WebGLFileReference file)
        {
            if (file == null || string.IsNullOrEmpty(file.HandleId))
            {
                return;
            }

            ReleaseByHandle(file.HandleId);
        }

        public void Release(WebGLFileReference[] files)
        {
            if (files == null)
            {
                return;
            }

            for (var i = 0; i < files.Length; i++)
            {
                Release(files[i]);
            }
        }

        public void ReleaseAll()
        {
            _filesByHandle.Clear();
            StandaloneFileBrowserWebGLReleaseAllFiles();
        }

        public void OnOpenRequestCompleted(string message)
        {
            var response = DeserializeOpenResponse(message);
            if (response == null || string.IsNullOrEmpty(response.requestId))
            {
                FailPendingOpenRequest("Failed to parse browser open-file response.");
                return;
            }

            Action<WebGLFileSelectionResult> callback;
            if (!_openCallbacks.TryGetValue(response.requestId, out callback))
            {
                return;
            }

            _openCallbacks.Remove(response.requestId);
            callback.Invoke(CreateOpenResult(response));
        }

        public void OnSaveRequestCompleted(string message)
        {
            var response = DeserializeSaveResponse(message);
            if (response == null || string.IsNullOrEmpty(response.requestId))
            {
                FailPendingSaveRequest("Failed to parse browser save-file response.");
                return;
            }

            Action<WebGLSaveResult> callback;
            if (!_saveCallbacks.TryGetValue(response.requestId, out callback))
            {
                return;
            }

            _saveCallbacks.Remove(response.requestId);
            callback.Invoke(CreateSaveResult(response));
        }

        private void ReleaseByHandle(string handleId)
        {
            if (!_filesByHandle.Remove(handleId))
            {
                return;
            }

            StandaloneFileBrowserWebGLReleaseFile(handleId);
        }

        private void FailPendingOpenRequest(string message)
        {
            if (_openCallbacks.Count == 0)
            {
                return;
            }

            using (var enumerator = _openCallbacks.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    return;
                }

                var entry = enumerator.Current;
                _openCallbacks.Remove(entry.Key);
                entry.Value.Invoke(CreateErrorOpenResult(message));
            }
        }

        private void FailPendingSaveRequest(string message)
        {
            if (_saveCallbacks.Count == 0)
            {
                return;
            }

            using (var enumerator = _saveCallbacks.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    return;
                }

                var entry = enumerator.Current;
                _saveCallbacks.Remove(entry.Key);
                entry.Value.Invoke(CreateErrorSaveResult(message));
            }
        }

        private string CreateRequestId(string prefix)
        {
            _requestIdCounter++;
            return prefix + "-" + _requestIdCounter;
        }

        private WebGLFileSelectionResult CreateOpenResult(WebGLFileSelectionResponse response)
        {
            var result = new WebGLFileSelectionResult();
            result.Status = ParseSelectionStatus(response.status);
            result.ErrorMessage = response.errorMessage ?? string.Empty;
            result.Files = ConvertFiles(response.files);

            if (result.Status == WebGLFileSelectionStatus.Success && result.Files.Length == 0)
            {
                result.Status = WebGLFileSelectionStatus.Cancelled;
            }

            for (var i = 0; i < result.Files.Length; i++)
            {
                var file = result.Files[i];
                if (file == null || string.IsNullOrEmpty(file.HandleId))
                {
                    continue;
                }

                _filesByHandle[file.HandleId] = file;
            }

            return result;
        }

        private static WebGLSaveResult CreateSaveResult(WebGLSaveResponse response)
        {
            var result = new WebGLSaveResult();
            result.Status = ParseSaveStatus(response.status);
            result.ErrorMessage = response.errorMessage ?? string.Empty;
            return result;
        }

        private static WebGLFileSelectionResult CreateBusyOpenResult()
        {
            var result = new WebGLFileSelectionResult();
            result.Status = WebGLFileSelectionStatus.Busy;
            return result;
        }

        private static WebGLFileSelectionResult CreateErrorOpenResult(string message)
        {
            var result = new WebGLFileSelectionResult();
            result.Status = WebGLFileSelectionStatus.Error;
            result.ErrorMessage = string.IsNullOrEmpty(message) ? "Browser file open failed." : message;
            return result;
        }

        private static WebGLSaveResult CreateBusySaveResult()
        {
            var result = new WebGLSaveResult();
            result.Status = WebGLSaveStatus.Busy;
            return result;
        }

        private static WebGLSaveResult CreateErrorSaveResult(string message)
        {
            var result = new WebGLSaveResult();
            result.Status = WebGLSaveStatus.Error;
            result.ErrorMessage = string.IsNullOrEmpty(message) ? "Browser file save failed." : message;
            return result;
        }

        private static WebGLFileReference[] ConvertFiles(WebGLFileResponse[] files)
        {
            if (files == null || files.Length == 0)
            {
                return new WebGLFileReference[0];
            }

            var result = new WebGLFileReference[files.Length];
            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                if (file == null)
                {
                    continue;
                }

                result[i] = new WebGLFileReference
                {
                    HandleId = file.handleId ?? string.Empty,
                    ObjectUrl = file.objectUrl ?? string.Empty,
                    Name = file.name ?? string.Empty,
                    Size = ParseLong(file.size),
                    MimeType = file.mimeType ?? string.Empty,
                    LastModifiedUnixMs = ParseLong(file.lastModifiedUnixMs),
                };
            }

            return result;
        }

        private static long ParseLong(string value)
        {
            long parsedValue;
            if (long.TryParse(value, out parsedValue))
            {
                return parsedValue;
            }

            return 0;
        }

        private static WebGLFileSelectionStatus ParseSelectionStatus(string status)
        {
            if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                return WebGLFileSelectionStatus.Success;
            }

            if (string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return WebGLFileSelectionStatus.Cancelled;
            }

            if (string.Equals(status, "busy", StringComparison.OrdinalIgnoreCase))
            {
                return WebGLFileSelectionStatus.Busy;
            }

            return WebGLFileSelectionStatus.Error;
        }

        private static WebGLSaveStatus ParseSaveStatus(string status)
        {
            if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                return WebGLSaveStatus.Success;
            }

            if (string.Equals(status, "blocked-by-browser", StringComparison.OrdinalIgnoreCase))
            {
                return WebGLSaveStatus.BlockedByBrowser;
            }

            if (string.Equals(status, "busy", StringComparison.OrdinalIgnoreCase))
            {
                return WebGLSaveStatus.Busy;
            }

            return WebGLSaveStatus.Error;
        }

        private static WebGLFileSelectionResponse DeserializeOpenResponse(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<WebGLFileSelectionResponse>(message);
            }
            catch
            {
                return null;
            }
        }

        private static WebGLSaveResponse DeserializeSaveResponse(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<WebGLSaveResponse>(message);
            }
            catch
            {
                return null;
            }
        }

        private static string GetFilterFromFileExtensionList(ExtensionFilter[] extensions)
        {
            if (extensions == null || extensions.Length == 0)
            {
                return string.Empty;
            }

            var acceptedExtensions = new List<string>();
            for (var i = 0; i < extensions.Length; i++)
            {
                var filterExtensions = extensions[i].Extensions;
                if (filterExtensions == null || filterExtensions.Length == 0)
                {
                    continue;
                }

                for (var j = 0; j < filterExtensions.Length; j++)
                {
                    var extension = NormalizeExtension(filterExtensions[j]);
                    if (extension == "*")
                    {
                        return string.Empty;
                    }

                    if (string.IsNullOrEmpty(extension))
                    {
                        continue;
                    }

                    if (!acceptedExtensions.Contains(extension))
                    {
                        acceptedExtensions.Add(extension);
                    }
                }
            }

            if (acceptedExtensions.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(",", acceptedExtensions.ToArray());
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return string.Empty;
            }

            var normalizedExtension = extension.Trim();
            if (normalizedExtension.Length == 0)
            {
                return string.Empty;
            }

            if (normalizedExtension == "*" || normalizedExtension == "*.*")
            {
                return "*";
            }

            if (!normalizedExtension.StartsWith("."))
            {
                normalizedExtension = "." + normalizedExtension;
            }

            return normalizedExtension;
        }

        [Serializable]
        private sealed class WebGLFileSelectionResponse
        {
            public string type;
            public string requestId;
            public string status;
            public WebGLFileResponse[] files;
            public string errorMessage;
        }

        [Serializable]
        private sealed class WebGLFileResponse
        {
            public string handleId;
            public string objectUrl;
            public string name;
            public string size;
            public string mimeType;
            public string lastModifiedUnixMs;
        }

        [Serializable]
        private sealed class WebGLSaveResponse
        {
            public string type;
            public string requestId;
            public string status;
            public string errorMessage;
        }
    }
}

#endif