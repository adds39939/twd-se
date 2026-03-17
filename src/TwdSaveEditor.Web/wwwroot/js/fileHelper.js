let directoryHandle = null;

window.fileSystemApi = {
    isSupported: () => !!window.showDirectoryPicker,

    pickDirectory: async () => {
        try {
            directoryHandle = await window.showDirectoryPicker({ mode: 'readwrite' });
            return true;
        } catch {
            return false;
        }
    },

    listFiles: async (extension) => {
        if (!directoryHandle) return [];
        const files = [];
        for await (const [name, handle] of directoryHandle) {
            if (handle.kind === 'file' && name.endsWith(extension))
                files.push(name);
        }
        return files;
    },

    readFile: async (name) => {
        if (!directoryHandle) return null;
        try {
            const fileHandle = await directoryHandle.getFileHandle(name);
            const file = await fileHandle.getFile();
            const buffer = await file.arrayBuffer();
            return btoa(String.fromCharCode(...new Uint8Array(buffer)));
        } catch {
            return null;
        }
    },

    readFileBytes: async (name) => {
        if (!directoryHandle) return null;
        try {
            const fileHandle = await directoryHandle.getFileHandle(name);
            const file = await fileHandle.getFile();
            const buffer = await file.arrayBuffer();
            return new Uint8Array(buffer);
        } catch {
            return null;
        }
    },

    writeFile: async (name, bytesBase64) => {
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
    },

    writeFileBytes: async (name, bytes) => {
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
    },

    hasDirectory: () => !!directoryHandle,

    getDirectoryName: () => directoryHandle?.name || ''
};
