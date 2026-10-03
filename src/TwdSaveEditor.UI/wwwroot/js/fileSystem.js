const databaseName = 'twd-save-editor';
const storeName = 'directories';
const rememberedKey = 'saves';
const readWrite = { mode: 'readwrite' };

let directoryHandle = null;

export function isSupported() {
    return !!window.showDirectoryPicker;
}

export async function pickDirectory() {
    try {
        directoryHandle = await window.showDirectoryPicker({ mode: 'readwrite', startIn: 'documents' });
    } catch {
        return false;
    }
    await remember(directoryHandle);
    return true;
}

export async function rememberedDirectory() {
    const handle = await remembered();
    if (!handle?.queryPermission) {
        return null;
    }
    const granted = await handle.queryPermission(readWrite) === 'granted';
    if (granted) {
        directoryHandle = handle;
    }
    return { name: handle.name, granted };
}

export async function reopenDirectory() {
    const handle = await remembered();
    if (!handle?.requestPermission || await handle.requestPermission(readWrite) !== 'granted') {
        return false;
    }
    directoryHandle = handle;
    return true;
}

export async function listFiles(extension) {
    if (!directoryHandle) {
        return [];
    }
    const files = [];
    for await (const [name, handle] of directoryHandle) {
        if (handle.kind === 'file' && name.endsWith(extension)) {
            files.push(name);
        }
    }
    return files;
}

export async function readFile(name) {
    if (!directoryHandle) {
        return null;
    }
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
    if (!directoryHandle) {
        return null;
    }
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
    if (!directoryHandle) {
        return false;
    }
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
    if (!directoryHandle) {
        return false;
    }
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

export async function deleteFile(name) {
    if (!directoryHandle) {
        return false;
    }
    try {
        await directoryHandle.removeEntry(name);
        return true;
    } catch {
        return false;
    }
}

export async function backupFiles(folderName, fileNames) {
    if (!directoryHandle) {
        return { folder: null, error: null };
    }

    let folder = null;
    let backupDir = null;
    for (const name of fileNames) {
        try {
            const source = await readSource(name);
            if (!source) {
                continue;
            }
            if (!backupDir) {
                folder = await unusedEntryName(folderName);
                backupDir = await directoryHandle.getDirectoryHandle(folder, { create: true });
            }
            const writable = await (await backupDir.getFileHandle(name, { create: true })).createWritable();
            await writable.write(await source.arrayBuffer());
            await writable.close();
        } catch (e) {
            if (folder) {
                await directoryHandle.removeEntry(folder, { recursive: true }).catch(() => { });
            }
            return { folder: null, error: `${name}: ${e.message}` };
        }
    }
    return { folder, error: null };
}

async function readSource(name) {
    try {
        return await (await directoryHandle.getFileHandle(name)).getFile();
    } catch (e) {
        if (e.name === 'NotFoundError') {
            return null;
        }
        throw e;
    }
}

async function unusedEntryName(baseName) {
    for (let index = 1; ; index++) {
        const name = index === 1 ? baseName : `${baseName}_${index}`;
        try {
            await directoryHandle.getDirectoryHandle(name);
        } catch (e) {
            if (e.name === 'NotFoundError') {
                return name;
            }
            if (e.name !== 'TypeMismatchError') {
                throw e;
            }
        }
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

function openDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(storeName);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function withStore(mode, action) {
    const database = await openDatabase();
    try {
        return await new Promise((resolve, reject) => {
            const request = action(database.transaction(storeName, mode).objectStore(storeName));
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    } finally {
        database.close();
    }
}

async function remember(handle) {
    try {
        await withStore('readwrite', store => store.put(handle, rememberedKey));
    } catch {
    }
}

async function remembered() {
    try {
        return await withStore('readonly', store => store.get(rememberedKey));
    } catch {
        return null;
    }
}
