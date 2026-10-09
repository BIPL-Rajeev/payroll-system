// Downloads a byte stream produced by the API in the browser.
// Called from Blazor: window.downloadFile(name, contentType, base64).
window.downloadFile = (fileName, contentType, base64Data) => {
    const link = document.createElement('a');
    link.href = 'data:' + contentType + ';base64,' + base64Data;
    link.download = fileName || 'download.xlsx';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
