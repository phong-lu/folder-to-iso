import Foundation

@main
enum CoreTests {
    static func main() throws {
        let fm = FileManager.default
        let root = fm.temporaryDirectory.appendingPathComponent("foldertoiso-test-\(UUID().uuidString)").resolvingSymlinksInPath()
        let source = root.appendingPathComponent("Source with spaces")
        try fm.createDirectory(at: source, withIntermediateDirectories: true)
        defer { try? fm.removeItem(at: root) }
        let target = root.appendingPathComponent("existing.iso")
        let original = Data("Existing output must survive cancellation".utf8)
        try original.write(to: target)
        precondition(IsoWriter.suggestedOutput(source.path + "/") == source.path + ".iso")
        precondition(IsoWriter.suggestedOutput(" ") == "")
        let cancelled = ImageCancellation()
        cancelled.cancel()
        do {
            try IsoWriter.create(source: source.path, destination: target.path, label: "Test", overwrite: true, cancellation: cancelled)
            fatalError("Pre-cancellation was ignored")
        } catch ImageError.cancelled { }
        let large = source.appendingPathComponent("large.bin")
        _ = fm.createFile(atPath: large.path, contents: nil)
        let file = try FileHandle(forWritingTo: large)
        try file.truncate(atOffset: (1 << 32) + 1048576)
        try file.close()
        let running = ImageCancellation()
        DispatchQueue.global().asyncAfter(deadline: .now() + 0.1) { running.cancel() }
        let start = Date()
        do {
            try IsoWriter.create(source: source.path, destination: target.path, label: "Test", overwrite: true, cancellation: running)
            fatalError("Cancellation was ignored")
        } catch ImageError.cancelled { }
        precondition(Date().timeIntervalSince(start) < 15)
        let afterCancellation = try Data(contentsOf: target)
        precondition(afterCancellation == original)
        let leftovers = try fm.contentsOfDirectory(atPath: root.path).filter { $0.hasPrefix(".foldertoiso-") }
        precondition(leftovers.isEmpty)
        print("PASS: macOS automatic naming, pre-cancellation, process cancellation, output preservation and cleanup.")
    }
}
