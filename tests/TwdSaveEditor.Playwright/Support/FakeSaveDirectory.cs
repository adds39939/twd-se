using Microsoft.Playwright;

namespace TwdSaveEditor.Playwright.Support;

internal static class FakeSaveDirectory
{
    private const string InstallScript = """
        (files) => {
            const decode = (b64) => Uint8Array.from(atob(b64), c => c.charCodeAt(0));
            const encode = (bytes) => {
                let binary = '';
                for (let i = 0; i < bytes.length; i += 8192) {
                    binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 8192));
                }
                return btoa(binary);
            };
            const notFound = (name) => new DOMException(`${name} not found`, 'NotFoundError');

            const makeDirectory = (name) => {
                const dir = { kind: 'directory', name, files: {}, directories: {} };

                const fileHandle = (fileName) => ({
                    kind: 'file',
                    name: fileName,
                    getFile: async () => new Blob([dir.files[fileName]]),
                    createWritable: async () => {
                        if (dir !== root && root.backupsBlocked) {
                            throw new DOMException('Writing backups is not allowed', 'NotAllowedError');
                        }
                        const chunks = [];
                        return {
                            write: async (data) => chunks.push(new Uint8Array(data.buffer ?? data)),
                            close: async () => {
                                const bytes = new Uint8Array(chunks.reduce((n, c) => n + c.length, 0));
                                let offset = 0;
                                for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
                                dir.files[fileName] = bytes;
                            }
                        };
                    }
                });

                dir.getFileHandle = async (fileName, options) => {
                    if (!(fileName in dir.files)) {
                        if (!options?.create) {
                            throw notFound(fileName);
                        }
                        dir.files[fileName] = new Uint8Array(0);
                    }
                    return fileHandle(fileName);
                };

                dir.removeEntry = async (entryName) => {
                    if (entryName in dir.files) {
                        delete dir.files[entryName];
                    } else if (entryName in dir.directories) {
                        delete dir.directories[entryName];
                    } else {
                        throw notFound(entryName);
                    }
                };
                dir.getDirectoryHandle = async (dirName, options) => {
                    if (!(dirName in dir.directories)) {
                        if (!options?.create) {
                            throw notFound(dirName);
                        }
                        dir.directories[dirName] = makeDirectory(dirName);
                    }
                    return dir.directories[dirName];
                };

                dir[Symbol.asyncIterator] = async function* () {
                    for (const fileName of Object.keys(dir.files)) {
                        yield [fileName, fileHandle(fileName)];
                    }
                    for (const dirName of Object.keys(dir.directories)) {
                        yield [dirName, dir.directories[dirName]];
                    }
                };

                return dir;
            };

            const root = makeDirectory('TestData');
            for (const [name, b64] of Object.entries(files)) {
                root.files[name] = decode(b64);
            }

            window.__saveDirectory = {
                readFile: (name) => name in root.files ? encode(root.files[name]) : null,
                fileNames: () => Object.keys(root.files),
                backups: () => Object.entries(root.directories)
                    .map(([folder, dir]) => ({ folder, files: Object.keys(dir.files) })),
                blockBackups: () => { root.backupsBlocked = true; },
            };
            window.showDirectoryPicker = async () => root;
        }
        """;

    public static Task InstallAsync(IPage page, IReadOnlyDictionary<string, string> base64Files)
        => page.EvaluateAsync(InstallScript, base64Files);

    public static Task<string?> ReadFileAsync(IPage page, string name)
        => page.EvaluateAsync<string?>("(name) => window.__saveDirectory.readFile(name)", name);

    public static Task<string[]> GetFileNamesAsync(IPage page)
        => page.EvaluateAsync<string[]>("() => window.__saveDirectory.fileNames()");

    public static Task<string?> GetLastBackupFolderAsync(IPage page)
        => page.EvaluateAsync<string?>("() => window.__saveDirectory.backups().at(-1)?.folder ?? null");

    public static Task<string[]?> GetLastBackupFilesAsync(IPage page)
        => page.EvaluateAsync<string[]?>("() => window.__saveDirectory.backups().at(-1)?.files ?? null");

    public static Task BlockBackupsAsync(IPage page)
        => page.EvaluateAsync("() => window.__saveDirectory.blockBackups()");

    public static async Task AddSaveAsync(IDictionary<string, string> files, string testDataSeason, string fileName,
        string? injectName = null)
    {
        files[injectName ?? fileName] = await ReadBase64Async(TestDataHelper.GetPath(testDataSeason, fileName));

        var seasonDir = TestDataHelper.GetSeasonDir(testDataSeason);
        var bundleBase = Path.GetFileNameWithoutExtension(fileName);

        var autosaveName = $"_{bundleBase}_autosave.bundle";
        var autosavePath = Path.Combine(seasonDir, autosaveName);
        if (File.Exists(autosavePath))
        {
            files[autosaveName] = await ReadBase64Async(autosavePath);
        }

        var estoreName = $"_{bundleBase}_id.estore";
        var estorePath = Path.Combine(seasonDir, estoreName);
        if (!File.Exists(estorePath))
        {
            return;
        }

        files[estoreName] = await ReadBase64Async(estorePath);
        foreach (var epagePath in Directory.GetFiles(seasonDir, $"_{bundleBase}_id_Page*.epage").OrderBy(f => f))
        {
            files[Path.GetFileName(epagePath)] = await ReadBase64Async(epagePath);
        }
    }

    private static async Task<string> ReadBase64Async(string path)
        => Convert.ToBase64String(await File.ReadAllBytesAsync(path));
}
