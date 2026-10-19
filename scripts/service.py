#!/usr/bin/env python3
"""Install/manage Reel's per-user login service. No root or extra supervisor."""
import argparse
import json
import os
from pathlib import Path
import plistlib
import secrets
import shlex
import shutil
import subprocess
import time
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parent.parent
LABEL = "local.reel"
SUPPORT = Path.home() / "Library/Application Support/Reel"
PLIST = Path.home() / f"Library/LaunchAgents/{LABEL}.plist"
TARGET = f"gui/{os.getuid()}/{LABEL}"


def launchctl(*args, check=True, quiet=False):
    return subprocess.run(["launchctl", *args], check=check, capture_output=quiet, text=True)


def loaded():
    return launchctl("print", TARGET, check=False, quiet=True).returncode == 0


def stop():
    # bootout returns before the old process has exited, and launchd keeps the service (and refuses to
    # bootstrap it again, error 5) until it has, so wait for it to go.
    launchctl("bootout", TARGET, check=False)
    deadline = time.monotonic() + 30
    while loaded():
        if time.monotonic() > deadline:
            raise SystemExit("Reel's old process did not exit within 30s; it may need kill -9 (see `status`).")
        time.sleep(0.5)


def bootstrap():
    result = launchctl("bootstrap", f"gui/{os.getuid()}", str(PLIST), check=False, quiet=True)
    if result.returncode != 0:
        raise SystemExit(f"launchctl bootstrap failed: {result.stderr.strip()}. Check `status`.")


def install(args):
    media = Path(args.media).expanduser().resolve()
    if not media.is_dir():
        raise SystemExit(f"Media folder unavailable: {media}")
    origin = urlparse(args.url)
    try:
        port = origin.port or 80
    except ValueError:  # a port that isn't a number
        port = None
    if origin.scheme != "http" or not origin.hostname or origin.path not in ("", "/") or port is None:
        raise SystemExit("Use the Studio's LAN origin, e.g. http://studio.local:8080")
    executables = {name: shutil.which(name) for name in ("dotnet", "ffmpeg", "ffprobe")}
    if not all(executables.values()):
        raise SystemExit("Install dotnet, ffmpeg and ffprobe first.")
    build = SUPPORT / "build"
    staging = SUPPORT / "staging"
    subprocess.run(["bash", str(ROOT / "scripts/publish.sh"), str(staging)], check=True)
    config_path = SUPPORT / "appsettings.json"
    # Merge so hand edits survive reinstall; only media and URL come from arguments.
    config = json.loads(config_path.read_text()) if config_path.exists() else {}
    origin_url = args.url.rstrip("/")
    reel = config.setdefault("Reel", {})
    reel.update(MediaRoot=str(media), Port=port, WebUrl=origin_url)
    if not reel.get("SecretKey"):
        reel["SecretKey"] = secrets.token_urlsafe(18)
    config.setdefault("Logging", {"LogLevel": {"Default": "Warning", "Microsoft.AspNetCore": "Warning"}})
    # Stop only after the replacement build succeeded.
    if PLIST.exists():
        stop()
    subprocess.run(["rsync", "-a", "--delete", str(staging) + "/", str(build) + "/"], check=True)
    config_path.write_text(json.dumps(config, indent=2) + "\n")
    config_path.chmod(0o600)
    # Console output goes to macOS's logger, which owns retention. No log worker.
    runner = SUPPORT / "run.sh"
    # The content root is SUPPORT, so the server reads (and live-reloads) the appsettings.json above, not a copy.
    command = " ".join(shlex.quote(part) for part in (
        executables["dotnet"], str(build / "Reel.Server.dll"),
        "--contentRoot", str(SUPPORT), "--webroot", str(build / "wwwroot")))
    # exec keeps dotnet as launchd's supervised process, so SIGTERM reaches it.
    runner.write_text(f"#!/bin/bash\nexec {command} > >(/usr/bin/logger -t Reel) 2>&1\n")
    runner.chmod(0o700)
    binary_dirs = list(dict.fromkeys(str(Path(value).parent) for value in executables.values()))
    environment = {"PATH": ":".join(binary_dirs + ["/usr/bin", "/bin"]), "ASPNETCORE_ENVIRONMENT": "Production"}
    # PathState starts the server at load and whenever the media folder (re)appears, and keeps it alive meanwhile.
    definition = {
        "Label": LABEL, "ProgramArguments": ["/bin/bash", str(runner)], "WorkingDirectory": str(build),
        "KeepAlive": {"PathState": {str(media): True}}, "ThrottleInterval": 30, "EnvironmentVariables": environment,
    }
    PLIST.parent.mkdir(parents=True, exist_ok=True)
    PLIST.write_bytes(plistlib.dumps(definition))
    bootstrap()
    print(f"Reel installed: {origin_url}. Pair using the TV QR code.")


def status():
    service = launchctl("print", TARGET, check=False, quiet=True)
    if service.returncode != 0:
        print("Reel is down: login service not loaded.")
        return 1
    pid = next((line.split("=")[1].strip() for line in service.stdout.splitlines()
                if line.strip().startswith("pid =")), None)
    config_path = SUPPORT / "appsettings.json"
    config = json.loads(config_path.read_text()) if config_path.exists() else {}
    reel = config.get("Reel", {})
    try:
        with urlopen(f"http://127.0.0.1:{reel.get('Port', 8080)}/api/health", timeout=3):
            print(f"Reel is running (healthy): {reel.get('WebUrl', '')}")
            return 0
    except HTTPError as error:
        try:
            reason = json.loads(error.read()).get("message") or error.reason
        except ValueError:
            reason = error.reason
        problem = f"unavailable ({reason})"
    except (URLError, OSError) as error:
        cause = getattr(error, "reason", error)
        if isinstance(cause, ConnectionRefusedError):
            print("Reel is down: loaded but not listening (starting, or media folder missing).")
            return 1
        problem = "hung (no health response within 3s)"
    print(f"Reel is {problem}; pid {pid or 'unknown'}.\n"
          "If the media drive is external, check for a macOS 'access files on a removable volume' "
          "prompt. A stuck process may ignore `stop` and need kill -9.")
    return 1


parser = argparse.ArgumentParser(description=__doc__)
sub = parser.add_subparsers(dest="action", required=True)
setup = sub.add_parser("install")
setup.add_argument("--media", required=True)
setup.add_argument("--url", required=True)
for action in ("start", "stop", "status", "uninstall"):
    sub.add_parser(action)
args = parser.parse_args()
if args.action == "install":
    install(args)
elif args.action == "status":
    raise SystemExit(status())
elif args.action == "stop":
    stop()
elif args.action == "start":
    if loaded():
        print("Reel is already running; use `status` to check health.")
    elif not PLIST.exists():
        raise SystemExit("Reel is not installed; run install first.")
    else:
        bootstrap()
elif args.action == "uninstall":
    stop()
    PLIST.unlink(missing_ok=True)
    print("Login service removed. Configuration and media progress retained.")
