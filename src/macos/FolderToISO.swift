import SwiftUI
import AppKit
import UniformTypeIdentifiers

@MainActor
final class AppModel: ObservableObject {
    static let shared = AppModel()
    @Published var source = "" {
        didSet {
            output = IsoWriter.suggestedOutput(source)
            label = source.isEmpty ? "" : String(URL(fileURLWithPath: source).lastPathComponent.prefix(32))
            createdOutput = nil
        }
    }
    @Published var output = ""
    @Published var label = "FolderToISO"
    @Published var status = "Ready"
    @Published var percent: Double?
    @Published var busy = false
    @Published var editingOutput = false
    @Published var createdOutput: String?

    var validSource: Bool {
        var directory: ObjCBool = false
        return !source.isEmpty && FileManager.default.fileExists(atPath: source, isDirectory: &directory) && directory.boolValue
    }

    func revealOutput() {
        if let output = createdOutput { NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: output)]) }
    }
    var pendingQuit = false
    private var cancellation: ImageCancellation?

    init() {
        let arguments = Array(CommandLine.arguments.dropFirst())
        if arguments.count == 2 && arguments[0] == "--source" {
            source = arguments[1]
            output = IsoWriter.suggestedOutput(source)
            label = String(URL(fileURLWithPath: source).lastPathComponent.prefix(32))
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
        guard !busy && validSource else { return }
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
        createdOutput = nil
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
                switch result {
                case .failure(let error): self.status = error.localizedDescription
                case .success: self.createdOutput = destination
                }
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
            ScrollView {
                VStack(alignment: .leading, spacing: 24) {
                    HStack(spacing: 18) {
                        if let url = Bundle.main.url(forResource: "FolderToISO", withExtension: "png"),
                           let logo = NSImage(contentsOf: url) {
                            Image(nsImage: logo).resizable().scaledToFit().frame(width: 64, height: 64)
                                .accessibilityLabel("FolderToISO icon")
                        }
                        VStack(alignment: .leading, spacing: 6) {
                            Text("FolderToISO").font(.system(size: 28, weight: .semibold))
                            Text("Burn a folder contents to ISO file").foregroundStyle(.secondary)
                        }
                    }

                    VStack(alignment: .leading, spacing: 20) {
                        VStack(alignment: .leading, spacing: 6) {
                            Text("Source folder")
                            HStack(spacing: 12) {
                                TextField("Choose a folder", text: $model.source)
                                    .textFieldStyle(.roundedBorder)
                                Button("Browse…", action: model.browseSource).frame(minWidth: 108)
                            }
                            Text("Your folder's contents become the root of the image.")
                                .font(.caption).foregroundStyle(.secondary)
                            if !model.source.isEmpty && !model.validSource {
                                Text("Invalid folder").font(.caption).foregroundStyle(.red)
                            }
                        }
                        if !model.source.isEmpty {
                            if model.editingOutput {
                                VStack(alignment: .leading, spacing: 6) {
                                    Text("ISO file")
                                    HStack(spacing: 12) {
                                        TextField("Choose where to save the .iso file", text: $model.output)
                                            .textFieldStyle(.roundedBorder)
                                        Button("Save as…", action: model.browseOutput).frame(minWidth: 108)
                                    }
                                }
                                VStack(alignment: .leading, spacing: 6) {
                                    Text("Volume label")
                                    TextField("Volume label", text: $model.label).textFieldStyle(.roundedBorder)
                                        .onChange(of: model.label) { _, value in
                                            if value.count > 32 { model.label = String(value.prefix(32)) }
                                        }
                                    Text("The output ISO's volume label").font(.caption).foregroundStyle(.secondary)
                                }
                            } else {
                                HStack(spacing: 12) {
                                    VStack(alignment: .leading, spacing: 6) {
                                        Text("ISO file: \(model.output)").textSelection(.enabled)
                                        Text("Volume label: \(model.label)")
                                    }.frame(maxWidth: .infinity, alignment: .leading)
                                    if model.validSource {
                                        Button("Change output") { model.editingOutput = true }
                                    }
                                }
                            }
                        }
                    }
                    .padding(24)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 12))
                    .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.primary.opacity(0.12), lineWidth: 1))
                    .controlSize(.large)
                    .disabled(model.busy)

                    VStack(spacing: 16) {
                        HStack(spacing: 12) {
                            Button("Create ISO", action: model.create)
                                .keyboardShortcut(.defaultAction).buttonStyle(.borderedProminent)
                                .disabled(model.busy || !model.validSource).frame(minWidth: 132)
                            Button("Cancel", action: model.cancel).disabled(!model.busy).frame(minWidth: 96)
                            Spacer(minLength: 8)
                            Text(model.status).textSelection(.enabled).foregroundStyle(.secondary)
                                .multilineTextAlignment(.trailing)
                            if model.createdOutput != nil {
                                Button(action: model.revealOutput) { Image(systemName: "folder") }
                                    .help("Open the contained folder").accessibilityLabel("Open the contained folder")
                            }
                        }
                        if let percent = model.percent { ProgressView(value: percent, total: 100) }
                        else if model.busy { ProgressView().controlSize(.small) }
                    }
                    .controlSize(.large)
                }
                .padding(32)
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .frame(width: 800, height: model.editingOutput ? 520 : (model.source.isEmpty ? 340 : 420))

        }
        .windowResizability(.contentSize)
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
