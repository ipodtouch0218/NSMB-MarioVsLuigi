var LibraryWebSockets = {
$webSocketInstances: [],

SocketCreate: function(url, protocols, openCallback, recvCallback, errorCallback, closeCallback)
{
    var str = UTF8ToString(url);
    var prot = UTF8ToString(protocols);
    var socket = {
        socket: new WebSocket(str, [prot]),
        error: null,
        sendBufForShared: null,
        send: typeof(SharedArrayBuffer) == "function" ? // SharedArrayBuffer is available and will not crash in 'isinstance' check
    		function (socketInstance, ptr, length) {
                if (HEAPU8.buffer instanceof SharedArrayBuffer) {
                    if (!this.sendBufForShared || this.sendBufForShared.byteLength < length) {
                        this.sendBufForShared = new ArrayBuffer(length);
                    }
                    var u8arr = new Uint8Array(this.sendBufForShared, 0, length);
                    u8arr.set(new Uint8Array(HEAPU8.buffer, ptr, length));
                    this.socket.send(u8arr);
                }  else {
                    this.socket.send(new Uint8Array(HEAPU8.buffer, ptr, length));
                }
            }
            :
            function (socketInstance, ptr, length) { // SharedArrayBuffer is not defined, ptr type is always ArrayBuffer
                this.socket.send(new Uint8Array(HEAPU8.buffer, ptr, length));
            }
    }
    var instance = webSocketInstances.push(socket) - 1;
    socket.socket.binaryType = 'arraybuffer';
    
    socket.socket.onopen = function () {
        {{{ makeDynCall('vi', 'openCallback') }}}(instance);
    }
    socket.socket.onmessage = function (e) {
        if (e.data instanceof ArrayBuffer)
        {
            const b = e.data;
            const ptr = _malloc(b.byteLength);
            const dataHeap = new Int8Array(HEAPU8.buffer, ptr, b.byteLength);
            dataHeap.set(new Int8Array(b));
            {{{ makeDynCall('viii', 'recvCallback') }}}(instance, ptr, b.byteLength);
            _free(ptr);
        }
    };
    socket.socket.onerror = function (e) {
        {{{ makeDynCall('vii', 'errorCallback') }}}(instance, e.code);
    }
    socket.socket.onclose = function (e) {
        if (e.code != 1000)
        {
            {{{ makeDynCall('vii', 'closeCallback') }}}(instance, e.code);
        }
    }
    return instance;
},

SocketState: function (socketInstance)
{
    var socket = webSocketInstances[socketInstance];
    return socket.socket.readyState;
},

SocketError: function (socketInstance, ptr, bufsize)
{
 	var socket = webSocketInstances[socketInstance];
 	if (socket.error == null)
 		return 0;
    stringToUTF8(socket.error, ptr, bufsize);
    return 1;
},

SocketSend: function (socketInstance, ptr, bufsize)
{
    var socket = webSocketInstances[socketInstance];
    socket.send(socketInstance, ptr, bufsize);
},

SocketClose: function (socketInstance)
{
    var socket = webSocketInstances[socketInstance];
    socket.socket.close();
}
};

autoAddDeps(LibraryWebSockets, '$webSocketInstances');
mergeInto(LibraryManager.library, LibraryWebSockets);
