using System.Runtime.Versioning;

// Reel runs on macOS; tests lock folders with Unix file modes to simulate unreadable volumes.
[assembly: UnsupportedOSPlatform("windows")]
