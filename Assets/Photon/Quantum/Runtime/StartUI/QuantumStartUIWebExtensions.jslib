mergeInto(LibraryManager.library, {
    PhotonStartUICopyToClipboard: function (textPtr) {
        var text = UTF8ToString(textPtr);

        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text);
        } else {
            var textarea = document.createElement('textarea');
            textarea.value = text;
            document.body.appendChild(textarea);
            textarea.focus();
            textarea.select();
            document.execCommand('copy');
            document.body.removeChild(textarea);
        }
    }
});
