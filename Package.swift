// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "SnapLingo",
    platforms: [
        .macOS(.v14),
    ],
    products: [
        .executable(name: "SnapLingo", targets: ["SnapLingo"]),
    ],
    targets: [
        .executableTarget(
            name: "SnapLingo",
            linkerSettings: [
                .linkedFramework("AppKit"),
                .linkedFramework("ApplicationServices"),
                .linkedFramework("Carbon"),
                .linkedFramework("Security"),
                .linkedFramework("SwiftUI"),
            ]
        ),
        .testTarget(
            name: "SnapLingoTests",
            dependencies: ["SnapLingo"]
        ),
    ]
)
