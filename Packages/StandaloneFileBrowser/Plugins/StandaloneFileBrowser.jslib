var StandaloneFileBrowserWebGLPlugin = {
    $SFBHelpers: {
        initialized: false,
        state: null,

        getGlobal: function() {
            if (typeof globalThis !== 'undefined') return globalThis;
            if (typeof self !== 'undefined') return self;
            if (typeof window !== 'undefined') return window;
            return this;
        },

        ensureState: function() {
            if (this.initialized) return this.state;
            var globalObj = this.getGlobal();
            if (!globalObj.__sfbWebGLState) {
                globalObj.__sfbWebGLState = {
                    nextHandleId: 0,
                    activeOpenRequest: null,
                    activeSaveRequest: null,
                    filesByHandle: {}
                };
            }
            this.state = globalObj.__sfbWebGLState;
            this.initialized = true;
            return this.state;
        },

        getState: function() {
            return this.ensureState();
        },

        createHandleId: function() {
            var state = this.getState();
            state.nextHandleId += 1;
            return 'handle-' + state.nextHandleId;
        },

        createOpenResponse: function(requestId, status, files, errorMessage) {
            return {
                type: 'open',
                requestId: requestId || '',
                status: status || 'error',
                files: files || [],
                errorMessage: errorMessage || ''
            };
        },

        createSaveResponse: function(requestId, status, errorMessage) {
            return {
                type: 'save',
                requestId: requestId || '',
                status: status || 'error',
                errorMessage: errorMessage || ''
            };
        },

        sendMessage: function(gameObjectName, methodName, payload) {
            SendMessage(gameObjectName, methodName, JSON.stringify(payload));
        },

        revokeHandle: function(handleId) {
            var state = this.getState();
            if (!handleId || !state.filesByHandle[handleId]) return;
            var fileEntry = state.filesByHandle[handleId];
            delete state.filesByHandle[handleId];
            if (fileEntry.objectUrl) {
                URL.revokeObjectURL(fileEntry.objectUrl);
            }
        },

        revokeAllHandles: function() {
            var state = this.getState();
            for (var handleId in state.filesByHandle) {
                if (state.filesByHandle.hasOwnProperty(handleId)) {
                    this.revokeHandle(handleId);
                }
            }
        },

        cleanupOpenRequest: function(request) {
            if (!request) return;

            if (request.input) {
                if (request.changeHandler) request.input.removeEventListener('change', request.changeHandler);
                if (request.cancelHandler) request.input.removeEventListener('cancel', request.cancelHandler);
                if (request.input.parentNode) request.input.parentNode.removeChild(request.input);
            }
            var state = this.getState();
            if (state.activeOpenRequest === request) state.activeOpenRequest = null;
        },

        completeOpenRequest: function(request, payload) {
            if (!request || request.completed) return;
            request.completed = true;
            this.cleanupOpenRequest(request);
            this.sendMessage(request.gameObjectName, request.methodName, payload);
        },

        cleanupSaveRequest: function(request, revokeObjectUrlAsync) {
            if (!request) return;
            if (request.anchor && request.anchor.parentNode) {
                request.anchor.parentNode.removeChild(request.anchor);
            }
            var objectUrl = request.objectUrl;
            request.anchor = null;
            request.objectUrl = null;
            var state = this.getState();
            if (state.activeSaveRequest === request) state.activeSaveRequest = null;
            if (!objectUrl) return;
            if (revokeObjectUrlAsync) {
                window.setTimeout(function() { URL.revokeObjectURL(objectUrl); }, 0);
                return;
            }
            URL.revokeObjectURL(objectUrl);
        },

        completeSaveRequest: function(request, payload) {
            if (!request || request.completed) return;
            request.completed = true;
            this.cleanupSaveRequest(request, payload && payload.status === 'success');
            this.sendMessage(request.gameObjectName, request.methodName, payload);
        }
    },

    StandaloneFileBrowserWebGLOpenFilePanel: function(gameObjectNamePtr, methodNamePtr, requestIdPtr, filterPtr, multiselect) {
        var helpers = SFBHelpers;

        var gameObjectName = UTF8ToString(gameObjectNamePtr);
        var methodName = UTF8ToString(methodNamePtr);
        var requestId = UTF8ToString(requestIdPtr);
        var filter = UTF8ToString(filterPtr);
        var state = helpers.getState();

        if (state.activeOpenRequest) {
            helpers.sendMessage(
                gameObjectName,
                methodName,
                helpers.createOpenResponse(requestId, 'busy', [], '')
            );
            return;
        }

        if (typeof navigator !== 'undefined' && navigator.userActivation && navigator.userActivation.isActive === false) {
            var errorMessage = 'Browser file picker must be triggered from a user gesture.';
            if (typeof console !== 'undefined' && console.error) console.error(errorMessage);
            helpers.sendMessage(
                gameObjectName,
                methodName,
                helpers.createOpenResponse(requestId, 'error', [], errorMessage)
            );
            return;
        }

        var request = {
            gameObjectName: gameObjectName,
            methodName: methodName,
            requestId: requestId,
            input: null,
            changeHandler: null,
            cancelHandler: null,
            focusHandler: null,
            completed: false
        };
        state.activeOpenRequest = request;

        try {
            var fileInput = document.createElement('input');
            request.input = fileInput;

            fileInput.setAttribute('type', 'file');
            fileInput.setAttribute('style', 'position:fixed;left:-10000px;top:-10000px;opacity:0;pointer-events:none;');

            if (multiselect) fileInput.setAttribute('multiple', '');
            if (filter) fileInput.setAttribute('accept', filter);

            fileInput.onclick = function() { this.value = ''; };

            request.changeHandler = function(event) {
                var selectedFiles = event.target && event.target.files ? event.target.files : [];
                var files = [];

                for (var i = 0; i < selectedFiles.length; i++) {
                    var browserFile = selectedFiles[i];
                    var handleId = helpers.createHandleId();
                    var objectUrl = URL.createObjectURL(browserFile);
                    var fileState = helpers.getState();

                    fileState.filesByHandle[handleId] = {
                        objectUrl: objectUrl,
                        name: browserFile.name || '',
                        size: browserFile.size || 0,
                        mimeType: browserFile.type || '',
                        lastModifiedUnixMs: browserFile.lastModified || 0
                    };

                    files.push({
                        handleId: handleId,
                        objectUrl: objectUrl,
                        name: browserFile.name || '',
                        size: String(browserFile.size || 0),
                        mimeType: browserFile.type || '',
                        lastModifiedUnixMs: String(browserFile.lastModified || 0)
                    });
                }

                helpers.completeOpenRequest(
                    request,
                    helpers.createOpenResponse(
                        request.requestId,
                        files.length > 0 ? 'success' : 'cancelled',
                        files,
                        ''
                    )
                );
            };

            request.cancelHandler = function() {
                helpers.completeOpenRequest(
                    request,
                    helpers.createOpenResponse(request.requestId, 'cancelled', [], '')
                );
            };

            fileInput.addEventListener('change', request.changeHandler);
            fileInput.addEventListener('cancel', request.cancelHandler);

            document.body.appendChild(fileInput);
            fileInput.click();
        } catch (error) {
            helpers.completeOpenRequest(
                request,
                helpers.createOpenResponse(
                    request.requestId,
                    'error',
                    [],
                    error && error.message ? error.message : 'Browser file open failed.'
                )
            );
        }
    },

    StandaloneFileBrowserWebGLSaveFile: function(gameObjectNamePtr, methodNamePtr, requestIdPtr, fileNamePtr, mimeTypePtr, byteArray, byteArraySize) {
        var helpers = SFBHelpers;

        var gameObjectName = UTF8ToString(gameObjectNamePtr);
        var methodName = UTF8ToString(methodNamePtr);
        var requestId = UTF8ToString(requestIdPtr);
        var fileName = UTF8ToString(fileNamePtr);
        var mimeType = UTF8ToString(mimeTypePtr);
        var state = helpers.getState();

        if (state.activeSaveRequest) {
            helpers.sendMessage(
                gameObjectName,
                methodName,
                helpers.createSaveResponse(requestId, 'busy', '')
            );
            return;
        }

        var request = {
            gameObjectName: gameObjectName,
            methodName: methodName,
            requestId: requestId,
            anchor: null,
            objectUrl: null,
            completed: false
        };
        state.activeSaveRequest = request;

        try {
            if (typeof navigator !== 'undefined' && navigator.userActivation && navigator.userActivation.isActive === false) {
                helpers.completeSaveRequest(
                    request,
                    helpers.createSaveResponse(
                        request.requestId,
                        'blocked-by-browser',
                        'Browser download must be triggered from a user gesture.'
                    )
                );
                return;
            }

            var bytes = new Uint8Array(byteArraySize);
            if (byteArraySize > 0) bytes.set(HEAPU8.subarray(byteArray, byteArray + byteArraySize));

            var blob = new Blob([bytes], { type: mimeType || 'application/octet-stream' });
            request.objectUrl = URL.createObjectURL(blob);
            request.anchor = document.createElement('a');
            request.anchor.setAttribute('download', fileName);
            request.anchor.setAttribute('style', 'display:none;');
            request.anchor.href = request.objectUrl;
            document.body.appendChild(request.anchor);
            request.anchor.click();

            helpers.completeSaveRequest(
                request,
                helpers.createSaveResponse(request.requestId, 'success', '')
            );
        } catch (error) {
            helpers.completeSaveRequest(
                request,
                helpers.createSaveResponse(
                    request.requestId,
                    'error',
                    error && error.message ? error.message : 'Browser file save failed.'
                )
            );
        }
    },

    StandaloneFileBrowserWebGLReleaseFile: function(handleIdPtr) {
        var helpers = SFBHelpers;
        var handleId = UTF8ToString(handleIdPtr);
        helpers.revokeHandle(handleId);
    },

    StandaloneFileBrowserWebGLReleaseAllFiles: function() {
        var helpers = SFBHelpers;
        helpers.revokeAllHandles();
    }
};

autoAddDeps(StandaloneFileBrowserWebGLPlugin, '$SFBHelpers');

mergeInto(LibraryManager.library, StandaloneFileBrowserWebGLPlugin);
