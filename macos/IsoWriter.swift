import Foundation
import Darwin

enum ImageError: LocalizedError {
    case message(String)
    case cancelled
    var errorDescription: String? {
        switch self {
        case .message(let text): return text
        case .cancelled: return "Cancelled. No incomplete ISO was kept."
        }
    }
}

final class ImageCancellation {
    private let lock = NSLock()
    private var cancelled = false
    private var process: Process?

    func cancel() {
        lock.lock(); defer { lock.unlock() }
        cancelled = true
        if let process = process, process.isRunning { process.terminate() }
    }

    func check() throws {
        lock.lock(); defer { lock.unlock() }
        if cancelled { throw ImageError.cancelled }
    }

    func start(_ process: Process) throws {
        lock.lock(); defer { lock.unlock() }
        if cancelled { throw ImageError.cancelled }
        try process.run()
        self.process = process
    }

    func commit(_ action: () throws -> Void) throws {
        lock.lock(); defer { lock.unlock() }
        if cancelled { throw ImageError.cancelled }
        try action()
        process = nil
    }
}

enum IsoWriter {
    static func suggestedOutput(_ source: String) -> String {
        let trimmed = source.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return "" }
        let path = (trimmed as NSString).standardizingPath
        return path + ".iso"
    }

    static func create(source: String, destination: String, label: String,
                       overwrite: Bool = false, cancellation: ImageCancellation = ImageCancellation(),
                       progress: (Double?, String) -> Void = { _, _ in }) throws {
        let fm = FileManager.default
        guard (source as NSString).isAbsolutePath, (destination as NSString).isAbsolutePath else {
            throw ImageError.message("Choose absolute source and output paths.")
        }
        let sourceURL = URL(fileURLWithPath: source).standardizedFileURL
        let initial = try sourceURL.resourceValues(forKeys: [.isDirectoryKey, .isSymbolicLinkKey])
        guard initial.isDirectory == true, initial.isSymbolicLink != true else {
            throw ImageError.message("Select an existing source folder without symbolic links.")
        }
        let sourceURLResolved = sourceURL.resolvingSymlinksInPath()
        let requested = URL(fileURLWithPath: destination).standardizedFileURL
        guard requested.pathExtension.lowercased() == "iso" else {
            throw ImageError.message("The output file must have an .iso extension.")
        }
        let directory = requested.deletingLastPathComponent().resolvingSymlinksInPath()
        var isDirectory: ObjCBool = false
        guard fm.fileExists(atPath: directory.path, isDirectory: &isDirectory), isDirectory.boolValue else {
            throw ImageError.message("The output folder does not exist.")
        }
        let target = directory.appendingPathComponent(requested.lastPathComponent)
        let volume = try sourceURLResolved.resourceValues(forKeys: [.volumeSupportsCaseSensitiveNamesKey])
        func comparisonPath(_ path: String) -> String {
            let normalized = path.precomposedStringWithCanonicalMapping
            return volume.volumeSupportsCaseSensitiveNames == true ? normalized :
                normalized.folding(options: .caseInsensitive, locale: Locale(identifier: "en_US_POSIX"))
        }
        let sourcePath = comparisonPath(sourceURLResolved.path)
        let targetPath = comparisonPath(target.path)
        let sourcePrefix = sourcePath == "/" ? "/" : sourcePath + "/"
        guard !targetPath.hasPrefix(sourcePrefix), targetPath != sourcePath else {
            throw ImageError.message("Save the ISO outside the source folder.")
        }
        let invalid = CharacterSet(charactersIn: "\\/:*?\"<>|").union(.controlCharacters)
        guard !label.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              label.utf16.count <= 32, label.rangeOfCharacter(from: invalid) == nil else {
            throw ImageError.message("Use a volume label of 1-32 characters without path punctuation or control characters.")
        }
        if fm.fileExists(atPath: target.path) && !overwrite {
            throw ImageError.message("The ISO already exists. Choose another path or confirm replacement.")
        }
        try cancellation.check()
        progress(nil, "Checking source files...")
        var enumerationError: Error?
        let keys: Set<URLResourceKey> = [.isSymbolicLinkKey, .isRegularFileKey, .isDirectoryKey, .fileSizeKey]
        guard let entries = fm.enumerator(at: sourceURLResolved, includingPropertiesForKeys: Array(keys),
                                         errorHandler: { _, error in enumerationError = error; return false }) else {
            throw ImageError.message("Could not read the source folder.")
        }
        var total: Int64 = 0
        for case let file as URL in entries {
            try cancellation.check()
            let values = try file.resourceValues(forKeys: keys)
            guard values.isSymbolicLink != true else {
                throw ImageError.message("Symbolic links are not supported: \(file.path)")
            }
            guard values.isRegularFile == true || values.isDirectory == true else {
                throw ImageError.message("Unsupported source entry: \(file.path)")
            }
            if values.isRegularFile == true {
                let (next, overflow) = total.addingReportingOverflow(Int64(values.fileSize ?? 0))
                guard !overflow else { throw ImageError.message("The source is too large.") }
                total = next
            }
        }
        if let error = enumerationError { throw error }
        let filesystem = try fm.attributesOfFileSystem(forPath: directory.path)
        if let free = filesystem[.systemFreeSize] as? NSNumber, free.int64Value < total {
            throw ImageError.message("The output volume does not have enough free space.")
        }
        let temporary = directory.appendingPathComponent(".foldertoiso-\(UUID().uuidString).partial.iso")
        defer { try? fm.removeItem(at: temporary) }
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/hdiutil")
        process.arguments = ["makehybrid", "-udf", "-udf-volume-name", label, "-o", temporary.path,
                             sourceURLResolved.path, "-puppetstrings"]
        let pipe = Pipe()
        process.standardOutput = pipe
        process.standardError = pipe
        process.standardInput = FileHandle.nullDevice
        progress(nil, "Preparing UDF image...")
        try cancellation.start(process)
        var diagnostics = Data()
        var pending = ""
        while true {
            let data = pipe.fileHandleForReading.availableData
            if data.isEmpty { break }
            diagnostics.append(data)
            if diagnostics.count > 65536 { diagnostics = Data(diagnostics.suffix(65536)) }
            pending += String(decoding: data, as: UTF8.self)
            let lines = pending.components(separatedBy: .newlines)
            pending = lines.last ?? ""
            for line in lines.dropLast() where line.hasPrefix("PERCENT:") || line.hasPrefix("PERCENTAGE:") {
                if let raw = line.split(separator: ":", maxSplits: 1).last,
                   let percent = Double(raw.trimmingCharacters(in: .whitespaces)) {
                    progress(percent < 0 ? nil : min(100, percent), "Writing ISO...")
                }
            }
        }
        process.waitUntilExit()
        try cancellation.check()
        guard process.terminationStatus == 0 else {
            throw ImageError.message("hdiutil failed: " + String(decoding: diagnostics, as: UTF8.self))
        }
        let size = try fm.attributesOfItem(atPath: temporary.path)[.size] as? NSNumber
        guard (size?.int64Value ?? 0) > 0 else { throw ImageError.message("hdiutil did not produce an image.") }
        try cancellation.commit {
            if fm.fileExists(atPath: target.path) && !overwrite {
                throw ImageError.message("The output file was created by another process. Choose another path.")
            }
            if Darwin.rename(temporary.path, target.path) != 0 {
                throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno))
            }
        }
        progress(100, "Created \(target.path)")
    }
}
