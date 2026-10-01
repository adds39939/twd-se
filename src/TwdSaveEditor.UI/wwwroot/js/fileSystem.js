let directoryHandle = null;

export function isSupported() {
    return !!window.showDirectoryPicker;
}

export async function pickDirectory() {
    try {
        directoryHandle = await window.showDirectoryPicker({ mode: 'readwrite', startIn: 'documents' });
        return true;
    } catch {
        return false;
    }
}

export async function listFiles(extension) {
    if (!directoryHandle) return [];
    const files = [];
    for await (const [name, handle] of directoryHandle) {
        if (handle.kind === 'file' && name.endsWith(extension))
            files.push(name);
    }
    return files;
}

export async function readFile(name) {
    if (!directoryHandle) return null;
    try {
        const fileHandle = await directoryHandle.getFileHandle(name);
        const file = await fileHandle.getFile();
        const buffer = await file.arrayBuffer();
        const bytes = new Uint8Array(buffer);
        let binary = '';
        const chunkSize = 8192;
        for (let i = 0; i < bytes.length; i += chunkSize) {
            binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunkSize));
        }
        return btoa(binary);
    } catch {
        return null;
    }
}

export async function readFileBytes(name) {
    if (!directoryHandle) return null;
    try {
        const fileHandle = await directoryHandle.getFileHandle(name);
        const file = await fileHandle.getFile();
        const buffer = await file.arrayBuffer();
        return new Uint8Array(buffer);
    } catch {
        return null;
    }
}

export async function writeFile(name, bytesBase64) {
    if (!directoryHandle) return false;
    try {
        const fileHandle = await directoryHandle.getFileHandle(name, { create: true });
        const writable = await fileHandle.createWritable();
        const bytes = Uint8Array.from(atob(bytesBase64), c => c.charCodeAt(0));
        await writable.write(bytes);
        await writable.close();
        return true;
    } catch {
        return false;
    }
}

export async function writeFileBytes(name, bytes) {
    if (!directoryHandle) return false;
    try {
        const fileHandle = await directoryHandle.getFileHandle(name, { create: true });
        const writable = await fileHandle.createWritable();
        await writable.write(bytes);
        await writable.close();
        return true;
    } catch {
        return false;
    }
}

export async function backupFiles(folderName, fileNames) {
    if (!directoryHandle) return false;
    try {
        const backupDir = await directoryHandle.getDirectoryHandle(folderName, { create: true });
        for (const name of fileNames) {
            try {
                const srcHandle = await directoryHandle.getFileHandle(name);
                const srcFile = await srcHandle.getFile();
                const srcData = await srcFile.arrayBuffer();
                const destHandle = await backupDir.getFileHandle(name, { create: true });
                const writable = await destHandle.createWritable();
                await writable.write(srcData);
                await writable.close();
            } catch { }
        }
        return true;
    } catch {
        return false;
    }
}

export function hasDirectory() {
    return !!directoryHandle;
}

export function getDirectoryName() {
    return directoryHandle?.name || '';
}

export function downloadFile(fileName, base64Data) {
    const bytes = Uint8Array.from(atob(base64Data), c => c.charCodeAt(0));
    const blob = new Blob([bytes], { type: 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
    URL.revokeObjectURL(url);
}
