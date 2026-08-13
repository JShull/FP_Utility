mergeInto(LibraryManager.library, {
    FP_DownloadBytes: function (data, dataLength, fileName, mimeType) {
        var safeFileName = UTF8ToString(fileName);
        var safeMimeType = UTF8ToString(mimeType);
        var bytes = HEAPU8.slice(data, data + dataLength);
        var blob = new Blob([bytes], { type: safeMimeType });
        var objectUrl = URL.createObjectURL(blob);
        var anchor = document.createElement('a');
        anchor.style.display = 'none';
        anchor.href = objectUrl;
        anchor.download = safeFileName;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        window.setTimeout(function () {
            URL.revokeObjectURL(objectUrl);
        }, 1000);
    }
});
