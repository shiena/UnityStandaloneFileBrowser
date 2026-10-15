#if UNITY_WEBGL

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SFB {
    public enum WebGLFileBrowserStatus {
        Success = 0,
        Cancelled = 1,
        Busy = 2,
        Error = 3,
        BlockedByBrowser = 4,
    }

    [Serializable]
    public sealed class WebGLFileReference {
        public string HandleId;
        public string ObjectUrl;
        public string Name;
        public long Size;
        public string MimeType;
        public long LastModifiedUnixMs;
    }

    [Serializable]
    public sealed class WebGLFileSelectionResult {
        public WebGLFileBrowserStatus Status;
        public WebGLFileReference[] Files;
        public string ErrorMessage;

        public WebGLFileSelectionResult() {
            Files = new WebGLFileReference[0];
            ErrorMessage = string.Empty;
        }
    }

    [Serializable]
    public sealed class WebGLSaveResult {
        public WebGLFileBrowserStatus Status;
        public string ErrorMessage;

        public WebGLSaveResult() {
            ErrorMessage = string.Empty;
        }
    }

    public static class StandaloneFileBrowserWebGL {
        public static void OpenFilePanelAsync(string extension, bool multiselect, Action<WebGLFileSelectionResult> cb) {
            var extensions = string.IsNullOrEmpty(extension) ? null : new [] { new ExtensionFilter(string.Empty, extension) };
            OpenFilePanelAsync(extensions, multiselect, cb);
        }

        public static void OpenFilePanelAsync(ExtensionFilter[] extensions, bool multiselect, Action<WebGLFileSelectionResult> cb) {
#if UNITY_EDITOR
            StandaloneFileBrowserWebGLEditorBridge.OpenFilePanelAsync(extensions, multiselect, cb);
#else
            StandaloneFileBrowserWebGLBridge.Instance.OpenFilePanelAsync(extensions, multiselect, cb);
#endif
        }

        public static void SaveFileAsync(string fileName, byte[] data, Action<WebGLSaveResult> cb) {
            SaveFileAsync(fileName, data, string.Empty, cb);
        }

        public static void SaveFileAsync(string fileName, byte[] data, string mimeType, Action<WebGLSaveResult> cb) {
#if UNITY_EDITOR
            StandaloneFileBrowserWebGLEditorBridge.SaveFileAsync(fileName, data, mimeType, cb);
#else
            StandaloneFileBrowserWebGLBridge.Instance.SaveFileAsync(fileName, data, mimeType, cb);
#endif
        }

        public static void Release(WebGLFileReference file) {
#if UNITY_EDITOR
            StandaloneFileBrowserWebGLEditorBridge.Release(file);
#else
            if (!StandaloneFileBrowserWebGLBridge.HasInstance) {
                return;
            }

            StandaloneFileBrowserWebGLBridge.Instance.Release(file);
#endif
        }

        public static void Release(WebGLFileReference[] files) {
#if UNITY_EDITOR
            StandaloneFileBrowserWebGLEditorBridge.Release(files);
#else
            if (!StandaloneFileBrowserWebGLBridge.HasInstance) {
                return;
            }

            StandaloneFileBrowserWebGLBridge.Instance.Release(files);
#endif
        }

        public static void ReleaseAll() {
#if UNITY_EDITOR
            StandaloneFileBrowserWebGLEditorBridge.ReleaseAll();
#else
            if (!StandaloneFileBrowserWebGLBridge.HasInstance) {
                return;
            }

            StandaloneFileBrowserWebGLBridge.Instance.ReleaseAll();
#endif
        }

        internal static WebGLFileBrowserStatus ParseStatus(string status) {
            if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)) {
                return WebGLFileBrowserStatus.Success;
            }

            if (string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase)) {
                return WebGLFileBrowserStatus.Cancelled;
            }

            if (string.Equals(status, "blocked-by-browser", StringComparison.OrdinalIgnoreCase)) {
                return WebGLFileBrowserStatus.BlockedByBrowser;
            }

            if (string.Equals(status, "busy", StringComparison.OrdinalIgnoreCase)) {
                return WebGLFileBrowserStatus.Busy;
            }

            return WebGLFileBrowserStatus.Error;
        }
    }

#if UNITY_EDITOR
    internal static class StandaloneFileBrowserWebGLEditorBridge {
        private static readonly Dictionary<string, WebGLFileReference> _filesByHandle = new Dictionary<string, WebGLFileReference>();
        private static int _nextHandleId;
        private static int _activeOpenRequests;
        private static int _activeSaveRequests;

        public static void OpenFilePanelAsync(ExtensionFilter[] extensions, bool multiselect, Action<WebGLFileSelectionResult> cb) {
            if (cb == null) {
                cb = delegate { };
            }

            if (_activeOpenRequests > 0) {
                cb.Invoke(CreateBusyOpenResult());
                return;
            }

            WebGLFileSelectionResult result;
            _activeOpenRequests++;
            try {
                var paths = StandaloneFileBrowser.OpenFilePanel(string.Empty, string.Empty, extensions, multiselect);
                result = CreateOpenResult(paths);
            }
            catch (Exception exception) {
                result = CreateErrorOpenResult(exception.Message);
            }
            finally {
                _activeOpenRequests--;
            }

            cb.Invoke(result);
        }

        public static void SaveFileAsync(string fileName, byte[] data, string mimeType, Action<WebGLSaveResult> cb) {
            if (cb == null) {
                cb = delegate { };
            }

            if (_activeSaveRequests > 0) {
                cb.Invoke(CreateBusySaveResult());
                return;
            }

            if (string.IsNullOrEmpty(fileName)) {
                cb.Invoke(CreateErrorSaveResult("File name cannot be empty."));
                return;
            }

            WebGLSaveResult result;
            _activeSaveRequests++;
            try {
                var extension = Path.GetExtension(fileName);
                if (!string.IsNullOrEmpty(extension)) {
                    extension = extension.TrimStart('.');
                }

                var defaultName = Path.GetFileNameWithoutExtension(fileName);
                var path = StandaloneFileBrowser.SaveFilePanel(string.Empty, string.Empty, defaultName, string.IsNullOrEmpty(extension) ? null : new [] { new ExtensionFilter(string.Empty, extension) });
                if (string.IsNullOrEmpty(path)) {
                    result = CreateSaveResult("cancelled", string.Empty);
                }
                else {
                    var buffer = data ?? new byte[0];
                    File.WriteAllBytes(path, buffer);
                    result = CreateSaveResult("success", string.Empty);
                }
            }
            catch (Exception exception) {
                result = CreateErrorSaveResult(exception.Message);
            }
            finally {
                _activeSaveRequests--;
            }

            cb.Invoke(result);
        }

        public static void Release(WebGLFileReference file) {
            if (file == null || string.IsNullOrEmpty(file.HandleId)) {
                return;
            }

            _filesByHandle.Remove(file.HandleId);
        }

        public static void Release(WebGLFileReference[] files) {
            if (files == null) {
                return;
            }

            for (var i = 0; i < files.Length; i++) {
                Release(files[i]);
            }
        }

        public static void ReleaseAll() {
            _filesByHandle.Clear();
        }

        private static WebGLFileSelectionResult CreateOpenResult(string[] paths) {
            var result = new WebGLFileSelectionResult();
            if (paths == null || paths.Length == 0) {
                result.Status = WebGLFileBrowserStatus.Cancelled;
                return result;
            }

            result.Status = WebGLFileBrowserStatus.Success;
            result.Files = new WebGLFileReference[paths.Length];
            for (var i = 0; i < paths.Length; i++) {
                var path = paths[i];
                var handleId = CreateHandleId();
                var fileInfo = new FileInfo(path);
                var file = new WebGLFileReference {
                    HandleId = handleId,
                    ObjectUrl = new Uri(path).AbsoluteUri,
                    Name = fileInfo.Exists ? fileInfo.Name : Path.GetFileName(path),
                    Size = fileInfo.Exists ? fileInfo.Length : 0,
                    MimeType = string.Empty,
                    LastModifiedUnixMs = fileInfo.Exists ? new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeMilliseconds() : 0,
                };
                result.Files[i] = file;
                _filesByHandle[handleId] = file;
            }

            return result;
        }

        private static WebGLFileSelectionResult CreateBusyOpenResult() {
            var result = new WebGLFileSelectionResult();
            result.Status = WebGLFileBrowserStatus.Busy;
            return result;
        }

        private static WebGLFileSelectionResult CreateErrorOpenResult(string message) {
            var result = new WebGLFileSelectionResult();
            result.Status = WebGLFileBrowserStatus.Error;
            result.ErrorMessage = string.IsNullOrEmpty(message) ? "Browser file open failed." : message;
            return result;
        }

        private static WebGLSaveResult CreateBusySaveResult() {
            var result = new WebGLSaveResult();
            result.Status = WebGLFileBrowserStatus.Busy;
            return result;
        }

        private static WebGLSaveResult CreateErrorSaveResult(string message) {
            var result = new WebGLSaveResult();
            result.Status = WebGLFileBrowserStatus.Error;
            result.ErrorMessage = string.IsNullOrEmpty(message) ? "Browser file save failed." : message;
            return result;
        }

        private static WebGLSaveResult CreateSaveResult(string status, string errorMessage) {
            var result = new WebGLSaveResult();
            result.Status = StandaloneFileBrowserWebGL.ParseStatus(status);
            result.ErrorMessage = errorMessage ?? string.Empty;
            return result;
        }

        private static string CreateHandleId() {
            _nextHandleId++;
            return "editor-handle-" + _nextHandleId;
        }
    }
#endif

}

#endif
