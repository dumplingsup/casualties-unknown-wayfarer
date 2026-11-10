"""CU Mod Kit: offline, exact-version profile planner and verified bundle builder.

Python 3.10+, standard library only. Never writes to the game or user saves.
"""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import sys
import zipfile


class Invalid(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise Invalid(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write_new(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def relative(value):
    require(isinstance(value, str) and value, "Empty target path")
    require("\\" not in value and ":" not in value, f"Non-portable target: {value}")
    parts = value.split("/")
    require(not PurePosixPath(value).is_absolute(), f"Absolute target: {value}")
    reserved = {"con", "prn", "aux", "nul"} | {f"{p}{n}" for p in ("com", "lpt") for n in range(1, 10)}
    for part in parts:
        require(part not in ("", ".", "..") and not part.endswith((" ", ".")), f"Unsafe target: {value}")
        require(not re.search(r'[<>"|?*\x00-\x1f]', part), f"Unsafe target: {value}")
        require(part.split(".")[0].lower() not in reserved, f"Reserved target: {value}")
    return value


def source_bytes(record):
    path = Path(record["source"])
    require(path.is_file(), f"Missing source: {path}")
    if "member" in record:
        with zipfile.ZipFile(path) as archive:
            members = [i for i in archive.infolist() if i.filename == record["member"]]
            require(len(members) == 1 and not members[0].is_dir(), "Missing/duplicate archive member")
            require(members[0].file_size <= 256 * 1024 * 1024, "Archive member exceeds 256 MiB")
            return archive.read(members[0])
    require(path.stat().st_size <= 256 * 1024 * 1024, "Source exceeds 256 MiB")
    return path.read_bytes()


def resolve(base, path):
    path = Path(path)
    return str((base / path).resolve())


def make_lock(profile_path):
    profile_path = Path(profile_path).resolve()
    p = read_json(profile_path)
    require(p.get("schema") == 1 and p.get("game") == "casualties-unknown", "Unsupported profile")
    components = {}
    for mod in p["components"]:
        identity = mod["id"]
        require(re.fullmatch(r"[a-z0-9][a-z0-9._-]*", identity), f"Invalid id: {identity}")
        require(identity not in components, f"Duplicate id: {identity}")
        require(isinstance(mod["version"], str) and mod["version"] and not any(x in mod["version"] for x in "*<>=^~"), "Use exact component versions")
        components[identity] = mod
    selected = p["enabled"]
    require(selected and len(selected) == len(set(selected)), "Empty/duplicate enabled list")
    require(set(selected) <= set(components), "Unknown enabled component")
    edges = {}
    for identity in selected:
        mod = components[identity]
        deps = mod.get("requires", {})
        require(set(deps) <= set(selected), f"Missing enabled dependencies for {identity}")
        for dep, version in deps.items():
            require(components[dep]["version"] == version, f"Dependency version mismatch: {identity} -> {dep}")
        require(not set(mod.get("conflicts", [])) & set(selected), f"Declared conflict: {identity}")
        edges[identity] = set(deps) | (set(mod.get("after", [])) & set(selected))
    order = []
    while edges:
        ready = sorted(k for k, deps in edges.items() if not deps)
        require(ready, f"Dependency/order cycle: {sorted(edges)}")
        order.extend(ready)
        edges = {k: deps - set(ready) for k, deps in edges.items() if k not in ready}
    fingerprints = []
    for item in p["fingerprints"]:
        item = dict(item, source=resolve(profile_path.parent, item["source"]))
        require(digest(source_bytes(item)) == item["sha256"], f"Environment changed: {item['source']}")
        fingerprints.append(item)
    require(fingerprints, "At least one game fingerprint is required")
    files = []
    targets = set()
    for identity in order:
        for item in components[identity]["files"]:
            target = relative(item["target"])
            lower = target.casefold()
            require(lower not in targets, f"File collision: {target}")
            require(not any(lower.startswith(t + "/") or t.startswith(lower + "/") for t in targets), f"File/directory collision: {target}")
            targets.add(lower)
            item = dict(item, source=resolve(profile_path.parent, item["source"]), owner=identity)
            payload = source_bytes(item)
            if "sha256" in item:
                require(digest(payload) == item["sha256"], f"Source changed: {item['source']}")
            item.update(sha256=digest(payload), size=len(payload))
            files.append(item)
    require(files, "Profile has no payload")
    return {"schema": 1, "game": p["game"], "profile": p["name"],
            "profile_sha256": digest(profile_path.read_bytes()), "build": p.get("build"),
            "fingerprints": fingerprints, "order": order,
            "versions": {i: components[i]["version"] for i in order},
            "files": files, "runtime_status": "NOT_RUN"}


def checked_files(lock):
    require(lock.get("schema") == 1 and lock.get("game") == "casualties-unknown", "Unsupported lock")
    seen = set()
    for record in lock["files"]:
        target = relative(record["target"])
        folded = target.casefold()
        require(folded not in seen and not any(folded.startswith(t + "/") or t.startswith(folded + "/") for t in seen), f"Colliding lock target: {target}")
        seen.add(folded)
        require(re.fullmatch("[a-f0-9]{64}", record["sha256"]), "Invalid SHA-256")
    require(seen, "Empty lock")
    return lock["files"]


def stage(lock_path, output):
    lock = read_json(lock_path)
    records = checked_files(lock)
    output = Path(output).absolute()
    require(not output.exists(), "Output must be a NEW directory")
    for parent in [output.parent, *output.parents]:
        require(not parent.is_symlink() and not (hasattr(parent, "is_junction") and parent.is_junction()), "Output ancestors must not be links/junctions")
    # Read and validate everything before producing any payload.
    for record in lock["fingerprints"]:
        require(digest(source_bytes(record)) == record["sha256"], "Game fingerprint drift")
    payloads = []
    total = 0
    for record in records:
        data = source_bytes(record)
        require(digest(data) == record["sha256"] and len(data) == record["size"], f"Locked source drift: {record['source']}")
        total += len(data)
        require(total <= 512 * 1024 * 1024, "Bundle exceeds 512 MiB; split the profile")
        payloads.append(data)
    output.mkdir(parents=True, exist_ok=False)
    # COMPLETE is written last. Interrupted stages remain incomplete and are not reused.
    for record, data in zip(records, payloads):
        target = output / "payload" / record["target"]
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open("xb") as stream:
            stream.write(data)
    write_new(output / "lock.json", lock)
    with (output / "COMPLETE").open("x", encoding="ascii") as stream:
        stream.write(digest((output / "lock.json").read_bytes()))
    return {"output": str(output), "files": len(records), "runtime_status": "NOT_RUN"}


def verify(folder):
    folder = Path(folder)
    lock = read_json(folder / "lock.json")
    require((folder / "COMPLETE").read_text(encoding="ascii") == digest((folder / "lock.json").read_bytes()), "Incomplete/changed lock")
    records = checked_files(lock)
    expected = {r["target"] for r in records}
    payload = folder / "payload"
    require(not payload.is_symlink() and not (hasattr(payload, "is_junction") and payload.is_junction()), "Linked payload root")
    for path in payload.rglob("*"):
        require(not path.is_symlink() and not (hasattr(path, "is_junction") and path.is_junction()), "Linked payload")
    actual = {p.relative_to(payload).as_posix() for p in payload.rglob("*") if p.is_file()}
    require(actual == expected, f"File set mismatch: missing={expected-actual}, extra={actual-expected}")
    for record in records:
        data = (payload / record["target"]).read_bytes()
        require(digest(data) == record["sha256"] and len(data) == record["size"], f"Payload drift: {record['target']}")
    return {"status": "PASS", "files": len(records), "runtime_status": "NOT_RUN"}


def doctor(game):
    game = Path(game).resolve()
    assembly = game / "CasualtiesUnknown_Data/Managed/Assembly-CSharp.dll"
    require(assembly.is_file() and (game / "CasualtiesUnknown.exe").is_file(), "Not a recognized Casualties installation")
    manifest = game.parent.parent / "appmanifest_4576490.acf"
    match = re.search(r'"buildid"\s+"(\d+)"', manifest.read_text(encoding="utf-8")) if manifest.exists() else None
    return {"game": str(game), "build": match[1] if match else "UNKNOWN",
            "assembly_sha256": digest(assembly.read_bytes()),
            "bepinex_present": (game / "BepInEx/core/BepInEx.dll").is_file(),
            "mono_present": (game / "MonoBleedingEdge").is_dir(),
            "runtime_status": "NOT_RUN"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    d = sub.add_parser("doctor"); d.add_argument("game")
    l = sub.add_parser("lock"); l.add_argument("profile"); l.add_argument("--out", required=True)
    s = sub.add_parser("stage"); s.add_argument("lock"); s.add_argument("--out", required=True)
    v = sub.add_parser("verify"); v.add_argument("folder")
    args = parser.parse_args()
    try:
        if args.command == "doctor": result = doctor(args.game)
        elif args.command == "lock":
            result = make_lock(args.profile); write_new(args.out, result)
        elif args.command == "stage": result = stage(args.lock, args.out)
        else: result = verify(args.folder)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except (Invalid, OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as exc:
        print(json.dumps({"status": "FAIL", "error": str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
