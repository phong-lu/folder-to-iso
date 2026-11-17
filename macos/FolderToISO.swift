import SwiftUI
import AppKit
import UniformTypeIdentifiers

@MainActor
final class AppModel: ObservableObject {
    static let shared = AppModel()
    @Published var source = "" {
        didSet { output = IsoWriter.suggestedOutput(source) }
    }
    @Published var output = ""
    @Published var label = "FolderToISO"
    @Published var status = "Ready"
    @Published var percent: Double?
    @Published var busy = false
    var pendingQuit = false
    private var cancellation: ImageCancellation?

    init() {
        let arguments = Array(CommandLine.arguments.dropFirst())
        if arguments.count == 2 && arguments[0] == "--source" {
            source = arguments[1]
            output = IsoWriter.suggestedOutput(source)
        } else if !arguments.isEmpty {
            status = "Usage: FolderToISO --source <folder path>"
        }
    }

    func browseSource() {
        let panel = NSOpenPanel()
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        panel.allowsMultipleSelection = false
        if panel.runModal() == .OK, let url = panel.url { source = url.path }
    }

    func browseOutput() {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [UTType(filenameExtension: "iso") ?? .data]
        panel.nameFieldStringValue = output.isEmpty ? "Image.iso" : URL(fileURLWithPath: output).lastPathComponent
        if !output.isEmpty { panel.directoryURL = URL(fileURLWithPath: output).deletingLastPathComponent() }
        if panel.runModal() == .OK, let url = panel.url { output = url.path }
    }

    func create() {
        guard !busy else { return }
        var overwrite = false
        if FileManager.default.fileExists(atPath: output) {
            let alert = NSAlert()
            alert.messageText = "Replace existing file?"
            alert.informativeText = output
            alert.addButton(withTitle: "Cancel")
            alert.addButton(withTitle: "Replace")
            guard alert.runModal() == .alertSecondButtonReturn else { return }
            overwrite = true
        }
        let source = self.source, destination = output, volume = label
        let token = ImageCancellation()
        cancellation = token
        busy = true
        percent = nil
        let replaceExisting = overwrite
        DispatchQueue.global(qos: .userInitiated).async {
            let result = Result {
                try IsoWriter.create(source: source, destination: destination, label: volume,
                                     overwrite: replaceExisting, cancellation: token) { percent, message in
                    DispatchQueue.main.async { self.percent = percent; self.status = message }
                }
            }
            DispatchQueue.main.async {
                if case .failure(let error) = result { self.status = error.localizedDescription }
                self.busy = false
                self.cancellation = nil
                if self.pendingQuit { NSApp.reply(toApplicationShouldTerminate: true) }
            }
        }
    }

    func cancel() { cancellation?.cancel(); status = "Cancelling..." }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        let model = AppModel.shared
        if model.busy { model.pendingQuit = true; model.cancel(); return .terminateLater }
        return .terminateNow
    }
}

struct FolderToISOApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    @StateObject private var model = AppModel.shared
    var body: some Scene {
        WindowGroup {
            VStack(alignment: .leading, spacing: 18) {
                Text("FolderToISO").font(.largeTitle.bold())
                Text("Turn a folder into a disc image").font(.title3)
                Text("UDF supports individual files larger than 4 GiB. Your folder's contents become the root of the image.")
                    .foregroundStyle(.secondary)
                VStack(alignment: .leading) {
                    Text("Source folder")
                    HStack { TextField("Choose a folder", text: $model.source); Button("Browse…", action: model.browseSource) }
                    Text("ISO file").padding(.top, 10)
                    HStack { TextField("Choose where to save the .iso file", text: $model.output); Button("Save as…", action: model.browseOutput) }
                    Text("Volume label").padding(.top, 10)
                    TextField("Volume label", text: $model.label)
                }.disabled(model.busy)
                Text("Save large images to APFS, HFS+ or exFAT. Keep the source folder unchanged until creation completes.")
                    .font(.callout).foregroundStyle(.secondary)
                HStack {
                    Button("Create ISO", action: model.create).keyboardShortcut(.defaultAction).disabled(model.busy)
                    Button("Cancel", action: model.cancel).disabled(!model.busy)
                }
                if let percent = model.percent { ProgressView(value: percent, total: 100) }
                else if model.busy { ProgressView().controlSize(.small) }
                Text(model.status).textSelection(.enabled).font(.callout)
            }.padding(28).frame(minWidth: 680, minHeight: 460)
        }
        .commands { CommandGroup(replacing: .newItem) { } }
    }
}

@main
enum Entry {
    @MainActor
    static func main() {
        let arguments = Array(CommandLine.arguments.dropFirst())
        if arguments.first == "--create" {
            guard arguments.count == 4 || (arguments.count == 5 && arguments[4] == "--replace") else {
                fputs("Usage: FolderToISO --create <source> <output.iso> <label> [--replace]\n", stderr)
                exit(2)
            }
            do {
                try IsoWriter.create(source: arguments[1], destination: arguments[2], label: arguments[3], overwrite: arguments.count == 5)
                print("Created \(arguments[2])")
            } catch { fputs(error.localizedDescription + "\n", stderr); exit(1) }
        } else {
            FolderToISOApp.main()
        }
    }
}
